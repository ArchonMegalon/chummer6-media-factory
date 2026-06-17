using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chummer.Run.AI.Services.Assets;

public interface IOriginDossierNarrationRenderingService
{
    Task<OriginDossierNarrationRenderReceipt> RenderAsync(
        OriginDossierNarrationRenderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class OriginDossierNarrationRenderingService : IOriginDossierNarrationRenderingService
{
    private readonly IMediaRenderJobService _jobs;

    public OriginDossierNarrationRenderingService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<OriginDossierNarrationRenderReceipt> RenderAsync(
        OriginDossierNarrationRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var receipts = new List<OriginDossierNarrationArtifactReceipt>(normalized.Artifacts.Count);
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

            receipts.Add(new OriginDossierNarrationArtifactReceipt(
                ReceiptId: BuildReceiptId(normalized, artifact),
                Role: artifact.Role,
                Provider: artifact.Provider,
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

        return new OriginDossierNarrationRenderReceipt(
            RenderingId: normalized.RenderingId,
            ApprovedOriginPacketId: normalized.ApprovedOriginPacketId,
            OriginRevisionId: normalized.OriginRevisionId,
            Source: normalized.Source,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: renderedAtUtc ?? normalized.RequestedAtUtc,
            Artifacts: receipts,
            PrimaryAudioReceiptIds: ReceiptIdsFor(receipts, OriginDossierNarrationArtifactRole.CanonicalAudio),
            AlternateAudioReceiptIds: ReceiptIdsFor(receipts, OriginDossierNarrationArtifactRole.AlternateAudio),
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

    private static OriginDossierNarrationRenderRequest Normalize(OriginDossierNarrationRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RenderingId, nameof(request.RenderingId));
        RequireText(request.ApprovedOriginPacketId, nameof(request.ApprovedOriginPacketId));
        RequireText(request.OriginRevisionId, nameof(request.OriginRevisionId));
        RequireText(request.Source, nameof(request.Source));
        if (request.Artifacts is null)
        {
            throw new ArgumentNullException(nameof(OriginDossierNarrationRenderRequest.Artifacts));
        }

        if (request.Artifacts.Count == 0)
        {
            throw new ArgumentException("At least one origin dossier narration artifact is required.", nameof(request));
        }

        var artifacts = request.Artifacts
            .Select((artifact, index) => NormalizeArtifact(artifact, index))
            .ToArray();
        var normalizedRequest = request with
        {
            RenderingId = request.RenderingId.Trim(),
            ApprovedOriginPacketId = request.ApprovedOriginPacketId.Trim(),
            OriginRevisionId = request.OriginRevisionId.Trim(),
            Source = request.Source.Trim()
        };

        RequirePayloadScope(artifacts, normalizedRequest);
        RequireRole(artifacts, OriginDossierNarrationArtifactRole.CanonicalAudio, request);
        RequireUniqueCompanionRefs(artifacts);

        return normalizedRequest with
        {
            Artifacts = artifacts
                .OrderBy(static artifact => artifact.Role)
                .ThenBy(static artifact => artifact.Provider, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.CompanionRef, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.OutputFormat, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.DeduplicationKey, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static OriginDossierNarrationArtifactRenderRequest NormalizeArtifact(
        OriginDossierNarrationArtifactRenderRequest? artifact,
        int index)
    {
        if (artifact is null)
        {
            throw new ArgumentException(
                $"Origin dossier narration artifacts[{index}] is required.",
                nameof(OriginDossierNarrationRenderRequest.Artifacts));
        }

        RequireText(artifact.Provider, nameof(artifact.Provider));
        RequireText(artifact.Category, nameof(artifact.Category));
        RequireText(artifact.Payload, nameof(artifact.Payload));
        RequireText(artifact.OutputFormat, nameof(artifact.OutputFormat));
        RequireText(artifact.CompanionRef, nameof(artifact.CompanionRef));
        RequireText(artifact.DeduplicationKey, nameof(artifact.DeduplicationKey));

        var captionRefs = NormalizeRefs(artifact.CaptionRefs, nameof(artifact.CaptionRefs));
        var previewRefs = NormalizeRefs(artifact.PreviewRefs, nameof(artifact.PreviewRefs));
        if (captionRefs.Count == 0)
        {
            throw new ArgumentException("Origin dossier narration audio artifacts require at least one caption ref.", nameof(artifact));
        }

        return artifact with
        {
            Provider = artifact.Provider.Trim(),
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
        IReadOnlyCollection<OriginDossierNarrationArtifactRenderRequest> artifacts,
        OriginDossierNarrationArtifactRole role,
        OriginDossierNarrationRenderRequest request)
    {
        if (!artifacts.Any(artifact => artifact.Role == role))
        {
            throw new ArgumentException($"Origin dossier narration rendering requires at least one {role} artifact.", nameof(request));
        }
    }

    private static void RequireUniqueCompanionRefs(IReadOnlyCollection<OriginDossierNarrationArtifactRenderRequest> artifacts)
    {
        var duplicate = artifacts
            .GroupBy(static artifact => artifact.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Origin dossier narration companion refs must be unique per approved origin packet: {duplicate.Key}.",
                nameof(OriginDossierNarrationRenderRequest.Artifacts));
        }
    }

    private static void RequirePayloadScope(
        IReadOnlyCollection<OriginDossierNarrationArtifactRenderRequest> artifacts,
        OriginDossierNarrationRenderRequest request)
    {
        var missingPacketId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesApprovedPacketId(artifact.Payload, request.ApprovedOriginPacketId));
        if (missingPacketId is not null)
        {
            throw new ArgumentException(
                "Origin dossier narration payloads must stay scoped to the approved origin packet id.",
                nameof(OriginDossierNarrationRenderRequest.Artifacts));
        }

        var missingRevisionId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesRevisionId(artifact.Payload, request.OriginRevisionId));
        if (missingRevisionId is not null)
        {
            throw new ArgumentException(
                "Origin dossier narration payloads must stay scoped to the origin revision id.",
                nameof(OriginDossierNarrationRenderRequest.Artifacts));
        }
    }

    private static bool PayloadMatchesApprovedPacketId(string payload, string approvedOriginPacketId)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.ApprovedOriginPacketId, approvedOriginPacketId, StringComparison.Ordinal);
        }

        string scopedPacketId;
        if (TryParseScopeFromTextPayload(payload, ["approvedOriginPacketId", "originPacketId", "packetId", "packet_id"], out scopedPacketId))
        {
            return string.Equals(scopedPacketId, approvedOriginPacketId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, approvedOriginPacketId);
    }

    private static bool PayloadMatchesRevisionId(string payload, string originRevisionId)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.OriginRevisionId, originRevisionId, StringComparison.Ordinal);
        }

        string scopedRevisionId;
        if (TryParseScopeFromTextPayload(payload, ["originRevisionId", "packetRevisionId", "origin_revision_id", "packet_revision_id"], out scopedRevisionId))
        {
            return string.Equals(scopedRevisionId, originRevisionId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, originRevisionId);
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

            if (!TryGetJsonStringProperty(document.RootElement, ["approvedOriginPacketId", "originPacketId", "packetId", "packet_id"], out var approvedOriginPacketId) ||
                !TryGetJsonStringProperty(document.RootElement, ["originRevisionId", "packetRevisionId", "origin_revision_id", "packet_revision_id"], out var originRevisionId))
            {
                return JsonScopePayload.JsonPayloadMissingScopeFields;
            }

            return new JsonScopePayload(
                IsJsonPayload: true,
                HasScopeFields: true,
                ApprovedOriginPacketId: approvedOriginPacketId,
                OriginRevisionId: originRevisionId);
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
        string ApprovedOriginPacketId,
        string OriginRevisionId)
    {
        public static JsonScopePayload NotJson => new(
            IsJsonPayload: false,
            HasScopeFields: false,
            ApprovedOriginPacketId: string.Empty,
            OriginRevisionId: string.Empty);

        public static JsonScopePayload JsonPayloadMissingScopeFields => new(
            IsJsonPayload: true,
            HasScopeFields: false,
            ApprovedOriginPacketId: string.Empty,
            OriginRevisionId: string.Empty);
    }

    private static IReadOnlyList<OriginDossierNarrationRoleReceiptGroup> BuildRoleReceiptGroups(
        IEnumerable<OriginDossierNarrationArtifactReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => receipt.Role)
            .OrderBy(static group => group.Key)
            .Select(static group =>
            {
                var rows = group
                    .OrderBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new OriginDossierNarrationRoleReceiptGroup(
                    Role: group.Key,
                    ReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.ReceiptId)),
                    JobIds: OrderedDistinct(rows.Select(static receipt => receipt.JobId)),
                    CompanionRefs: OrderedDistinct(rows.Select(static receipt => receipt.CompanionRef)),
                    Providers: OrderedDistinct(rows.Select(static receipt => receipt.Provider)),
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

    private static MediaRenderJobType ToJobType(OriginDossierNarrationArtifactRole role) =>
        role switch
        {
            OriginDossierNarrationArtifactRole.CanonicalAudio => MediaRenderJobType.OriginDossierCanonicalAudiobookAudio,
            OriginDossierNarrationArtifactRole.AlternateAudio => MediaRenderJobType.OriginDossierAlternateAudiobookAudio,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported origin dossier narration artifact role.")
        };

    private static string BuildScopedDeduplicationKey(
        OriginDossierNarrationRenderRequest request,
        OriginDossierNarrationArtifactRenderRequest artifact)
    {
        var fields = new[]
        {
            "origin-dossier-narration",
            request.ApprovedOriginPacketId,
            request.OriginRevisionId,
            request.RenderingId,
            artifact.Role.ToString(),
            artifact.Provider,
            artifact.Category,
            artifact.OutputFormat,
            artifact.CompanionRef,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "origin-dossier-narration:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildReceiptId(
        OriginDossierNarrationRenderRequest request,
        OriginDossierNarrationArtifactRenderRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(request, artifact),
            BuildRefHashSegment("caption", artifact.CaptionRefs),
            BuildRefHashSegment("preview", artifact.PreviewRefs));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "origin_dossier_narration_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
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
        IEnumerable<OriginDossierNarrationArtifactReceipt> receipts,
        OriginDossierNarrationArtifactRole role) =>
        receipts
            .Where(receipt => receipt.Role == role)
            .Select(static receipt => receipt.ReceiptId)
            .OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<OriginDossierNarrationCompanionRefReceipt> BuildCompanionRefReceipts(
        IEnumerable<OriginDossierNarrationArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new OriginDossierNarrationCompanionRefReceipt(
                Ref: receipt.CompanionRef,
                Role: receipt.Role,
                Provider: receipt.Provider,
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

    private static IReadOnlyList<OriginDossierNarrationReadyRef> BuildCompanionReadyRefs(
        IEnumerable<OriginDossierNarrationArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new OriginDossierNarrationReadyRef(
                Ref: receipt.CompanionRef,
                Role: receipt.Role,
                Provider: receipt.Provider,
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

    private static IReadOnlyList<OriginDossierNarrationCaptionRefReceipt> BuildCaptionRefReceipts(
        IEnumerable<OriginDossierNarrationArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.CaptionRefs.Select(captionRef => (captionRef, receipt)))
            .GroupBy(static item => item.captionRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new OriginDossierNarrationCaptionRefReceipt(
                Ref: CanonicalizeGroupedRef(group.Select(static item => item.captionRef)),
                ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId)),
                JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId)),
                CompanionRefs: OrderedDistinct(group.Select(static item => item.receipt.CompanionRef)),
                Roles: group
                    .Select(static item => item.receipt.Role)
                    .Distinct()
                    .OrderBy(static role => role)
                    .ToArray(),
                Providers: OrderedDistinct(group.Select(static item => item.receipt.Provider)),
                ArtifactReceipts: BuildGroupedArtifactReceipts(group.Select(static item => item.receipt))))
            .ToArray();

    private static IReadOnlyList<OriginDossierNarrationPreviewRefReceipt> BuildPreviewRefReceipts(
        IEnumerable<OriginDossierNarrationArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.PreviewRefs.Select(previewRef => (previewRef, receipt)))
            .GroupBy(static item => item.previewRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new OriginDossierNarrationPreviewRefReceipt(
                Ref: CanonicalizeGroupedRef(group.Select(static item => item.previewRef)),
                ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId)),
                JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId)),
                CompanionRefs: OrderedDistinct(group.Select(static item => item.receipt.CompanionRef)),
                Roles: group
                    .Select(static item => item.receipt.Role)
                    .Distinct()
                    .OrderBy(static role => role)
                    .ToArray(),
                Providers: OrderedDistinct(group.Select(static item => item.receipt.Provider)),
                ArtifactReceipts: BuildGroupedArtifactReceipts(group.Select(static item => item.receipt))))
            .ToArray();

    private static string CanonicalizeGroupedRef(IEnumerable<string> refs) =>
        OrderedDistinct(refs).First();

    private static IReadOnlyList<OriginDossierNarrationGroupedArtifactReceipt> BuildGroupedArtifactReceipts(
        IEnumerable<OriginDossierNarrationArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.CompanionRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new OriginDossierNarrationGroupedArtifactReceipt(
                ReceiptId: receipt.ReceiptId,
                Role: receipt.Role,
                Provider: receipt.Provider,
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
                throw new InvalidOperationException($"Origin dossier narration job {jobId} disappeared before receipt emission.");
            }

            if (status.State is MediaRenderJobState.Succeeded)
            {
                return status;
            }

            if (status.State is MediaRenderJobState.Failed or MediaRenderJobState.Expired)
            {
                throw new InvalidOperationException($"Origin dossier narration job {jobId} ended as {status.State}: {status.Error ?? "unknown"}");
            }

            await Task.Delay(20, cancellationToken);
        }

        throw new TimeoutException($"Origin dossier narration job {jobId} did not finish before receipt emission.");
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
                $"Origin dossier narration job {status.JobId} succeeded without asset and lifecycle truth required for receipt emission.");
        }
    }

    private static DateTimeOffset MaxTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;
}
