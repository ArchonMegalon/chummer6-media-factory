using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chummer.Run.AI.Services.Assets;

public interface IExplainPresenterSiblingRenderingService
{
    Task<ExplainPresenterSiblingRenderReceipt> RenderAsync(
        ExplainPresenterSiblingRenderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ExplainPresenterSiblingRenderingService : IExplainPresenterSiblingRenderingService
{
    private readonly IMediaRenderJobService _jobs;

    public ExplainPresenterSiblingRenderingService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<ExplainPresenterSiblingRenderReceipt> RenderAsync(
        ExplainPresenterSiblingRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var receipts = new List<ExplainPresenterSiblingArtifactReceipt>(normalized.Artifacts.Count);
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

            receipts.Add(new ExplainPresenterSiblingArtifactReceipt(
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

        return new ExplainPresenterSiblingRenderReceipt(
            RenderingId: normalized.RenderingId,
            ApprovedExplanationPacketId: normalized.ApprovedExplanationPacketId,
            ExplanationPacketRevisionId: normalized.ExplanationPacketRevisionId,
            GroundingScopeRef: normalized.GroundingScopeRef,
            Source: normalized.Source,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: renderedAtUtc ?? normalized.RequestedAtUtc,
            FirstPartyTextFallback: normalized.FirstPartyTextFallback,
            TextFallbackReceipt: BuildTextFallbackReceipt(normalized),
            Artifacts: receipts,
            AudioReceiptIds: ReceiptIdsFor(receipts, ExplainPresenterSiblingArtifactRole.Audio),
            PresenterReceiptIds: ReceiptIdsFor(receipts, ExplainPresenterSiblingArtifactRole.PresenterVideo),
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

    private static ExplainPresenterSiblingRenderRequest Normalize(ExplainPresenterSiblingRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RenderingId, nameof(request.RenderingId));
        RequireText(request.ApprovedExplanationPacketId, nameof(request.ApprovedExplanationPacketId));
        RequireText(request.ExplanationPacketRevisionId, nameof(request.ExplanationPacketRevisionId));
        RequireText(request.GroundingScopeRef, nameof(request.GroundingScopeRef));
        RequireText(request.Source, nameof(request.Source));
        RequireText(request.FirstPartyTextFallback, nameof(request.FirstPartyTextFallback));
        if (request.Artifacts is null)
        {
            throw new ArgumentNullException(nameof(ExplainPresenterSiblingRenderRequest.Artifacts));
        }
        if (request.Artifacts.Count == 0)
        {
            throw new ArgumentException("At least one explain presenter sibling artifact is required.", nameof(request));
        }

        var artifacts = request.Artifacts
            .Select((artifact, index) => NormalizeArtifact(artifact, index))
            .ToArray();
        var normalizedRequest = request with
        {
            RenderingId = request.RenderingId.Trim(),
            ApprovedExplanationPacketId = request.ApprovedExplanationPacketId.Trim(),
            ExplanationPacketRevisionId = request.ExplanationPacketRevisionId.Trim(),
            GroundingScopeRef = request.GroundingScopeRef.Trim(),
            Source = request.Source.Trim(),
            FirstPartyTextFallback = request.FirstPartyTextFallback.Trim(),
        };

        RequirePayloadScope(artifacts, normalizedRequest);
        RequireAtLeastOneRole(artifacts, normalizedRequest);
        RequireUniqueCompanionRefs(artifacts);

        return normalizedRequest with
        {
            Artifacts = artifacts
                .OrderBy(static artifact => artifact.Role)
                .ThenBy(static artifact => artifact.CompanionRef, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.OutputFormat, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.DeduplicationKey, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static ExplainPresenterSiblingArtifactRenderRequest NormalizeArtifact(
        ExplainPresenterSiblingArtifactRenderRequest? artifact,
        int index)
    {
        if (artifact is null)
        {
            throw new ArgumentException(
                $"Explain presenter sibling artifacts[{index}] is required.",
                nameof(ExplainPresenterSiblingRenderRequest.Artifacts));
        }

        RequireText(artifact.Category, nameof(artifact.Category));
        RequireText(artifact.Payload, nameof(artifact.Payload));
        RequireText(artifact.OutputFormat, nameof(artifact.OutputFormat));
        RequireText(artifact.CompanionRef, nameof(artifact.CompanionRef));
        RequireText(artifact.DeduplicationKey, nameof(artifact.DeduplicationKey));

        var captionRefs = NormalizeRefs(artifact.CaptionRefs, nameof(artifact.CaptionRefs));
        var previewRefs = NormalizeRefs(artifact.PreviewRefs, nameof(artifact.PreviewRefs));

        if (captionRefs.Count == 0)
        {
            throw new ArgumentException("Explain presenter audio and presenter siblings require at least one caption ref.", nameof(artifact));
        }

        if (artifact.Role is ExplainPresenterSiblingArtifactRole.PresenterVideo && previewRefs.Count == 0)
        {
            throw new ArgumentException("Explain presenter video siblings require at least one preview ref.", nameof(artifact));
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

    private static void RequireAtLeastOneRole(
        IReadOnlyCollection<ExplainPresenterSiblingArtifactRenderRequest> artifacts,
        ExplainPresenterSiblingRenderRequest request)
    {
        if (!artifacts.Any(artifact => artifact.Role is ExplainPresenterSiblingArtifactRole.Audio or ExplainPresenterSiblingArtifactRole.PresenterVideo))
        {
            throw new ArgumentException("Explain presenter rendering requires at least one audio or presenter artifact.", nameof(request));
        }
    }

    private static void RequireUniqueCompanionRefs(IReadOnlyCollection<ExplainPresenterSiblingArtifactRenderRequest> artifacts)
    {
        var duplicate = artifacts
            .GroupBy(static artifact => artifact.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Explain presenter companion refs must be unique per approved explanation packet: {duplicate.Key}.",
                nameof(ExplainPresenterSiblingRenderRequest.Artifacts));
        }
    }

    private static void RequirePayloadScope(
        IReadOnlyCollection<ExplainPresenterSiblingArtifactRenderRequest> artifacts,
        ExplainPresenterSiblingRenderRequest request)
    {
        var missingPacketId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesApprovedPacketId(artifact.Payload, request.ApprovedExplanationPacketId));
        if (missingPacketId is not null)
        {
            throw new ArgumentException(
                "Explain presenter payloads must stay scoped to the approved explanation packet id.",
                nameof(ExplainPresenterSiblingRenderRequest.Artifacts));
        }

        var missingRevisionId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesRevisionId(artifact.Payload, request.ExplanationPacketRevisionId));
        if (missingRevisionId is not null)
        {
            throw new ArgumentException(
                "Explain presenter payloads must stay scoped to the explanation packet revision id.",
                nameof(ExplainPresenterSiblingRenderRequest.Artifacts));
        }

        var missingGroundingScope = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesGroundingScopeRef(artifact.Payload, request.GroundingScopeRef));
        if (missingGroundingScope is not null)
        {
            throw new ArgumentException(
                "Explain presenter payloads must stay scoped to the grounding scope ref.",
                nameof(ExplainPresenterSiblingRenderRequest.Artifacts));
        }
    }

    private static bool PayloadMatchesApprovedPacketId(string payload, string approvedExplanationPacketId)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.ApprovedExplanationPacketId, approvedExplanationPacketId, StringComparison.Ordinal);
        }

        string scopedPacketId;
        if (TryParseScopeFromTextPayload(payload, ["approvedExplanationPacketId", "packet_id", "packetId"], out scopedPacketId))
        {
            return string.Equals(scopedPacketId, approvedExplanationPacketId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, approvedExplanationPacketId);
    }

    private static bool PayloadMatchesRevisionId(string payload, string explanationPacketRevisionId)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.ExplanationPacketRevisionId, explanationPacketRevisionId, StringComparison.Ordinal);
        }

        string scopedRevisionId;
        if (TryParseScopeFromTextPayload(payload, ["explanationPacketRevisionId", "packetRevisionId", "packet_revision_id"], out scopedRevisionId))
        {
            return string.Equals(scopedRevisionId, explanationPacketRevisionId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, explanationPacketRevisionId);
    }

    private static bool PayloadMatchesGroundingScopeRef(string payload, string groundingScopeRef)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.GroundingScopeRef, groundingScopeRef, StringComparison.Ordinal);
        }

