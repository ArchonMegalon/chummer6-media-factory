using Chummer.Media.Contracts.Assets;
using Chummer.Media.Contracts.Rendering;

const string ShaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
const string ShaB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
const string ShaC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
const string ShaD = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
const string ShaE = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
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
var canonicalManifestSha256 = MediaAssetManifestDigest.ComputeSha256(manifest);
var authority = new MediaReleaseAuthorityBinding(
    AuthorityContract: PublicMediaAssetProjection.RequiredAuthorityContract,
    RegistryRepository: PublicMediaAssetProjection.RequiredRegistryRepository,
    RegistryCommit: Commit,
    ReleaseVersion: "run-20260718-preview",
    CurrentRef: "registry://release-evidence/CURRENT.json",
    CurrentSha256: ShaE,
    AuthoritySnapshotRef: $"registry://release-evidence/snapshots/run-20260718-preview/{ShaB}/SNAPSHOT.json",
    AuthoritySnapshotSha256: ShaB,
    ManifestRef: $"registry://release-evidence/snapshots/run-20260718-preview/{ShaB}/RELEASE_CHANNEL.json",
    ManifestSha256: ShaC,
    ReleaseDecisionRef: $"registry://release-evidence/snapshots/run-20260718-preview/{ShaB}/RELEASE_DECISION.json",
    ReleaseDecisionSha256: ShaD,
    ReleaseDecisionStatus: "review_required",
    ProvenanceRef: $"release-evidence://media-factory/snapshots/run-20260718-preview/{ShaB}/decisions/{ShaD}/assets/{manifest.AssetId}/{manifest.ContentHash}/manifests/{canonicalManifestSha256}/provenance/{ShaA}.json",
    ProvenanceSha256: ShaA,
    AssetId: manifest.AssetId,
    AssetContentSha256: manifest.ContentHash,
    MediaManifestCanonicalSha256: canonicalManifestSha256);
var eligibility = new MediaPublicEligibility(
    Contract: PublicMediaAssetProjection.RequiredEligibilityContract,
    CuratedForPublicRelease: true,
    CuratedBy: "media-release-curator",
    CuratedAtUtc: now,
    AuthoritySnapshotSha256: ShaB,
    AssetId: manifest.AssetId,
    AssetContentSha256: manifest.ContentHash,
    MediaManifestCanonicalSha256: canonicalManifestSha256);

var projection = PublicMediaAssetProjection.Create(manifest, authority, eligibility);
Assert(ReferenceEquals(manifest, projection.Manifest), "Projection must preserve the exact manifest.");
Assert(ReferenceEquals(authority, projection.Authority), "Projection must preserve the exact authority binding.");
Assert(ReferenceEquals(eligibility, projection.Eligibility), "Projection must preserve exact public eligibility.");
Assert(
    canonicalManifestSha256 == MediaAssetManifestDigest.ComputeSha256(manifest),
    "Canonical media-manifest hashing must be deterministic.");
Assert(
    canonicalManifestSha256 == "6b6a51b3b345d7a3734697ce66499e7ebc3001fb19f57d847347c10c6b2e0671",
    "C# and Python must share the exact canonical media-manifest vector.");
var seventhTickManifest = manifest with
{
    Lifecycle = manifest.Lifecycle with { ApprovedAtUtc = now.AddTicks(1) },
};
Assert(
    MediaAssetManifestDigest.ComputeSha256(seventhTickManifest)
        == "8645e54e544bf72f553f21b3945e9d42fe795312a9bdbd326914ec07d3b73b47",
    "C# and Python must preserve the seventh fractional timestamp digit.");
var utf16OrdinalLineageManifest = manifest with
{
    DerivedAssetIds = ["\uE000", "\U00010000"],
};
Assert(
    MediaAssetManifestDigest.ComputeSha256(utf16OrdinalLineageManifest)
        == "adde2158cc4667b0675a984d7fb44a7eceb3dca864881098cf3e76e753e582d8",
    "C# and Python must share UTF-16 ordinal derived-asset ordering.");

ExpectFailure(
    () => PublicMediaAssetProjection.Create(manifest, authority with { ManifestSha256 = "ABC" }, eligibility),
    "Digest drift must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority with { ProvenanceRef = "file:///docker/private/provider-receipt.json" },
        eligibility),
    "Host-local provenance must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority with { AuthorityContract = "chummer.release-authority-snapshot/v1" },
        eligibility),
    "Stale authority contracts must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority with { AuthoritySnapshotRef = "SNAPSHOT.json" },
        eligibility),
    "Relative authority references must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority with { ManifestRef = "https://chummer.run/release/%2e%2e/private.json" },
        eligibility),
    "Encoded traversal in authority references must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority,
        eligibility with { CuratedForPublicRelease = false }),
    "Uncurated assets must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest with { StorageBucket = " " },
        authority,
        eligibility),
    "Blank storage metadata must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest with { RenderJobId = "" },
        authority,
        eligibility),
    "Blank render metadata must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest with
        {
            Lifecycle = manifest.Lifecycle with
            {
                ApprovalStatus = AssetApprovalStatus.Rejected,
                RejectedAtUtc = now,
            },
        },
        authority,
        eligibility),
    "Rejected assets must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest with { Lifecycle = manifest.Lifecycle with { PurgedAtUtc = now } },
        authority,
        eligibility),
    "Purged assets must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest with { Lifecycle = manifest.Lifecycle with { PersistedAtUtc = null } },
        authority,
        eligibility),
    "Incomplete asset lifecycles must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority with
        {
            AuthoritySnapshotRef = "registry://release-evidence/snapshots/run-20260718-preview/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/SNAPSHOT.json",
        },
        eligibility),
    "Authority refs that omit the bound snapshot digest must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest with { AssetId = "asset-substitution" },
        authority,
        eligibility),
    "Asset-id substitution must fail even when content bytes are unchanged.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest with { ContentHash = ShaE },
        authority,
        eligibility),
    "Content substitution must fail even when authority bytes are unchanged.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority with { MediaManifestCanonicalSha256 = ShaE },
        eligibility),
    "Canonical manifest substitution must fail closed.");
ExpectFailure(
    () => PublicMediaAssetProjection.Create(
        manifest,
        authority,
        eligibility with { AssetId = "asset-substitution" }),
    "Curation cannot substitute another asset under the same release authority.");

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
