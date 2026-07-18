using Chummer.Media.Contracts.Assets;
using Chummer.Media.Contracts.Rendering;

const string ShaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
const string ShaB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
const string ShaC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
const string ShaD = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
const string Commit = "0123456789abcdef0123456789abcdef01234567";

var now = DateTimeOffset.Parse("2026-07-18T00:00:00Z");
var manifest = new MediaAssetManifest(
    AssetId: "asset-public-proof",
    CatalogKey: "public/proof",
    RenderJobId: "render-public-proof",
    RenderKind: MediaRenderKind.Video,
    StorageBucket: "public-media",
    StorageObjectKey: "release/asset-public-proof.mp4",
    ContentType: "video/mp4",
    ContentLengthBytes: 42,
    ContentHash: ShaA,
    PreviewAssetId: null,
    ParentAssetId: null,
    Lifecycle: new MediaAssetLifecycleState(
        AssetApprovalStatus.Approved,
        now,
        now,
        null,
        now,
        null,
        null),
    DerivedAssetIds: []);
var authority = new MediaReleaseAuthorityBinding(
    AuthorityContract: PublicMediaAssetProjection.RequiredAuthorityContract,
    RegistryRepository: PublicMediaAssetProjection.RequiredRegistryRepository,
    RegistryCommit: Commit,
    ReleaseVersion: "run-20260718-preview",
    AuthoritySnapshotRef: "registry://release-evidence/run-20260718-preview/SNAPSHOT.json",
    AuthoritySnapshotSha256: ShaB,
    ManifestRef: "registry://release-evidence/run-20260718-preview/RELEASE_CHANNEL.json",
    ManifestSha256: ShaC,
    ReleaseDecisionRef: "registry://release-evidence/run-20260718-preview/RELEASE_DECISION.json",
    ReleaseDecisionSha256: ShaD,
    ReleaseDecisionStatus: "review_required",
    ProvenanceRef: "release-evidence://release-evidence/run-20260718-preview/provenance/media.json",
    ProvenanceSha256: ShaA);

var projection = PublicMediaAssetProjection.Create(manifest, authority);
Assert(ReferenceEquals(manifest, projection.Manifest), "Projection must preserve the exact manifest.");
Assert(ReferenceEquals(authority, projection.Authority), "Projection must preserve the exact authority binding.");

ExpectFailure(
    () => PublicMediaAssetProjection.Create(manifest, authority with { ManifestSha256 = "ABC" }),
    "Digest drift must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority with { ProvenanceRef = "file:///docker/private/provider-receipt.json" }),
    "Host-local provenance must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority with { AuthorityContract = "chummer.release-authority-snapshot/v1" }),
    "Stale authority contracts must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority with { AuthoritySnapshotRef = "SNAPSHOT.json" }),
    "Relative authority references must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority with { ManifestRef = "https://chummer.run/release/%2e%2e/private.json" }),
    "Encoded traversal in authority references must fail closed.");

Console.WriteLine("media-publication-authority: ok");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void ExpectFailure(Action action, string message)
{
    try
    {
        action();
    }
    catch (ArgumentException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
