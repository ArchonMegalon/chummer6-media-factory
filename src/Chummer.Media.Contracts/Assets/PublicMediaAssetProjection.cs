using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Chummer.Media.Contracts.Assets;

/// <summary>
/// Immutable Registry and provenance authority carried by a public media projection.
/// Provider-private execution evidence remains outside this contract.
/// </summary>
public sealed record MediaReleaseAuthorityBinding(
    string AuthorityContract,
    string RegistryRepository,
    string RegistryCommit,
    string ReleaseVersion,
    string CurrentRef,
    string CurrentSha256,
    string AuthoritySnapshotRef,
    string AuthoritySnapshotSha256,
    string ManifestRef,
    string ManifestSha256,
    string ReleaseDecisionRef,
    string ReleaseDecisionSha256,
    string ReleaseDecisionStatus,
    string ProvenanceRef,
    string ProvenanceSha256,
    string AssetId,
    string AssetContentSha256,
    string MediaManifestCanonicalSha256);

/// <summary>
/// Explicit curation decision binding one asset to one governed authority snapshot.
/// This is separate from provider execution evidence and cannot promote a release.
/// </summary>
public sealed record MediaPublicEligibility(
    string Contract,
    bool CuratedForPublicRelease,
    string CuratedBy,
    DateTimeOffset CuratedAtUtc,
    string AuthoritySnapshotSha256,
    string AssetId,
    string AssetContentSha256,
    string MediaManifestCanonicalSha256);

/// <summary>
/// Cross-language canonical digest for one exact media manifest. The encoding is
/// a fixed sequence of nullable, length-prefixed UTF-8 values under a versioned
/// domain separator; it is deliberately independent of serializer defaults.
/// </summary>
public static class MediaAssetManifestDigest
{
    /// <summary>The canonical digest preimage contract.</summary>
    public const string Contract = "chummer.media.asset-manifest-digest/v1";

