using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chummer.Run.AI.Services.Assets;

public interface IBuildExplainCompanionRenderingService
{
    Task<BuildExplainCompanionRenderReceipt> RenderAsync(
        BuildExplainCompanionRenderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class BuildExplainCompanionRenderingService : IBuildExplainCompanionRenderingService
{
    private readonly IMediaRenderJobService _jobs;

    public BuildExplainCompanionRenderingService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<BuildExplainCompanionRenderReceipt> RenderAsync(
        BuildExplainCompanionRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        // This render-only, render-verified lane emits build explain sibling receipts without mutating engine truth.
        var normalized = Normalize(request);
        var receipts = new List<BuildExplainCompanionArtifactReceipt>(normalized.Artifacts.Count);
        DateTimeOffset? renderedAtUtc = null;

        foreach (var artifact in normalized.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enqueued = await _jobs.EnqueueAsync(
                new MediaRenderJobEnqueueRequest(
                    JobType: ToJobType(artifact.Role),
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

            receipts.Add(new BuildExplainCompanionArtifactReceipt(
                ReceiptId: BuildReceiptId(normalized, artifact),
                Role: artifact.Role,
                Category: artifact.Category,
                OutputFormat: artifact.OutputFormat,
                CompanionRef: artifact.CompanionRef,
                CaptionRefs: artifact.CaptionRefs,
                PreviewRefs: artifact.PreviewRefs,
                JobId: status.JobId,
                JobState: status.State,
                AssetId: status.AssetId,
                AssetUrl: status.AssetUrl,
                CacheTtl: status.CacheTtl,
                ApprovalState: status.ApprovalState,
                RetentionState: status.RetentionState,
                StorageClass: status.StorageClass));
        }

        return new BuildExplainCompanionRenderReceipt(
            RenderingId: normalized.RenderingId,
            ApprovedExplainPacketId: normalized.ApprovedExplainPacketId,
            ExplainPacketRevisionId: normalized.ExplainPacketRevisionId,
            Source: normalized.Source,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: renderedAtUtc ?? normalized.RequestedAtUtc,
            Artifacts: receipts,
            VideoReceiptIds: ReceiptIdsFor(receipts, BuildExplainCompanionArtifactRole.Video),
            AudioReceiptIds: ReceiptIdsFor(receipts, BuildExplainCompanionArtifactRole.Audio),
            PreviewCardReceiptIds: ReceiptIdsFor(receipts, BuildExplainCompanionArtifactRole.PreviewCard),
            PacketCompanionReceiptIds: ReceiptIdsFor(receipts, BuildExplainCompanionArtifactRole.PacketCompanion),
            JobIds: receipts
                .Select(static receipt => receipt.JobId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static jobId => jobId, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            CompanionRefs: OrderedDistinct(receipts.Select(static receipt => receipt.CompanionRef)),
            CompanionReadyRefs: BuildCompanionReadyRefs(receipts),
            CaptionRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.CaptionRefs)),
            PreviewRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.PreviewRefs)),
            RoleReceiptGroups: BuildRoleReceiptGroups(receipts),
            CompanionRefReceipts: BuildCompanionRefReceipts(receipts),
            CaptionRefReceipts: BuildCaptionRefReceipts(receipts),
            PreviewRefReceipts: BuildPreviewRefReceipts(receipts));
    }

    private static BuildExplainCompanionRenderRequest Normalize(BuildExplainCompanionRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RenderingId, nameof(request.RenderingId));
        RequireText(request.ApprovedExplainPacketId, nameof(request.ApprovedExplainPacketId));
        RequireText(request.ExplainPacketRevisionId, nameof(request.ExplainPacketRevisionId));
        RequireText(request.Source, nameof(request.Source));
        if (request.Artifacts is null)
        {
            throw new ArgumentNullException(nameof(BuildExplainCompanionRenderRequest.Artifacts));
        }
        if (request.Artifacts.Count == 0)
        {
            throw new ArgumentException("At least one build explain companion artifact is required.", nameof(request));
        }

        var artifacts = request.Artifacts
            .Select((artifact, index) => NormalizeArtifact(artifact, index))
            .ToArray();
        var renderingId = request.RenderingId.Trim();
        var approvedExplainPacketId = request.ApprovedExplainPacketId.Trim();
        var explainPacketRevisionId = request.ExplainPacketRevisionId.Trim();
        var source = request.Source.Trim();
        var normalizedRequest = request with
        {
            RenderingId = renderingId,
            ApprovedExplainPacketId = approvedExplainPacketId,
            ExplainPacketRevisionId = explainPacketRevisionId,
            Source = source,
        };

        RequirePayloadScope(artifacts, normalizedRequest);
        RequireRole(artifacts, BuildExplainCompanionArtifactRole.Video, request);
        RequireRole(artifacts, BuildExplainCompanionArtifactRole.Audio, request);
        RequireRole(artifacts, BuildExplainCompanionArtifactRole.PreviewCard, request);
        RequireRole(artifacts, BuildExplainCompanionArtifactRole.PacketCompanion, request);
        RequireUniqueCompanionRefs(artifacts);

        var orderedArtifacts = artifacts
            .OrderBy(static artifact => artifact.Role)
            .ThenBy(static artifact => artifact.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static artifact => artifact.OutputFormat, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static artifact => artifact.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static artifact => artifact.DeduplicationKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalizedRequest with
        {
            Artifacts = orderedArtifacts
        };
    }

    private static BuildExplainCompanionArtifactRenderRequest NormalizeArtifact(
        BuildExplainCompanionArtifactRenderRequest? artifact,
        int index)
    {
        if (artifact is null)
        {
            throw new ArgumentException(
                $"Build explain companion artifacts[{index}] is required.",
                nameof(BuildExplainCompanionRenderRequest.Artifacts));
        }

        RequireText(artifact.Category, nameof(artifact.Category));
        RequireText(artifact.Payload, nameof(artifact.Payload));
        RequireText(artifact.OutputFormat, nameof(artifact.OutputFormat));
        RequireText(artifact.CompanionRef, nameof(artifact.CompanionRef));
        RequireText(artifact.DeduplicationKey, nameof(artifact.DeduplicationKey));

        var captionRefs = NormalizeRefs(artifact.CaptionRefs, nameof(artifact.CaptionRefs));
        var previewRefs = NormalizeRefs(artifact.PreviewRefs, nameof(artifact.PreviewRefs));
        if (artifact.Role is BuildExplainCompanionArtifactRole.Video or BuildExplainCompanionArtifactRole.Audio && captionRefs.Count == 0)
        {
            throw new ArgumentException("Build explain video and audio companions require at least one caption ref.", nameof(artifact));
        }

        if (artifact.Role is BuildExplainCompanionArtifactRole.Video or BuildExplainCompanionArtifactRole.PreviewCard or BuildExplainCompanionArtifactRole.PacketCompanion && previewRefs.Count == 0)
        {
            throw new ArgumentException("Build explain video, preview-card, and packet companions require at least one preview ref.", nameof(artifact));
        }

        return artifact with
        {
            Category = artifact.Category.Trim(),
            Payload = artifact.Payload.Trim(),
            OutputFormat = artifact.OutputFormat.Trim(),
            CompanionRef = artifact.CompanionRef.Trim(),
            CaptionRefs = captionRefs,
            PreviewRefs = previewRefs,
            DeduplicationKey = artifact.DeduplicationKey.Trim()
        };
    }

    private static void RequireRole(
        IReadOnlyCollection<BuildExplainCompanionArtifactRenderRequest> artifacts,
        BuildExplainCompanionArtifactRole role,
        BuildExplainCompanionRenderRequest request)
    {
        if (!artifacts.Any(artifact => artifact.Role == role))
        {
            throw new ArgumentException($"Build explain companion rendering requires at least one {role} artifact.", nameof(request));
        }
    }

    private static void RequireUniqueCompanionRefs(IReadOnlyCollection<BuildExplainCompanionArtifactRenderRequest> artifacts)
    {
        var duplicate = artifacts
            .GroupBy(static artifact => artifact.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Build explain companion refs must be unique per approved explain packet: {duplicate.Key}.",
                nameof(BuildExplainCompanionRenderRequest.Artifacts));
        }
    }