        string scopedGroundingScope;
        if (TryParseScopeFromTextPayload(payload, ["groundingScopeRef", "grounding_scope_ref", "value_ref", "valueRef"], out scopedGroundingScope))
        {
            return string.Equals(scopedGroundingScope, groundingScopeRef, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, groundingScopeRef);
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

            if (!TryGetJsonStringProperty(document.RootElement, ["approvedExplanationPacketId", "packet_id", "packetId"], out var approvedExplanationPacketId) ||
                !TryGetJsonStringProperty(document.RootElement, ["explanationPacketRevisionId", "packetRevisionId", "packet_revision_id"], out var explanationPacketRevisionId) ||
                !TryGetJsonStringProperty(document.RootElement, ["groundingScopeRef", "grounding_scope_ref", "value_ref", "valueRef"], out var groundingScopeRef))
            {
                return JsonScopePayload.JsonPayloadMissingScopeFields;
            }

            return new JsonScopePayload(
                IsJsonPayload: true,
                HasScopeFields: true,
                ApprovedExplanationPacketId: approvedExplanationPacketId,
                ExplanationPacketRevisionId: explanationPacketRevisionId,
                GroundingScopeRef: groundingScopeRef);
        }
        catch (JsonException)
        {
            return JsonScopePayload.NotJson;
        }
    }

    private static bool TryGetJsonStringProperty(JsonElement element, IReadOnlyList<string> propertyNames, out string value)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.String)
            {
                value = TrimScopeValue(property.GetString());
                return value.Length > 0;
            }
        }

        foreach (var candidate in element.EnumerateObject())
        {
            foreach (var propertyName in propertyNames)
            {
                if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                    candidate.Value.ValueKind is JsonValueKind.String)
                {
                    value = TrimScopeValue(candidate.Value.GetString());
                    return value.Length > 0;
                }
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryParseScopeFromTextPayload(string payload, IReadOnlyList<string> propertyNames, out string value)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(propertyNames);

        foreach (var propertyName in propertyNames)
        {
            var pattern = $@"(?<![A-Za-z0-9_\-]){Regex.Escape(propertyName)}\s*[:=]\s*(?:""(?<value>[^""]+)""|'(?<value>[^']+)'|(?<value>[^\s,;|&]+))";
            var match = Regex.Match(payload, pattern, RegexOptions.CultureInvariant);
            if (match.Success)
            {
                value = TrimScopeValue(match.Groups["value"].Value);
                return value.Length > 0;
            }
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
        string ApprovedExplanationPacketId,
        string ExplanationPacketRevisionId,
        string GroundingScopeRef)
    {
        public static JsonScopePayload NotJson => new(
            IsJsonPayload: false,
            HasScopeFields: false,
            ApprovedExplanationPacketId: string.Empty,
            ExplanationPacketRevisionId: string.Empty,
            GroundingScopeRef: string.Empty);

        public static JsonScopePayload JsonPayloadMissingScopeFields => new(
            IsJsonPayload: true,
            HasScopeFields: false,
            ApprovedExplanationPacketId: string.Empty,
            ExplanationPacketRevisionId: string.Empty,
            GroundingScopeRef: string.Empty);
    }

    private static IReadOnlyList<ExplainPresenterSiblingRoleReceiptGroup> BuildRoleReceiptGroups(
        IEnumerable<ExplainPresenterSiblingArtifactReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => receipt.Role)
            .OrderBy(static group => group.Key)
            .Select(static group =>
            {
                var rows = group
                    .OrderBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new ExplainPresenterSiblingRoleReceiptGroup(
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

    private static MediaRenderJobType ToJobType(ExplainPresenterSiblingArtifactRole role) =>
        role switch
        {
            ExplainPresenterSiblingArtifactRole.Audio => MediaRenderJobType.ExplainPresenterSiblingAudio,
            ExplainPresenterSiblingArtifactRole.PresenterVideo => MediaRenderJobType.ExplainPresenterSiblingPresenterVideo,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported explain presenter artifact role.")
        };

    private static string BuildScopedDeduplicationKey(
        ExplainPresenterSiblingRenderRequest request,
        ExplainPresenterSiblingArtifactRenderRequest artifact)
    {
        var fields = new[]
        {
            "explain-presenter-sibling",
            request.ApprovedExplanationPacketId,
            request.ExplanationPacketRevisionId,
            request.GroundingScopeRef,
            request.RenderingId,
            artifact.Role.ToString(),
            artifact.Category,
            artifact.OutputFormat,
            artifact.CompanionRef,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "explain-presenter-sibling:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildReceiptId(
        ExplainPresenterSiblingRenderRequest request,
        ExplainPresenterSiblingArtifactRenderRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(request, artifact),
            BuildRefHashSegment("caption", artifact.CaptionRefs),
            BuildRefHashSegment("preview", artifact.PreviewRefs),
            $"{request.FirstPartyTextFallback.Length}:{request.FirstPartyTextFallback}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "explain_presenter_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static ExplainPresenterTextFallbackReceipt BuildTextFallbackReceipt(ExplainPresenterSiblingRenderRequest request)
    {
        var input = string.Join(
            "\n",
            "explain-presenter-text-fallback",
            $"{request.ApprovedExplanationPacketId.Length}:{request.ApprovedExplanationPacketId}",
            $"{request.ExplanationPacketRevisionId.Length}:{request.ExplanationPacketRevisionId}",
            $"{request.GroundingScopeRef.Length}:{request.GroundingScopeRef}",
            $"{request.FirstPartyTextFallback.Length}:{request.FirstPartyTextFallback}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new ExplainPresenterTextFallbackReceipt(
            ReceiptId: "explain_presenter_text_fallback_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16],
            Text: request.FirstPartyTextFallback,
            GroundingScopeRef: request.GroundingScopeRef);
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
        IEnumerable<ExplainPresenterSiblingArtifactReceipt> receipts,
        ExplainPresenterSiblingArtifactRole role) =>
        receipts
            .Where(receipt => receipt.Role == role)
            .Select(static receipt => receipt.ReceiptId)
            .OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<ExplainPresenterCompanionRefReceipt> BuildCompanionRefReceipts(
        IEnumerable<ExplainPresenterSiblingArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new ExplainPresenterCompanionRefReceipt(
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

    private static IReadOnlyList<ExplainPresenterSiblingReadyRef> BuildCompanionReadyRefs(
        IEnumerable<ExplainPresenterSiblingArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new ExplainPresenterSiblingReadyRef(
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

    private static IReadOnlyList<ExplainPresenterCaptionRefReceipt> BuildCaptionRefReceipts(
        IEnumerable<ExplainPresenterSiblingArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.CaptionRefs.Select(captionRef => (captionRef, receipt)))
            .GroupBy(static item => item.captionRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new ExplainPresenterCaptionRefReceipt(
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

    private static IReadOnlyList<ExplainPresenterPreviewRefReceipt> BuildPreviewRefReceipts(
        IEnumerable<ExplainPresenterSiblingArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.PreviewRefs.Select(previewRef => (previewRef, receipt)))
            .GroupBy(static item => item.previewRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new ExplainPresenterPreviewRefReceipt(
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

    private static IReadOnlyList<ExplainPresenterSiblingGroupedArtifactReceipt> BuildGroupedArtifactReceipts(
        IEnumerable<ExplainPresenterSiblingArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new ExplainPresenterSiblingGroupedArtifactReceipt(
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
                throw new InvalidOperationException($"Explain presenter job {jobId} disappeared before receipt emission.");
            }

            if (status.State is MediaRenderJobState.Succeeded)
            {
                return status;
            }

            if (status.State is MediaRenderJobState.Failed or MediaRenderJobState.Expired)
            {
                throw new InvalidOperationException($"Explain presenter job {jobId} ended as {status.State}: {status.Error ?? "unknown"}");
            }

            await Task.Delay(20, cancellationToken);
        }

        throw new TimeoutException($"Explain presenter job {jobId} did not finish before receipt emission.");
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
                $"Explain presenter job {status.JobId} succeeded without asset and lifecycle truth required for receipt emission.");
        }
    }

    private static DateTimeOffset MaxTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;
}