    /// <summary>Returns the lowercase SHA-256 of the exact canonical manifest.</summary>
    public static string ComputeSha256(MediaAssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(manifest.Lifecycle);
        ArgumentNullException.ThrowIfNull(manifest.DerivedAssetIds);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, Contract);
        Append(hash, manifest.AssetId);
        Append(hash, manifest.CatalogKey);
        Append(hash, manifest.RenderJobId);
        Append(hash, ((int)manifest.RenderKind).ToString(CultureInfo.InvariantCulture));
        Append(hash, manifest.StorageBucket);
        Append(hash, manifest.StorageObjectKey);
        Append(hash, manifest.ContentType);
        Append(hash, manifest.ContentLengthBytes.ToString(CultureInfo.InvariantCulture));
        Append(hash, manifest.ContentHash);
        Append(hash, manifest.PreviewAssetId);
        Append(hash, manifest.ParentAssetId);
        Append(hash, ((int)manifest.Lifecycle.ApprovalStatus).ToString(CultureInfo.InvariantCulture));
        Append(hash, FormatTimestamp(manifest.Lifecycle.CreatedAtUtc));
        Append(hash, FormatTimestamp(manifest.Lifecycle.ApprovedAtUtc));
        Append(hash, FormatTimestamp(manifest.Lifecycle.RejectedAtUtc));
        Append(hash, FormatTimestamp(manifest.Lifecycle.PersistedAtUtc));
        Append(hash, FormatTimestamp(manifest.Lifecycle.ExpiresAtUtc));
        Append(hash, FormatTimestamp(manifest.Lifecycle.PurgedAtUtc));
        var derivedAssetIds = manifest.DerivedAssetIds.Order(StringComparer.Ordinal).ToArray();
        Append(hash, derivedAssetIds.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var derivedAssetId in derivedAssetIds)
        {
            Append(hash, derivedAssetId);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string? FormatTimestamp(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
            CultureInfo.InvariantCulture);

    private static void Append(IncrementalHash hash, string? value)
    {
        if (value is null)
        {
            hash.AppendData([0]);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> header = stackalloc byte[5];
        header[0] = 1;
        BinaryPrimitives.WriteInt32BigEndian(header[1..], bytes.Length);
        hash.AppendData(header);
        hash.AppendData(bytes);
    }
}

/// <summary>
/// The only release-facing projection of a media manifest. Creation fails closed
/// unless exact Registry snapshot, decision, manifest, and provenance identities
/// are present and portable.
/// </summary>
public sealed class PublicMediaAssetProjection
{
    /// <summary>The Registry release-authority contract accepted by this boundary.</summary>
    public const string RequiredAuthorityContract = "chummer.release-authority-snapshot/v2";

    /// <summary>The sole repository authorized to issue release snapshots.</summary>
    public const string RequiredRegistryRepository = "ArchonMegalon/chummer6-hub-registry";

    /// <summary>The curation contract required before an asset can enter a public projection.</summary>
    public const string RequiredEligibilityContract = "chummer.media.public-eligibility/v2";

    private static readonly string[] ForbiddenLocalPathFragments =
    [
        "/tmp/",
        "/var/tmp/",
        "/docker/",
        "/workspace/",
        "/home/",
        "file://",
    ];

    private PublicMediaAssetProjection(
        MediaAssetManifest manifest,
        MediaReleaseAuthorityBinding authority,
        MediaPublicEligibility eligibility)
    {
        Manifest = manifest;
        Authority = authority;
        Eligibility = eligibility;
    }

    /// <summary>The exact rendered-asset manifest being projected.</summary>
    public MediaAssetManifest Manifest { get; }

    /// <summary>The immutable Registry and provenance identity for the projection.</summary>
    public MediaReleaseAuthorityBinding Authority { get; }

    /// <summary>The explicit public-curation decision bound to this exact snapshot.</summary>
    public MediaPublicEligibility Eligibility { get; }

    /// <summary>
    /// Creates a release-facing projection after validating portable immutable authority.
    /// </summary>
    public static PublicMediaAssetProjection Create(
        MediaAssetManifest manifest,
        MediaReleaseAuthorityBinding authority,
        MediaPublicEligibility eligibility)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(eligibility);

        RequireText(manifest.AssetId, nameof(manifest.AssetId));
        RequireText(manifest.CatalogKey, nameof(manifest.CatalogKey));
        RequireText(manifest.RenderJobId, nameof(manifest.RenderJobId));
        if (!Enum.IsDefined(manifest.RenderKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(manifest.RenderKind),
                "Public media must use one recognized render kind.");
        }

        RequireText(manifest.StorageBucket, nameof(manifest.StorageBucket));
        RequireStorageObjectKey(manifest.StorageObjectKey);
        RequireContentType(manifest.ContentType);
        if (manifest.ContentLengthBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(manifest.ContentLengthBytes),
                "Public media bytes must have a positive length.");
        }

        if (!string.Equals(
                eligibility.Contract,
                RequiredEligibilityContract,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Eligibility contract must be {RequiredEligibilityContract}.",
                nameof(eligibility));
        }

        if (!eligibility.CuratedForPublicRelease)
        {
            throw new ArgumentException(
                "Only an explicitly curated asset is eligible for a public projection.",
                nameof(eligibility));
        }

        RequireText(eligibility.CuratedBy, nameof(eligibility.CuratedBy));
        RequireUtcTimestamp(eligibility.CuratedAtUtc, nameof(eligibility.CuratedAtUtc));
        RequireSha256(manifest.ContentHash, nameof(manifest.ContentHash));
        ValidateLifecycle(manifest, eligibility);
        ValidateLineage(manifest);
        var canonicalManifestSha256 = MediaAssetManifestDigest.ComputeSha256(manifest);
        if (!string.Equals(
                authority.AuthorityContract,
                RequiredAuthorityContract,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"AuthorityContract must be {RequiredAuthorityContract}.",
                nameof(authority));
        }

        if (!string.Equals(
                authority.RegistryRepository,
                RequiredRegistryRepository,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"RegistryRepository must be {RequiredRegistryRepository}.",
                nameof(authority));
        }

        RequireHex(authority.RegistryCommit, 40, nameof(authority.RegistryCommit));
        RequireReleaseVersion(authority.ReleaseVersion);
        RequirePortableAuthorityRef(authority.CurrentRef, nameof(authority.CurrentRef));
        if (!string.Equals(
                authority.CurrentRef,
                "registry://release-evidence/CURRENT.json",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "CurrentRef must identify Registry CURRENT.json; CurrentSha256 pins its exact bytes.",
                nameof(authority));
        }

        RequireSha256(authority.CurrentSha256, nameof(authority.CurrentSha256));
        RequireSha256(authority.AuthoritySnapshotSha256, nameof(authority.AuthoritySnapshotSha256));
        RequirePortableAuthorityRef(authority.AuthoritySnapshotRef, nameof(authority.AuthoritySnapshotRef));
        RequireExactReleaseRef(
            authority.AuthoritySnapshotRef,
            authority.ReleaseVersion,
            authority.AuthoritySnapshotSha256,
            "SNAPSHOT.json",
            nameof(authority.AuthoritySnapshotRef));
        RequirePortableAuthorityRef(authority.ManifestRef, nameof(authority.ManifestRef));
        RequireExactReleaseRef(
            authority.ManifestRef,
            authority.ReleaseVersion,
            authority.AuthoritySnapshotSha256,
            "RELEASE_CHANNEL.json",
            nameof(authority.ManifestRef));
        RequireSha256(authority.ManifestSha256, nameof(authority.ManifestSha256));
        RequirePortableAuthorityRef(authority.ReleaseDecisionRef, nameof(authority.ReleaseDecisionRef));
        RequireExactReleaseRef(
            authority.ReleaseDecisionRef,
            authority.ReleaseVersion,
            authority.AuthoritySnapshotSha256,
            "RELEASE_DECISION.json",
            nameof(authority.ReleaseDecisionRef));
        RequireSha256(authority.ReleaseDecisionSha256, nameof(authority.ReleaseDecisionSha256));
        if (authority.ReleaseDecisionStatus is not (
                "review_required" or "preview_ready" or "stable_ready"))
        {
            throw new ArgumentException(
                "ReleaseDecisionStatus must be review_required, preview_ready, or stable_ready.",
                nameof(authority));
        }

        RequirePortableAuthorityRef(authority.ProvenanceRef, nameof(authority.ProvenanceRef));
        RequireSha256(authority.ProvenanceSha256, nameof(authority.ProvenanceSha256));
        RequireIdentifier(authority.AssetId, nameof(authority.AssetId));
        RequireSha256(authority.AssetContentSha256, nameof(authority.AssetContentSha256));
        RequireSha256(
            authority.MediaManifestCanonicalSha256,
            nameof(authority.MediaManifestCanonicalSha256));
        var expectedProvenanceRef =
            "release-evidence://media-factory/snapshots/"
            + $"{authority.ReleaseVersion}/{authority.AuthoritySnapshotSha256}/"
            + $"decisions/{authority.ReleaseDecisionSha256}/"
            + $"assets/{authority.AssetId}/{authority.AssetContentSha256}/"
            + $"manifests/{authority.MediaManifestCanonicalSha256}/"
            + $"provenance/{authority.ProvenanceSha256}.json";
        if (!string.Equals(authority.ProvenanceRef, expectedProvenanceRef, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "ProvenanceRef must be content-addressed beneath the governed snapshot and decision.",
                nameof(authority));
        }

        if (!string.Equals(
                eligibility.AuthoritySnapshotSha256,
                authority.AuthoritySnapshotSha256,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Public eligibility must bind the exact governed authority snapshot.",
                nameof(eligibility));
        }

        RequireIdentifier(eligibility.AssetId, nameof(eligibility.AssetId));
        RequireSha256(eligibility.AssetContentSha256, nameof(eligibility.AssetContentSha256));
        RequireSha256(
            eligibility.MediaManifestCanonicalSha256,
            nameof(eligibility.MediaManifestCanonicalSha256));
        var expectedAssetBinding = (
            manifest.AssetId,
            manifest.ContentHash,
            canonicalManifestSha256);
        if ((authority.AssetId, authority.AssetContentSha256, authority.MediaManifestCanonicalSha256)
                != expectedAssetBinding
            || (eligibility.AssetId, eligibility.AssetContentSha256, eligibility.MediaManifestCanonicalSha256)
                != expectedAssetBinding)
        {
            throw new ArgumentException(
                "Public eligibility and provenance must bind the exact asset id, content SHA-256, and canonical media-manifest SHA-256.",
                nameof(manifest));
        }

        return new PublicMediaAssetProjection(manifest, authority, eligibility);
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException($"{name} must be non-empty canonical text.", name);
        }
    }

    private static void RequireSha256(string value, string name) => RequireHex(value, 64, name);

    private static void RequireIdentifier(string value, string name)
    {
        RequireText(value, name);
        if (value.Length > 128
            || value is "." or ".."
            || value.Any(static character =>
                character is not (>= 'a' and <= 'z')
                    and not (>= 'A' and <= 'Z')
                    and not (>= '0' and <= '9')
                    and not ('.' or '_' or '+' or '-')))
        {
            throw new ArgumentException($"{name} must be one canonical identifier.", name);
        }
    }

    private static void RequireExactReleaseRef(
        string value,
        string releaseVersion,
        string snapshotSha256,
        string fileName,
        string name)
    {
        var expected =
            $"registry://release-evidence/snapshots/{releaseVersion}/{snapshotSha256}/{fileName}";
        if (!string.Equals(value, expected, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{name} must be the canonical release-evidence reference {expected}.",
                name);
        }
    }

    private static void ValidateLifecycle(
        MediaAssetManifest manifest,
        MediaPublicEligibility eligibility)
    {
        ArgumentNullException.ThrowIfNull(manifest.Lifecycle);
        var lifecycle = manifest.Lifecycle;
        RequireUtcTimestamp(lifecycle.CreatedAtUtc, nameof(lifecycle.CreatedAtUtc));
        if (lifecycle.ApprovalStatus != AssetApprovalStatus.Approved
            || lifecycle.ApprovedAtUtc is not { } approvedAtUtc
            || lifecycle.PersistedAtUtc is not { } persistedAtUtc
            || lifecycle.RejectedAtUtc is not null
            || lifecycle.PurgedAtUtc is not null)
        {
            throw new ArgumentException(
                "Public media requires an approved, persisted, non-rejected, non-purged lifecycle.",
                nameof(manifest));
        }

        RequireUtcTimestamp(approvedAtUtc, nameof(lifecycle.ApprovedAtUtc));
        RequireUtcTimestamp(persistedAtUtc, nameof(lifecycle.PersistedAtUtc));
        if (approvedAtUtc < lifecycle.CreatedAtUtc
            || persistedAtUtc < lifecycle.CreatedAtUtc
            || eligibility.CuratedAtUtc < approvedAtUtc
            || eligibility.CuratedAtUtc < persistedAtUtc
            || lifecycle.ExpiresAtUtc is { } expiresAtUtc
                && expiresAtUtc <= eligibility.CuratedAtUtc)
        {
            throw new ArgumentException(
                "Public media lifecycle and curation timestamps are incomplete, expired, or out of order.",
                nameof(manifest));
        }

        if (lifecycle.ExpiresAtUtc is { } expiration)
        {
            RequireUtcTimestamp(expiration, nameof(lifecycle.ExpiresAtUtc));
        }
    }

    private static void ValidateLineage(MediaAssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest.DerivedAssetIds);
        var derived = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assetId in manifest.DerivedAssetIds)
        {
            RequireText(assetId, nameof(manifest.DerivedAssetIds));
            if (!derived.Add(assetId) || string.Equals(assetId, manifest.AssetId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Derived asset ids must be unique and cannot contain the projected asset.",
                    nameof(manifest));
            }
        }

        if (manifest.PreviewAssetId is { } previewAssetId)
        {
            RequireText(previewAssetId, nameof(manifest.PreviewAssetId));
        }

        if (manifest.ParentAssetId is { } parentAssetId)
        {
            RequireText(parentAssetId, nameof(manifest.ParentAssetId));
            if (string.Equals(parentAssetId, manifest.AssetId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A public asset cannot be its own parent.",
                    nameof(manifest));
            }
        }
    }

    private static void RequireStorageObjectKey(string value)
    {
        RequireText(value, nameof(MediaAssetManifest.StorageObjectKey));
        if (value.StartsWith("/", StringComparison.Ordinal)
            || value.Contains('\\')
            || value.Any(char.IsControl)
            || value.Split('/').Any(static segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException(
                "StorageObjectKey must be a portable canonical object key without traversal.",
                nameof(MediaAssetManifest.StorageObjectKey));
        }
    }

    private static void RequireContentType(string value)
    {
        RequireText(value, nameof(MediaAssetManifest.ContentType));
        var slash = value.IndexOf('/');
        if (slash <= 0
            || slash == value.Length - 1
            || slash != value.LastIndexOf('/')
            || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "ContentType must be one canonical media type.",
                nameof(MediaAssetManifest.ContentType));
        }
    }

    private static void RequireUtcTimestamp(DateTimeOffset value, string name)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException($"{name} must be a non-default UTC timestamp.", name);
        }
    }

    private static void RequireReleaseVersion(string value)
    {
        RequireText(value, nameof(MediaReleaseAuthorityBinding.ReleaseVersion));
        if (value.Length > 128
            || value is "." or ".."
            || value.Any(static character =>
                character is not (>= 'a' and <= 'z')
                    and not (>= 'A' and <= 'Z')
                    and not (>= '0' and <= '9')
                    and not ('.' or '_' or '+' or '-')))
        {
            throw new ArgumentException(
                "ReleaseVersion must be one canonical Registry release identifier.",
                nameof(MediaReleaseAuthorityBinding.ReleaseVersion));
        }
    }

    private static void RequireHex(string value, int length, string name)
    {
        if (value is null || value.Length != length || value.Any(static character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                $"{name} must be an exact lowercase {length}-character hexadecimal digest.",
                name);
        }
    }

    private static void RequirePortableAuthorityRef(string value, string name)
    {
        RequireText(value, name);
        var normalizedValue = value.ToLowerInvariant();
        if (value.Contains('\\')
            || value.Any(char.IsControl)
            || normalizedValue.Contains("/../", StringComparison.Ordinal)
            || normalizedValue.Contains("/./", StringComparison.Ordinal)
            || normalizedValue.Contains("%2e", StringComparison.Ordinal)
            || normalizedValue.Contains("%2f", StringComparison.Ordinal)
            || normalizedValue.Contains("%5c", StringComparison.Ordinal)
            || ForbiddenLocalPathFragments.Any(fragment =>
                value.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            || !Uri.TryCreate(value, UriKind.Absolute, out var authorityUri)
            || authorityUri.Scheme is not ("https" or "registry" or "release-evidence" or "urn")
            || (authorityUri.Scheme is not "urn" && string.IsNullOrWhiteSpace(authorityUri.Host))
            || !string.IsNullOrEmpty(authorityUri.UserInfo)
            || !string.IsNullOrEmpty(authorityUri.Query)
            || !string.IsNullOrEmpty(authorityUri.Fragment)
            || authorityUri.Segments.Any(static segment => segment is "../" or "./" or ".." or "."))
        {
            throw new ArgumentException(
                $"{name} must be a portable immutable authority reference.",
                name);
        }
    }
}
