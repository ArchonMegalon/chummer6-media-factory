using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chummer.Run.AI.Services.Assets;

public interface IInstallAwareConciergeBundleService
{
    Task<InstallAwareConciergeBundleReceipt> RenderAsync(
        InstallAwareConciergeRenderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class InstallAwareConciergeBundleService : IInstallAwareConciergeBundleService
{
    public const int MaxSiblingNotesPerArtifact = 2;

    private readonly IMediaRenderJobService _jobs;

    public InstallAwareConciergeBundleService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<InstallAwareConciergeBundleReceipt> RenderAsync(
        InstallAwareConciergeRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var receipts = new List<InstallAwareConciergeArtifactReceipt>(normalized.Artifacts.Count);
        DateTimeOffset? renderedAtUtc = null;

        foreach (var artifact in normalized.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enqueued = await _jobs.EnqueueAsync(
                new MediaRenderJobEnqueueRequest(
                    JobType: ToJobType(artifact.BundleKind, artifact.Role),
                    DeduplicationKey: BuildScopedDeduplicationKey(normalized, artifact),
                    Category: artifact.Category,
                    Payload: artifact.Payload,
                    Source: normalized.Source,
                    CacheTtl: artifact.CacheTtl,
                    MaxBytes: artifact.MaxBytes,
                    RequiresApproval: artifact.RequiresApproval,
                    PersistOnApproval: artifact.PersistOnApproval,
                    AllowPersistentPinning: artifact.AllowPersistentPinning),
                cancellationToken);
            var status = await WaitForTerminalStatusAsync(enqueued.JobId, cancellationToken);
            ValidateReceiptStatus(status);
            var jobRenderedAtUtc = status.CompletedAtUtc ?? status.CreatedAtUtc;
            renderedAtUtc = renderedAtUtc is { } currentRenderedAtUtc
                ? MaxTimestamp(currentRenderedAtUtc, jobRenderedAtUtc)
                : jobRenderedAtUtc;

            receipts.Add(new InstallAwareConciergeArtifactReceipt(
                ReceiptId: BuildReceiptId(normalized, artifact),
                BundleKind: artifact.BundleKind,
                Role: artifact.Role,
                Category: artifact.Category,
                OutputFormat: artifact.OutputFormat,
                CompanionRef: artifact.CompanionRef,
                CaptionRefs: artifact.CaptionRefs,
                PreviewRefs: artifact.PreviewRefs,
                SiblingNoteRefs: artifact.SiblingNoteRefs,
                JobId: status.JobId,
                JobState: status.State,
                AssetId: status.AssetId,
                AssetUrl: status.AssetUrl,
                CacheTtl: status.CacheTtl,
                ApprovalState: status.ApprovalState,
                RetentionState: status.RetentionState,
                StorageClass: status.StorageClass));
        }

        return new InstallAwareConciergeBundleReceipt(
            RenderingId: normalized.RenderingId,
            InstallAwarePacketId: normalized.InstallAwarePacketId,
            InstalledBuildReceiptId: normalized.InstalledBuildReceiptId,
            ArtifactIdentityId: normalized.ArtifactIdentityId,
            Source: normalized.Source,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: renderedAtUtc ?? normalized.RequestedAtUtc,
            Artifacts: receipts,
            ReleaseExplainerReceiptIds: ReceiptIdsFor(receipts, InstallAwareConciergeBundleKind.ReleaseExplainer),
            SupportClosureReceiptIds: ReceiptIdsFor(receipts, InstallAwareConciergeBundleKind.SupportClosure),
            PublicConciergeReceiptIds: ReceiptIdsFor(receipts, InstallAwareConciergeBundleKind.PublicConcierge),
            JobIds: receipts
                .Select(static receipt => receipt.JobId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static jobId => jobId, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            CompanionRefs: OrderedDistinct(receipts.Select(static receipt => receipt.CompanionRef)),
            CompanionReadyRefs: BuildCompanionReadyRefs(receipts),
            CaptionRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.CaptionRefs)),
            PreviewRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.PreviewRefs)),
            SiblingNoteRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.SiblingNoteRefs)),
            BundleReceiptGroups: BuildBundleReceiptGroups(receipts),
            RoleReceiptGroups: BuildRoleReceiptGroups(receipts),
            CompanionRefReceipts: BuildCompanionRefReceipts(receipts),
            CaptionRefReceipts: BuildCaptionRefReceipts(receipts),
            PreviewRefReceipts: BuildPreviewRefReceipts(receipts),
            SiblingNoteReceipts: BuildSiblingNoteReceipts(receipts));
    }

    private static InstallAwareConciergeRenderRequest Normalize(InstallAwareConciergeRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RenderingId, nameof(request.RenderingId));
        RequireText(request.InstallAwarePacketId, nameof(request.InstallAwarePacketId));
        RequireText(request.InstalledBuildReceiptId, nameof(request.InstalledBuildReceiptId));
        RequireText(request.ArtifactIdentityId, nameof(request.ArtifactIdentityId));
        RequireText(request.Source, nameof(request.Source));
        if (request.Artifacts.Count == 0)
        {
            throw new ArgumentException("At least one install-aware concierge artifact is required.", nameof(request));
        }

        var renderingId = request.RenderingId.Trim();
        var installAwarePacketId = request.InstallAwarePacketId.Trim();
        var installedBuildReceiptId = request.InstalledBuildReceiptId.Trim();
        var artifactIdentityId = request.ArtifactIdentityId.Trim();
        var source = request.Source.Trim();
        var artifacts = request.Artifacts.Select(NormalizeArtifact).ToArray();
        var normalizedRequest = request with
        {
            RenderingId = renderingId,
            InstallAwarePacketId = installAwarePacketId,
            InstalledBuildReceiptId = installedBuildReceiptId,
            ArtifactIdentityId = artifactIdentityId,
            Source = source,
        };

        RequirePayloadScope(artifacts, normalizedRequest);
        RequireBundleRoles(artifacts, InstallAwareConciergeBundleKind.ReleaseExplainer, normalizedRequest);
        RequireBundleRoles(artifacts, InstallAwareConciergeBundleKind.SupportClosure, normalizedRequest);
        RequireBundleRoles(artifacts, InstallAwareConciergeBundleKind.PublicConcierge, normalizedRequest);
        RequireUniqueCompanionRefs(artifacts);

        return normalizedRequest with
        {
            Artifacts = artifacts
                .OrderBy(static artifact => artifact.BundleKind)
                .ThenBy(static artifact => artifact.Role)
                .ThenBy(static artifact => artifact.CompanionRef, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.OutputFormat, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.DeduplicationKey, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static InstallAwareConciergeArtifactRenderRequest NormalizeArtifact(InstallAwareConciergeArtifactRenderRequest artifact)
    {
        RequireText(artifact.Category, nameof(artifact.Category));
        RequireText(artifact.Payload, nameof(artifact.Payload));
        RequireText(artifact.OutputFormat, nameof(artifact.OutputFormat));
        RequireText(artifact.CompanionRef, nameof(artifact.CompanionRef));
        RequireText(artifact.DeduplicationKey, nameof(artifact.DeduplicationKey));

        var captionRefs = NormalizeRefs(artifact.CaptionRefs, nameof(artifact.CaptionRefs));
        var previewRefs = NormalizeRefs(artifact.PreviewRefs, nameof(artifact.PreviewRefs));
        var siblingNoteRefs = NormalizeRefs(artifact.SiblingNoteRefs, nameof(artifact.SiblingNoteRefs));

        if (artifact.Role is InstallAwareConciergeArtifactRole.Video or InstallAwareConciergeArtifactRole.Audio && captionRefs.Count == 0)
        {
            throw new ArgumentException("Install-aware concierge video and audio companions require at least one caption ref.", nameof(artifact));
        }

        if (artifact.Role is InstallAwareConciergeArtifactRole.Video or InstallAwareConciergeArtifactRole.PreviewCard && previewRefs.Count == 0)
        {
            throw new ArgumentException("Install-aware concierge video and preview-card companions require at least one preview ref.", nameof(artifact));
        }

        if (siblingNoteRefs.Count == 0)
        {
            throw new ArgumentException("Install-aware concierge companions require at least one sibling note ref.", nameof(artifact));
        }

        if (siblingNoteRefs.Count > MaxSiblingNotesPerArtifact)
        {
            throw new ArgumentException(
                $"Install-aware concierge sibling notes stay bounded to at most {MaxSiblingNotesPerArtifact} refs per artifact.",
                nameof(artifact));
        }

        return artifact with
        {
            Category = artifact.Category.Trim(),
            Payload = artifact.Payload.Trim(),
            OutputFormat = artifact.OutputFormat.Trim(),
            CompanionRef = artifact.CompanionRef.Trim(),
            CaptionRefs = captionRefs,
            PreviewRefs = previewRefs,
            SiblingNoteRefs = siblingNoteRefs,
            DeduplicationKey = artifact.DeduplicationKey.Trim()
        };
    }

    private static void RequireBundleRoles(
        IReadOnlyCollection<InstallAwareConciergeArtifactRenderRequest> artifacts,
        InstallAwareConciergeBundleKind bundleKind,
        InstallAwareConciergeRenderRequest request)
    {
        foreach (var role in Enum.GetValues<InstallAwareConciergeArtifactRole>())
        {
            if (!artifacts.Any(artifact => artifact.BundleKind == bundleKind && artifact.Role == role))
            {
                throw new ArgumentException(
                    $"Install-aware concierge {bundleKind} bundles require at least one {role} artifact.",
                    nameof(request));
            }
        }
    }

    private static void RequireUniqueCompanionRefs(IReadOnlyCollection<InstallAwareConciergeArtifactRenderRequest> artifacts)
    {
        var duplicate = artifacts
            .GroupBy(static artifact => artifact.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Install-aware concierge companion refs must be unique per install-aware packet: {duplicate.Key}.",
                nameof(InstallAwareConciergeRenderRequest.Artifacts));
        }
    }

    private static void RequirePayloadScope(
        IReadOnlyCollection<InstallAwareConciergeArtifactRenderRequest> artifacts,
        InstallAwareConciergeRenderRequest request)
    {
        var missingPacketId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesInstallAwarePacketId(artifact.Payload, request.InstallAwarePacketId));
        if (missingPacketId is not null)
        {
            throw new ArgumentException(
                "Install-aware concierge payloads must stay scoped to the install-aware packet id.",
                nameof(InstallAwareConciergeRenderRequest.Artifacts));
        }

        var missingInstalledBuildReceiptId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesInstalledBuildReceiptId(artifact.Payload, request.InstalledBuildReceiptId));
        if (missingInstalledBuildReceiptId is not null)
        {
            throw new ArgumentException(
                "Install-aware concierge payloads must stay scoped to the installed build receipt id.",
                nameof(InstallAwareConciergeRenderRequest.Artifacts));
        }

        var missingArtifactIdentityId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesArtifactIdentityId(artifact.Payload, request.ArtifactIdentityId));
        if (missingArtifactIdentityId is not null)
        {
            throw new ArgumentException(
                "Install-aware concierge payloads must stay scoped to the artifact identity id.",
                nameof(InstallAwareConciergeRenderRequest.Artifacts));
        }
    }

    private static bool PayloadMatchesInstallAwarePacketId(string payload, string installAwarePacketId)
    {
        if (TryParseScopeFromJsonPayload(
            payload,
            out var scopedInstallAwarePacketId,
            out _,
            out _,
            out var parsedJsonPayload))
        {
            return string.Equals(scopedInstallAwarePacketId, installAwarePacketId, StringComparison.Ordinal);
        }

        if (parsedJsonPayload)
        {
            return false;
        }

        if (TryParseScopeFromTextPayload(payload, "installAwarePacketId", out scopedInstallAwarePacketId))
        {
            return string.Equals(scopedInstallAwarePacketId, installAwarePacketId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, installAwarePacketId);
    }

    private static bool PayloadMatchesInstalledBuildReceiptId(string payload, string installedBuildReceiptId)
    {
        if (TryParseScopeFromJsonPayload(
            payload,
            out _,
            out var scopedInstalledBuildReceiptId,
            out _,
            out var parsedJsonPayload))
        {
            return string.Equals(scopedInstalledBuildReceiptId, installedBuildReceiptId, StringComparison.Ordinal);
        }

        if (parsedJsonPayload)
        {
            return false;
        }

        if (TryParseScopeFromTextPayload(payload, "installedBuildReceiptId", out scopedInstalledBuildReceiptId))
        {
            return string.Equals(scopedInstalledBuildReceiptId, installedBuildReceiptId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, installedBuildReceiptId);
    }

    private static bool PayloadMatchesArtifactIdentityId(string payload, string artifactIdentityId)
    {
        if (TryParseScopeFromJsonPayload(
            payload,
            out _,
            out _,
            out var scopedArtifactIdentityId,
            out var parsedJsonPayload))
        {
            return string.Equals(scopedArtifactIdentityId, artifactIdentityId, StringComparison.Ordinal);
        }

        if (parsedJsonPayload)
        {
            return false;
        }

        if (TryParseScopeFromTextPayload(payload, "artifactIdentityId", out scopedArtifactIdentityId))
        {
            return string.Equals(scopedArtifactIdentityId, artifactIdentityId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, artifactIdentityId);
    }

    private static bool TryParseScopeFromJsonPayload(
        string payload,
        out string installAwarePacketId,
        out string installedBuildReceiptId,
        out string artifactIdentityId,
        out bool parsedJsonPayload)
    {
        installAwarePacketId = string.Empty;
        installedBuildReceiptId = string.Empty;
        artifactIdentityId = string.Empty;
        parsedJsonPayload = false;

        try
        {
            using var document = JsonDocument.Parse(payload);
            parsedJsonPayload = true;
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetJsonStringProperty(document.RootElement, "installAwarePacketId", out installAwarePacketId) ||
                !TryGetJsonStringProperty(document.RootElement, "installedBuildReceiptId", out installedBuildReceiptId) ||
                !TryGetJsonStringProperty(document.RootElement, "artifactIdentityId", out artifactIdentityId))
            {
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetJsonStringProperty(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is JsonValueKind.String)
        {
            value = (property.GetString() ?? string.Empty).Trim();
            return value.Length > 0;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                candidate.Value.ValueKind is JsonValueKind.String)
            {
                value = (candidate.Value.GetString() ?? string.Empty).Trim();
                return value.Length > 0;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryParseScopeFromTextPayload(
        string payload,
        string propertyName,
        out string value)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(propertyName);

        var pattern = $@"(?<![A-Za-z0-9_\-]){Regex.Escape(propertyName)}\s*[:=]\s*(?:""(?<value>[^""]+)""|'(?<value>[^']+)'|(?<value>[^\s,;|&]+))";
        var match = Regex.Match(payload, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            value = match.Groups["value"].Value.Trim();
            return value.Length > 0;
        }

        value = string.Empty;
        return false;
    }

    private static bool ContainsDelimitedScopeValue(string payload, string expected)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(expected);

        var searchIndex = 0;
        while (searchIndex < payload.Length)
        {
            var matchIndex = payload.IndexOf(expected, searchIndex, StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                return false;
            }

            var beforeIndex = matchIndex - 1;
            var afterIndex = matchIndex + expected.Length;
            if (IsScopeDelimiter(payload, beforeIndex) && IsScopeDelimiter(payload, afterIndex))
            {
                return true;
            }

            searchIndex = matchIndex + expected.Length;
        }

        return false;
    }

    private static bool IsScopeDelimiter(string payload, int index)
    {
        if (index < 0 || index >= payload.Length)
        {
            return true;
        }

        return !IsScopeTokenCharacter(payload[index]);
    }

    private static bool IsScopeTokenCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '-' or '_' or '/' or '.' or ':';

    private static IReadOnlyList<InstallAwareConciergeRoleReceiptGroup> BuildRoleReceiptGroups(
        IEnumerable<InstallAwareConciergeArtifactReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => (receipt.BundleKind, receipt.Role))
            .OrderBy(static group => group.Key.BundleKind)
            .ThenBy(static group => group.Key.Role)
            .Select(static group =>
            {
                var rows = group
                    .OrderBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new InstallAwareConciergeRoleReceiptGroup(
                    BundleKind: group.Key.BundleKind,
                    Role: group.Key.Role,
                    ReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.ReceiptId)),
                    JobIds: OrderedDistinct(rows.Select(static receipt => receipt.JobId)),
                    CompanionRefs: OrderedDistinct(rows.Select(static receipt => receipt.CompanionRef)),
                    CaptionRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.CaptionRefs)),
                    PreviewRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.PreviewRefs)),
                    SiblingNoteRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.SiblingNoteRefs)),
                    ArtifactReceipts: BuildGroupedArtifactReceipts(rows));
            })
            .ToArray();

    private static IReadOnlyList<InstallAwareConciergeBundleReceiptGroup> BuildBundleReceiptGroups(
        IEnumerable<InstallAwareConciergeArtifactReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => receipt.BundleKind)
            .OrderBy(static group => group.Key)
            .Select(static group =>
            {
                var rows = group
                    .OrderBy(static receipt => receipt.Role)
                    .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new InstallAwareConciergeBundleReceiptGroup(
                    BundleKind: group.Key,
                    ReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.ReceiptId)),
                    JobIds: OrderedDistinct(rows.Select(static receipt => receipt.JobId)),
                    CompanionRefs: OrderedDistinct(rows.Select(static receipt => receipt.CompanionRef)),
                    CaptionRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.CaptionRefs)),
                    PreviewRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.PreviewRefs)),
                    SiblingNoteRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.SiblingNoteRefs)),
                    Roles: rows
                        .Select(static receipt => receipt.Role)
                        .Distinct()
                        .OrderBy(static role => role)
                        .ToArray(),
                    ArtifactReceipts: BuildGroupedArtifactReceipts(rows));
            })
            .ToArray();

    private static IReadOnlyList<string> NormalizeRefs(IReadOnlyList<string> refs, string name)
    {
        ArgumentNullException.ThrowIfNull(refs, name);
        return OrderedDistinct(
            refs
                .Select(static value => value?.Trim() ?? string.Empty)
                .Where(static value => value.Length > 0));
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }
    }

    private static MediaRenderJobType ToJobType(
        InstallAwareConciergeBundleKind bundleKind,
        InstallAwareConciergeArtifactRole role) =>
        (bundleKind, role) switch
        {
            (InstallAwareConciergeBundleKind.ReleaseExplainer, InstallAwareConciergeArtifactRole.Video) => MediaRenderJobType.InstallAwareReleaseExplainerVideo,
            (InstallAwareConciergeBundleKind.ReleaseExplainer, InstallAwareConciergeArtifactRole.Audio) => MediaRenderJobType.InstallAwareReleaseExplainerAudio,
            (InstallAwareConciergeBundleKind.ReleaseExplainer, InstallAwareConciergeArtifactRole.PreviewCard) => MediaRenderJobType.InstallAwareReleaseExplainerPreviewCard,
            (InstallAwareConciergeBundleKind.SupportClosure, InstallAwareConciergeArtifactRole.Video) => MediaRenderJobType.InstallAwareSupportClosureVideo,
            (InstallAwareConciergeBundleKind.SupportClosure, InstallAwareConciergeArtifactRole.Audio) => MediaRenderJobType.InstallAwareSupportClosureAudio,
            (InstallAwareConciergeBundleKind.SupportClosure, InstallAwareConciergeArtifactRole.PreviewCard) => MediaRenderJobType.InstallAwareSupportClosurePreviewCard,
            (InstallAwareConciergeBundleKind.PublicConcierge, InstallAwareConciergeArtifactRole.Video) => MediaRenderJobType.InstallAwarePublicConciergeVideo,
            (InstallAwareConciergeBundleKind.PublicConcierge, InstallAwareConciergeArtifactRole.Audio) => MediaRenderJobType.InstallAwarePublicConciergeAudio,
            (InstallAwareConciergeBundleKind.PublicConcierge, InstallAwareConciergeArtifactRole.PreviewCard) => MediaRenderJobType.InstallAwarePublicConciergePreviewCard,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported install-aware concierge artifact role.")
        };

    private static string BuildScopedDeduplicationKey(
        InstallAwareConciergeRenderRequest request,
        InstallAwareConciergeArtifactRenderRequest artifact)
    {
        // Keep replay identity scoped to install/build/artifact truth plus the sibling itself.
        // Source and requested timestamps are metadata and must not fork stable concierge jobs.
        var fields = new[]
        {
            "install-aware-concierge",
            request.InstallAwarePacketId,
            request.InstalledBuildReceiptId,
            request.ArtifactIdentityId,
            request.RenderingId,
            artifact.BundleKind.ToString(),
            artifact.Role.ToString(),
            artifact.Category,
            artifact.OutputFormat,
            artifact.CompanionRef,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "install-aware-concierge:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildReceiptId(
        InstallAwareConciergeRenderRequest request,
        InstallAwareConciergeArtifactRenderRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(request, artifact),
            BuildRefHashSegment("caption", artifact.CaptionRefs),
            BuildRefHashSegment("preview", artifact.PreviewRefs),
            BuildRefHashSegment("sibling-note", artifact.SiblingNoteRefs));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "install_aware_concierge_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildRefHashSegment(string prefix, IReadOnlyList<string> refs) =>
        string.Join(
            "\n",
            new[] { $"{prefix}:{refs.Count}" }
                .Concat(refs.Select(static value => $"{value.Length}:{value}")));

    private static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> values) =>
        values
            .GroupBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderBy(static value => value, StringComparer.Ordinal).First())
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> ReceiptIdsFor(
        IEnumerable<InstallAwareConciergeArtifactReceipt> receipts,
        InstallAwareConciergeBundleKind bundleKind) =>
        receipts
            .Where(receipt => receipt.BundleKind == bundleKind)
            .Select(static receipt => receipt.ReceiptId)
            .OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<InstallAwareConciergeCompanionRefReceipt> BuildCompanionRefReceipts(
        IEnumerable<InstallAwareConciergeArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.BundleKind)
            .ThenBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new InstallAwareConciergeCompanionRefReceipt(
                Ref: receipt.CompanionRef,
                BundleKind: receipt.BundleKind,
                Role: receipt.Role,
                Category: receipt.Category,
                ReceiptId: receipt.ReceiptId,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                SiblingNoteRefs: receipt.SiblingNoteRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private static IReadOnlyList<InstallAwareConciergeCompanionReadyRef> BuildCompanionReadyRefs(
        IEnumerable<InstallAwareConciergeArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.BundleKind)
            .ThenBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new InstallAwareConciergeCompanionReadyRef(
                Ref: receipt.CompanionRef,
                BundleKind: receipt.BundleKind,
                Role: receipt.Role,
                Category: receipt.Category,
                ReceiptId: receipt.ReceiptId,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                SiblingNoteRefs: receipt.SiblingNoteRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private static IReadOnlyList<InstallAwareConciergeCaptionRefReceipt> BuildCaptionRefReceipts(
        IEnumerable<InstallAwareConciergeArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.CaptionRefs.Select(captionRef => (captionRef, receipt)))
            .GroupBy(static item => item.captionRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new InstallAwareConciergeCaptionRefReceipt(
                Ref: CanonicalizeGroupedRef(group.Select(static item => item.captionRef)),
                ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId)),
                JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId)),
                CompanionRefs: OrderedDistinct(group.Select(static item => item.receipt.CompanionRef)),
                BundleKinds: group.Select(static item => item.receipt.BundleKind).Distinct().OrderBy(static kind => kind).ToArray(),
                Roles: group.Select(static item => item.receipt.Role).Distinct().OrderBy(static role => role).ToArray(),
                ArtifactReceipts: BuildGroupedArtifactReceipts(group.Select(static item => item.receipt))))
            .ToArray();

    private static IReadOnlyList<InstallAwareConciergePreviewRefReceipt> BuildPreviewRefReceipts(
        IEnumerable<InstallAwareConciergeArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.PreviewRefs.Select(previewRef => (previewRef, receipt)))
            .GroupBy(static item => item.previewRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new InstallAwareConciergePreviewRefReceipt(
                Ref: CanonicalizeGroupedRef(group.Select(static item => item.previewRef)),
                ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId)),
                JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId)),
                CompanionRefs: OrderedDistinct(group.Select(static item => item.receipt.CompanionRef)),
                BundleKinds: group.Select(static item => item.receipt.BundleKind).Distinct().OrderBy(static kind => kind).ToArray(),
                Roles: group.Select(static item => item.receipt.Role).Distinct().OrderBy(static role => role).ToArray(),
                ArtifactReceipts: BuildGroupedArtifactReceipts(group.Select(static item => item.receipt))))
            .ToArray();

    private static IReadOnlyList<InstallAwareConciergeSiblingNoteReceipt> BuildSiblingNoteReceipts(
        IEnumerable<InstallAwareConciergeArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.SiblingNoteRefs.Select(siblingNoteRef => (siblingNoteRef, receipt)))
            .GroupBy(static item => item.siblingNoteRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new InstallAwareConciergeSiblingNoteReceipt(
                Ref: CanonicalizeGroupedRef(group.Select(static item => item.siblingNoteRef)),
                ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId)),
                JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId)),
                CompanionRefs: OrderedDistinct(group.Select(static item => item.receipt.CompanionRef)),
                BundleKinds: group.Select(static item => item.receipt.BundleKind).Distinct().OrderBy(static kind => kind).ToArray(),
                Roles: group.Select(static item => item.receipt.Role).Distinct().OrderBy(static role => role).ToArray(),
                ArtifactReceipts: BuildGroupedArtifactReceipts(group.Select(static item => item.receipt))))
            .ToArray();

    private static string CanonicalizeGroupedRef(IEnumerable<string> refs) =>
        OrderedDistinct(refs).First();

    private static IReadOnlyList<InstallAwareConciergeGroupedArtifactReceipt> BuildGroupedArtifactReceipts(
        IEnumerable<InstallAwareConciergeArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.BundleKind)
            .ThenBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new InstallAwareConciergeGroupedArtifactReceipt(
                ReceiptId: receipt.ReceiptId,
                BundleKind: receipt.BundleKind,
                Role: receipt.Role,
                Category: receipt.Category,
                CompanionRef: receipt.CompanionRef,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                SiblingNoteRefs: receipt.SiblingNoteRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private async Task<MediaRenderJobStatus> WaitForTerminalStatusAsync(
        string jobId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = _jobs.Get(jobId);
            if (status is null)
            {
                throw new InvalidOperationException($"Install-aware concierge job {jobId} disappeared before receipt emission.");
            }

            if (status.State is MediaRenderJobState.Succeeded)
            {
                return status;
            }

            if (status.State is MediaRenderJobState.Failed or MediaRenderJobState.Expired)
            {
                throw new InvalidOperationException($"Install-aware concierge job {jobId} ended as {status.State}: {status.Error ?? "unknown"}");
            }

            await Task.Delay(20, cancellationToken);
        }

        throw new TimeoutException($"Install-aware concierge job {jobId} did not finish before receipt emission.");
    }

    private static void ValidateReceiptStatus(MediaRenderJobStatus status)
    {
        if (string.IsNullOrWhiteSpace(status.AssetId) ||
            string.IsNullOrWhiteSpace(status.AssetUrl) ||
            status.ApprovalState is null ||
            status.RetentionState is null ||
            status.StorageClass is null)
        {
            throw new InvalidOperationException(
                $"Install-aware concierge job {status.JobId} succeeded without asset and lifecycle truth required for receipt emission.");
        }
    }

    private static DateTimeOffset MaxTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;
}