    private static void RequirePayloadScope(
        IReadOnlyCollection<BuildExplainCompanionArtifactRenderRequest> artifacts,
        BuildExplainCompanionRenderRequest request)
    {
        var missingApprovedPacketId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesApprovedPacketId(
                artifact.Payload,
                request.ApprovedExplainPacketId));
        if (missingApprovedPacketId is not null)
        {
            throw new ArgumentException(
                "Build explain companion payloads must stay scoped to the approved explain packet id.",
                nameof(BuildExplainCompanionRenderRequest.Artifacts));
        }

        var missingRevisionId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesRevisionId(
                artifact.Payload,
                request.ExplainPacketRevisionId));
        if (missingRevisionId is not null)
        {
            throw new ArgumentException(
                "Build explain companion payloads must stay scoped to the explain packet revision id.",
                nameof(BuildExplainCompanionRenderRequest.Artifacts));
        }
    }

    private static bool PayloadMatchesApprovedPacketId(
        string payload,
        string approvedExplainPacketId)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.ApprovedExplainPacketId, approvedExplainPacketId, StringComparison.Ordinal);
        }

        string scopedApprovedPacketId;
        if (TryParseScopeFromTextPayload(payload, "approvedExplainPacketId", out scopedApprovedPacketId))
        {
            return string.Equals(scopedApprovedPacketId, approvedExplainPacketId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, approvedExplainPacketId);
    }

    private static bool PayloadMatchesRevisionId(
        string payload,
        string explainPacketRevisionId)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.ExplainPacketRevisionId, explainPacketRevisionId, StringComparison.Ordinal);
        }

        string scopedRevisionId;
        if (TryParseScopeFromTextPayload(payload, "explainPacketRevisionId", out scopedRevisionId))
        {
            return string.Equals(scopedRevisionId, explainPacketRevisionId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, explainPacketRevisionId);
    }

    private static JsonScopePayload ParseJsonScopePayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return JsonScopePayload.JsonPayloadMissingScopeFields;
            }

            if (!TryGetJsonStringProperty(document.RootElement, "approvedExplainPacketId", out var approvedExplainPacketId) ||
                !TryGetJsonStringProperty(document.RootElement, "explainPacketRevisionId", out var explainPacketRevisionId))
            {
                return JsonScopePayload.JsonPayloadMissingScopeFields;
            }

            return new JsonScopePayload(
                IsJsonPayload: true,
                HasScopeFields: true,
                ApprovedExplainPacketId: approvedExplainPacketId,
                ExplainPacketRevisionId: explainPacketRevisionId);
        }
        catch (JsonException)
        {
            return JsonScopePayload.NotJson;
        }
    }

    private static bool TryGetJsonStringProperty(
        JsonElement element,
        string propertyName,
        out string value)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is JsonValueKind.String)
        {
            value = TrimScopeValue(property.GetString());
            return value.Length > 0;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                candidate.Value.ValueKind is JsonValueKind.String)
            {
                value = TrimScopeValue(candidate.Value.GetString());
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
        var match = Regex.Match(payload, pattern, RegexOptions.CultureInvariant);
        if (match.Success)
        {
            value = TrimScopeValue(match.Groups["value"].Value);
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

    private static string TrimScopeValue(string? value) => value?.Trim() ?? string.Empty;

    private readonly record struct JsonScopePayload(
        bool IsJsonPayload,
        bool HasScopeFields,
        string ApprovedExplainPacketId,
        string ExplainPacketRevisionId)
    {
        public static JsonScopePayload NotJson => new(
            IsJsonPayload: false,
            HasScopeFields: false,
            ApprovedExplainPacketId: string.Empty,
            ExplainPacketRevisionId: string.Empty);

        public static JsonScopePayload JsonPayloadMissingScopeFields => new(
            IsJsonPayload: true,
            HasScopeFields: false,
            ApprovedExplainPacketId: string.Empty,
            ExplainPacketRevisionId: string.Empty);
    }

    private static IReadOnlyList<BuildExplainCompanionRoleReceiptGroup> BuildRoleReceiptGroups(
        IEnumerable<BuildExplainCompanionArtifactReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => receipt.Role)
            .OrderBy(static group => group.Key)
            .Select(static group =>
            {
                var rows = group
                    .OrderBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new BuildExplainCompanionRoleReceiptGroup(
                    Role: group.Key,
                    ReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.ReceiptId)),
                    JobIds: OrderedDistinct(rows.Select(static receipt => receipt.JobId)),
                    CompanionRefs: OrderedDistinct(rows.Select(static receipt => receipt.CompanionRef)),
                    CaptionRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.CaptionRefs)),
                    PreviewRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.PreviewRefs)),
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

    private static MediaRenderJobType ToJobType(BuildExplainCompanionArtifactRole role) =>
        role switch
        {
            BuildExplainCompanionArtifactRole.Video => MediaRenderJobType.BuildExplainCompanionVideo,
            BuildExplainCompanionArtifactRole.Audio => MediaRenderJobType.BuildExplainCompanionAudio,
            BuildExplainCompanionArtifactRole.PreviewCard => MediaRenderJobType.BuildExplainCompanionPreviewCard,
            BuildExplainCompanionArtifactRole.PacketCompanion => MediaRenderJobType.BuildExplainCompanionPacketCompanion,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported build explain companion artifact role.")
        };

    private static string BuildScopedDeduplicationKey(
        BuildExplainCompanionRenderRequest request,
        BuildExplainCompanionArtifactRenderRequest artifact)
    {
        var fields = new[]
        {
            "build-explain-companion",
            request.ApprovedExplainPacketId,
            request.ExplainPacketRevisionId,
            request.RenderingId,
            artifact.Role.ToString(),
            artifact.Category,
            artifact.OutputFormat,
            artifact.CompanionRef,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "build-explain-companion:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildReceiptId(
        BuildExplainCompanionRenderRequest request,
        BuildExplainCompanionArtifactRenderRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(request, artifact),
            BuildRefHashSegment("caption", artifact.CaptionRefs),
            BuildRefHashSegment("preview", artifact.PreviewRefs));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "build_explain_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
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
        IEnumerable<BuildExplainCompanionArtifactReceipt> receipts,
        BuildExplainCompanionArtifactRole role) =>
        receipts
            .Where(receipt => receipt.Role == role)
            .Select(static receipt => receipt.ReceiptId)
            .OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<BuildExplainCompanionRefReceipt> BuildCompanionRefReceipts(
        IEnumerable<BuildExplainCompanionArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new BuildExplainCompanionRefReceipt(
                Ref: receipt.CompanionRef,
                Role: receipt.Role,
                Category: receipt.Category,
                ReceiptId: receipt.ReceiptId,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private static IReadOnlyList<BuildExplainCompanionReadyRef> BuildCompanionReadyRefs(
        IEnumerable<BuildExplainCompanionArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new BuildExplainCompanionReadyRef(
                Ref: receipt.CompanionRef,
                Role: receipt.Role,
                Category: receipt.Category,
                ReceiptId: receipt.ReceiptId,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private static IReadOnlyList<BuildExplainCaptionRefReceipt> BuildCaptionRefReceipts(
        IEnumerable<BuildExplainCompanionArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.CaptionRefs.Select(captionRef => (captionRef, receipt)))
            .GroupBy(static item => item.captionRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new BuildExplainCaptionRefReceipt(
                Ref: CanonicalizeGroupedRef(group.Select(static item => item.captionRef)),
                ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId)),
                JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId)),
                CompanionRefs: OrderedDistinct(group.Select(static item => item.receipt.CompanionRef)),
                Roles: group
                    .Select(static item => item.receipt.Role)
                    .Distinct()
                    .OrderBy(static role => role)
                    .ToArray(),
                ArtifactReceipts: BuildGroupedArtifactReceipts(group.Select(static item => item.receipt))))
            .ToArray();

    private static IReadOnlyList<BuildExplainPreviewRefReceipt> BuildPreviewRefReceipts(
        IEnumerable<BuildExplainCompanionArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.PreviewRefs.Select(previewRef => (previewRef, receipt)))
            .GroupBy(static item => item.previewRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new BuildExplainPreviewRefReceipt(
                Ref: CanonicalizeGroupedRef(group.Select(static item => item.previewRef)),
                ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId)),
                JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId)),
                CompanionRefs: OrderedDistinct(group.Select(static item => item.receipt.CompanionRef)),
                Roles: group
                    .Select(static item => item.receipt.Role)
                    .Distinct()
                    .OrderBy(static role => role)
                    .ToArray(),
                ArtifactReceipts: BuildGroupedArtifactReceipts(group.Select(static item => item.receipt))))
            .ToArray();

    private static string CanonicalizeGroupedRef(IEnumerable<string> refs) =>
        OrderedDistinct(refs).First();

    private static IReadOnlyList<BuildExplainCompanionGroupedArtifactReceipt> BuildGroupedArtifactReceipts(
        IEnumerable<BuildExplainCompanionArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new BuildExplainCompanionGroupedArtifactReceipt(
                ReceiptId: receipt.ReceiptId,
                Role: receipt.Role,
                Category: receipt.Category,
                CompanionRef: receipt.CompanionRef,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
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
                throw new InvalidOperationException($"Build explain companion job {jobId} disappeared before receipt emission.");
            }

            if (status.State is MediaRenderJobState.Succeeded)
            {
                return status;
            }

            if (status.State is MediaRenderJobState.Failed or MediaRenderJobState.Expired)
            {
                throw new InvalidOperationException($"Build explain companion job {jobId} ended as {status.State}: {status.Error ?? "unknown"}");
            }

            await Task.Delay(20, cancellationToken);
        }

        throw new TimeoutException($"Build explain companion job {jobId} did not finish before receipt emission.");
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
                $"Build explain companion job {status.JobId} succeeded without asset and lifecycle truth required for receipt emission.");
        }
    }

    private static DateTimeOffset MaxTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;
}
