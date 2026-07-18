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
    string AuthoritySnapshotRef,
    string AuthoritySnapshotSha256,
    string ManifestRef,
    string ManifestSha256,
    string ReleaseDecisionRef,
    string ReleaseDecisionSha256,
    string ReleaseDecisionStatus,
    string ProvenanceRef,
    string ProvenanceSha256);

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
        MediaReleaseAuthorityBinding authority)
    {
        Manifest = manifest;
        Authority = authority;
    }

    /// <summary>The exact rendered-asset manifest being projected.</summary>
    public MediaAssetManifest Manifest { get; }

    /// <summary>The immutable Registry and provenance identity for the projection.</summary>
    public MediaReleaseAuthorityBinding Authority { get; }

    /// <summary>
    /// Creates a release-facing projection after validating portable immutable authority.
    /// </summary>
    public static PublicMediaAssetProjection Create(
        MediaAssetManifest manifest,
        MediaReleaseAuthorityBinding authority)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(authority);

        RequireText(manifest.AssetId, nameof(manifest.AssetId));
        if (manifest.ContentLengthBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(manifest.ContentLengthBytes),
                "Public media bytes must have a positive length.");
        }

        RequireSha256(manifest.ContentHash, nameof(manifest.ContentHash));
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
        RequirePortableAuthorityRef(authority.AuthoritySnapshotRef, nameof(authority.AuthoritySnapshotRef));
        RequireExactReleaseRef(
            authority.AuthoritySnapshotRef,
            authority.ReleaseVersion,
            "SNAPSHOT.json",
            nameof(authority.AuthoritySnapshotRef));
        RequireSha256(authority.AuthoritySnapshotSha256, nameof(authority.AuthoritySnapshotSha256));
        RequirePortableAuthorityRef(authority.ManifestRef, nameof(authority.ManifestRef));
        RequireExactReleaseRef(
            authority.ManifestRef,
            authority.ReleaseVersion,
            "RELEASE_CHANNEL.json",
            nameof(authority.ManifestRef));
        RequireSha256(authority.ManifestSha256, nameof(authority.ManifestSha256));
        RequirePortableAuthorityRef(authority.ReleaseDecisionRef, nameof(authority.ReleaseDecisionRef));
        RequireExactReleaseRef(
            authority.ReleaseDecisionRef,
            authority.ReleaseVersion,
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
        var expectedProvenancePrefix =
            $"release-evidence://release-evidence/{authority.ReleaseVersion}/provenance/";
        if (!authority.ProvenanceRef.StartsWith(expectedProvenancePrefix, StringComparison.Ordinal)
            || !authority.ProvenanceRef.EndsWith(".json", StringComparison.Ordinal)
            || authority.ProvenanceRef.Length == expectedProvenancePrefix.Length + ".json".Length)
        {
            throw new ArgumentException(
                "ProvenanceRef must name a release-scoped immutable provenance JSON record.",
                nameof(authority));
        }

        RequireSha256(authority.ProvenanceSha256, nameof(authority.ProvenanceSha256));

        return new PublicMediaAssetProjection(manifest, authority);
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException($"{name} must be non-empty canonical text.", name);
        }
    }

    private static void RequireSha256(string value, string name) => RequireHex(value, 64, name);

    private static void RequireExactReleaseRef(
        string value,
        string releaseVersion,
        string fileName,
        string name)
    {
        var expected = $"registry://release-evidence/{releaseVersion}/{fileName}";
        if (!string.Equals(value, expected, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{name} must be the canonical release-evidence reference {expected}.",
                name);
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
