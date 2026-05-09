using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chummer.Run.AI.Services.Assets;

public interface IModeratedTestimonialRenderingService
{
    Task<ModeratedTestimonialRenderReceipt> RenderAsync(
        ModeratedTestimonialRenderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ModeratedTestimonialRenderingService : IModeratedTestimonialRenderingService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);

    private readonly IMediaRenderJobService _jobs;

    public ModeratedTestimonialRenderingService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<ModeratedTestimonialRenderReceipt> RenderAsync(
        ModeratedTestimonialRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var receipts = new List<ModeratedTestimonialArtifactReceipt>(normalized.Artifacts.Count);
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

            receipts.Add(new ModeratedTestimonialArtifactReceipt(
                ReceiptId: BuildReceiptId(normalized, artifact),
                Role: artifact.Role,
                Category: artifact.Category,
                OutputFormat: artifact.OutputFormat,
                AssetRef: artifact.AssetRef,
                CaptionRefs: artifact.CaptionRefs,
                PreviewRefs: artifact.PreviewRefs,
                JobId: status.JobId,
                JobState: status.State,
                ModerationState: BuildModerationState(status),
                AssetId: status.AssetId,
                AssetUrl: status.AssetUrl,
                CacheTtl: status.CacheTtl,
                ApprovalState: status.ApprovalState,
                RetentionState: status.RetentionState,
                StorageClass: status.StorageClass));
        }

        return new ModeratedTestimonialRenderReceipt(
            RenderingId: normalized.RenderingId,
            PublicationId: normalized.PublicationId,
            ModerationCaseId: normalized.ModerationCaseId,
            SourceReceiptId: normalized.SourceReceiptId,
            ConsentReceiptId: normalized.ConsentReceiptId,
            Source: normalized.Source,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: renderedAtUtc ?? normalized.RequestedAtUtc,
            Artifacts: receipts,
            VideoReceiptIds: ReceiptIdsFor(receipts, ModeratedTestimonialArtifactRole.Video),
            AudioReceiptIds: ReceiptIdsFor(receipts, ModeratedTestimonialArtifactRole.Audio),
            PreviewCardReceiptIds: ReceiptIdsFor(receipts, ModeratedTestimonialArtifactRole.PreviewCard),
            TranscriptCardReceiptIds: ReceiptIdsFor(receipts, ModeratedTestimonialArtifactRole.TranscriptCard),
            JobIds: OrderedDistinct(receipts.Select(static receipt => receipt.JobId)),
            AssetRefs: OrderedDistinct(receipts.Select(static receipt => receipt.AssetRef)),
            ReadyRefs: BuildReadyRefs(receipts),
            CaptionRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.CaptionRefs)),
            PreviewRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.PreviewRefs)),
            RoleReceiptGroups: BuildRoleReceiptGroups(receipts),
            ArtifactRefReceipts: BuildArtifactRefReceipts(receipts),
            CaptionRefReceipts: BuildCaptionRefReceipts(receipts),
            PreviewRefReceipts: BuildPreviewRefReceipts(receipts));
    }

    private static ModeratedTestimonialRenderRequest Normalize(ModeratedTestimonialRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RenderingId, nameof(request.RenderingId));
        RequireText(request.PublicationId, nameof(request.PublicationId));
        RequireText(request.ModerationCaseId, nameof(request.ModerationCaseId));
        RequireText(request.SourceReceiptId, nameof(request.SourceReceiptId));
        RequireText(request.ConsentReceiptId, nameof(request.ConsentReceiptId));
        RequireText(request.Source, nameof(request.Source));
        if (request.Artifacts.Count == 0)
        {
            throw new ArgumentException("At least one moderated testimonial artifact is required.", nameof(request));
        }

        var artifacts = request.Artifacts
            .Select(NormalizeArtifact)
            .OrderBy(static artifact => artifact.Role)
            .ThenBy(static artifact => artifact.AssetRef, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static artifact => artifact.OutputFormat, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static artifact => artifact.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static artifact => artifact.DeduplicationKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var normalizedRequest = request with
        {
            RenderingId = request.RenderingId.Trim(),
            PublicationId = request.PublicationId.Trim(),
            ModerationCaseId = request.ModerationCaseId.Trim(),
            SourceReceiptId = request.SourceReceiptId.Trim(),
            ConsentReceiptId = request.ConsentReceiptId.Trim(),
            Source = request.Source.Trim(),
            Artifacts = artifacts
        };

        RequireRoleCoverage(artifacts, normalizedRequest);
        RequireUniqueAssetRefs(artifacts);
        RequirePayloadScope(artifacts, normalizedRequest);

        return normalizedRequest;
    }

    private static ModeratedTestimonialArtifactRenderRequest NormalizeArtifact(ModeratedTestimonialArtifactRenderRequest artifact)
    {
        RequireText(artifact.Category, nameof(artifact.Category));
        RequireText(artifact.Payload, nameof(artifact.Payload));
        RequireText(artifact.OutputFormat, nameof(artifact.OutputFormat));
        RequireText(artifact.AssetRef, nameof(artifact.AssetRef));
        RequireText(artifact.DeduplicationKey, nameof(artifact.DeduplicationKey));

        var captionRefs = NormalizeRefs(artifact.CaptionRefs, nameof(artifact.CaptionRefs));
        var previewRefs = NormalizeRefs(artifact.PreviewRefs, nameof(artifact.PreviewRefs));

        if (artifact.Role is ModeratedTestimonialArtifactRole.Video or ModeratedTestimonialArtifactRole.Audio
            && captionRefs.Count == 0)
        {
            throw new ArgumentException("Moderated testimonial video and audio artifacts require at least one caption ref.", nameof(artifact));
        }

        if (artifact.Role is ModeratedTestimonialArtifactRole.Video or ModeratedTestimonialArtifactRole.PreviewCard
            && previewRefs.Count == 0)
        {
            throw new ArgumentException("Moderated testimonial video and preview-card artifacts require at least one preview ref.", nameof(artifact));
        }

        return artifact with
        {
            Category = artifact.Category.Trim(),
            Payload = artifact.Payload.Trim(),
            OutputFormat = artifact.OutputFormat.Trim(),
            AssetRef = artifact.AssetRef.Trim(),
            CaptionRefs = captionRefs,
            PreviewRefs = previewRefs,
            DeduplicationKey = artifact.DeduplicationKey.Trim()
        };
    }

    private static void RequireRoleCoverage(
        IReadOnlyCollection<ModeratedTestimonialArtifactRenderRequest> artifacts,
        ModeratedTestimonialRenderRequest request)
    {
        foreach (var role in Enum.GetValues<ModeratedTestimonialArtifactRole>())
        {
            if (!artifacts.Any(artifact => artifact.Role == role))
            {
                throw new ArgumentException(
                    $"Moderated testimonial renders require at least one {role} artifact.",
                    nameof(request));
            }
        }
    }

    private static void RequireUniqueAssetRefs(IReadOnlyCollection<ModeratedTestimonialArtifactRenderRequest> artifacts)
    {
        var duplicate = artifacts
            .GroupBy(static artifact => artifact.AssetRef, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Moderated testimonial asset refs must be unique per render request: {duplicate.Key}.",
                nameof(ModeratedTestimonialRenderRequest.Artifacts));
        }
    }

    private static void RequirePayloadScope(
        IReadOnlyCollection<ModeratedTestimonialArtifactRenderRequest> artifacts,
        ModeratedTestimonialRenderRequest request)
    {
        var missingPublicationId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesScope(artifact.Payload, "publicationId", request.PublicationId));
        if (missingPublicationId is not null)
        {
            throw new ArgumentException(
                "Moderated testimonial payloads must stay scoped to the publication id.",
                nameof(ModeratedTestimonialRenderRequest.Artifacts));
        }

        var missingModerationCaseId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesScope(artifact.Payload, "moderationCaseId", request.ModerationCaseId));
        if (missingModerationCaseId is not null)
        {
            throw new ArgumentException(
                "Moderated testimonial payloads must stay scoped to the moderation case id.",
                nameof(ModeratedTestimonialRenderRequest.Artifacts));
        }

        var missingSourceReceiptId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesScope(artifact.Payload, "sourceReceiptId", request.SourceReceiptId));
        if (missingSourceReceiptId is not null)
        {
            throw new ArgumentException(
                "Moderated testimonial payloads must stay scoped to the source receipt id.",
                nameof(ModeratedTestimonialRenderRequest.Artifacts));
        }

        var missingConsentReceiptId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesScope(artifact.Payload, "consentReceiptId", request.ConsentReceiptId));
        if (missingConsentReceiptId is not null)
        {
            throw new ArgumentException(
                "Moderated testimonial payloads must stay scoped to the consent receipt id.",
                nameof(ModeratedTestimonialRenderRequest.Artifacts));
        }
    }

    private static bool PayloadMatchesScope(string payload, string propertyName, string expected)
    {
        var jsonScope = ParseJsonScopePayload(payload, propertyName);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeField &&
                   string.Equals(jsonScope.ScopeValue, expected, StringComparison.Ordinal);
        }

        if (TryParseScopeFromTextPayload(payload, propertyName, out var scopedValue))
        {
            return string.Equals(scopedValue, expected, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, expected);
    }

    private async Task<MediaRenderJobStatus> WaitForTerminalStatusAsync(string jobId, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = _jobs.Get(jobId) ?? throw new InvalidOperationException($"Unknown media render job '{jobId}'.");
            if (IsTerminal(status.State))
            {
                return status;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private static bool IsTerminal(MediaRenderJobState state) =>
        state is MediaRenderJobState.Succeeded
            or MediaRenderJobState.Failed
            or MediaRenderJobState.Expired;

    private static void ValidateReceiptStatus(MediaRenderJobStatus status)
    {
        if (status.State == MediaRenderJobState.Succeeded)
        {
            return;
        }

        if (status.State == MediaRenderJobState.Failed)
        {
            throw new InvalidOperationException(
                $"Moderated testimonial render job {status.JobId} failed: {status.Error ?? "unknown error"}");
        }

        throw new InvalidOperationException(
            $"Moderated testimonial render job {status.JobId} ended in unsupported state {status.State}.");
    }

    private static IReadOnlyList<ModeratedTestimonialReadyRef> BuildReadyRefs(
        IEnumerable<ModeratedTestimonialArtifactReceipt> receipts) =>
        receipts
            .Where(static receipt => receipt.JobState == MediaRenderJobState.Succeeded)
            .OrderBy(static receipt => receipt.AssetRef, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static receipt => receipt.Role)
            .Select(static receipt => new ModeratedTestimonialReadyRef(
                Ref: receipt.AssetRef,
                Role: receipt.Role,
                Category: receipt.Category,
                ReceiptId: receipt.ReceiptId,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                ModerationState: receipt.ModerationState,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private static IReadOnlyList<ModeratedTestimonialArtifactRefReceipt> BuildArtifactRefReceipts(
        IEnumerable<ModeratedTestimonialArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.AssetRef, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static receipt => receipt.Role)
            .Select(static receipt => new ModeratedTestimonialArtifactRefReceipt(
                Ref: receipt.AssetRef,
                Role: receipt.Role,
                Category: receipt.Category,
                ReceiptId: receipt.ReceiptId,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                ModerationState: receipt.ModerationState,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private static IReadOnlyList<ModeratedTestimonialCaptionRefReceipt> BuildCaptionRefReceipts(
        IEnumerable<ModeratedTestimonialArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.CaptionRefs.Select(refValue => (Ref: refValue, Receipt: receipt)))
            .GroupBy(static item => item.Ref, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group =>
            {
                var rows = group
                    .Select(static item => item.Receipt)
                    .OrderBy(static receipt => receipt.AssetRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new ModeratedTestimonialCaptionRefReceipt(
                    Ref: group.Key,
                    ReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.ReceiptId)),
                    JobIds: OrderedDistinct(rows.Select(static receipt => receipt.JobId)),
                    AssetRefs: OrderedDistinct(rows.Select(static receipt => receipt.AssetRef)),
                    Roles: rows.Select(static receipt => receipt.Role).Distinct().OrderBy(static role => role).ToArray(),
                    ArtifactReceipts: BuildGroupedArtifactReceipts(rows));
            })
            .ToArray();

    private static IReadOnlyList<ModeratedTestimonialPreviewRefReceipt> BuildPreviewRefReceipts(
        IEnumerable<ModeratedTestimonialArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.PreviewRefs.Select(refValue => (Ref: refValue, Receipt: receipt)))
            .GroupBy(static item => item.Ref, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group =>
            {
                var rows = group
                    .Select(static item => item.Receipt)
                    .OrderBy(static receipt => receipt.AssetRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new ModeratedTestimonialPreviewRefReceipt(
                    Ref: group.Key,
                    ReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.ReceiptId)),
                    JobIds: OrderedDistinct(rows.Select(static receipt => receipt.JobId)),
                    AssetRefs: OrderedDistinct(rows.Select(static receipt => receipt.AssetRef)),
                    Roles: rows.Select(static receipt => receipt.Role).Distinct().OrderBy(static role => role).ToArray(),
                    ArtifactReceipts: BuildGroupedArtifactReceipts(rows));
            })
            .ToArray();

    private static IReadOnlyList<ModeratedTestimonialRoleReceiptGroup> BuildRoleReceiptGroups(
        IEnumerable<ModeratedTestimonialArtifactReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => receipt.Role)
            .OrderBy(static group => group.Key)
            .Select(static group =>
            {
                var rows = group
                    .OrderBy(static receipt => receipt.AssetRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new ModeratedTestimonialRoleReceiptGroup(
                    Role: group.Key,
                    ReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.ReceiptId)),
                    JobIds: OrderedDistinct(rows.Select(static receipt => receipt.JobId)),
                    AssetRefs: OrderedDistinct(rows.Select(static receipt => receipt.AssetRef)),
                    CaptionRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.CaptionRefs)),
                    PreviewRefs: OrderedDistinct(rows.SelectMany(static receipt => receipt.PreviewRefs)),
                    ArtifactReceipts: BuildGroupedArtifactReceipts(rows));
            })
            .ToArray();

    private static IReadOnlyList<ModeratedTestimonialGroupedArtifactReceipt> BuildGroupedArtifactReceipts(
        IEnumerable<ModeratedTestimonialArtifactReceipt> receipts) =>
        receipts
            .Select(static receipt => new ModeratedTestimonialGroupedArtifactReceipt(
                ReceiptId: receipt.ReceiptId,
                Role: receipt.Role,
                Category: receipt.Category,
                AssetRef: receipt.AssetRef,
                JobId: receipt.JobId,
                JobState: receipt.JobState,
                OutputFormat: receipt.OutputFormat,
                ModerationState: receipt.ModerationState,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl,
                CacheTtl: receipt.CacheTtl,
                ApprovalState: receipt.ApprovalState,
                RetentionState: receipt.RetentionState,
                StorageClass: receipt.StorageClass))
            .ToArray();

    private static IReadOnlyList<string> ReceiptIdsFor(
        IEnumerable<ModeratedTestimonialArtifactReceipt> receipts,
        ModeratedTestimonialArtifactRole role) =>
        receipts
            .Where(receipt => receipt.Role == role)
            .Select(static receipt => receipt.ReceiptId)
            .OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)
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

    private static MediaRenderJobType ToJobType(ModeratedTestimonialArtifactRole role) =>
        role switch
        {
            ModeratedTestimonialArtifactRole.Video => MediaRenderJobType.ModeratedTestimonialVideo,
            ModeratedTestimonialArtifactRole.Audio => MediaRenderJobType.ModeratedTestimonialAudio,
            ModeratedTestimonialArtifactRole.PreviewCard => MediaRenderJobType.ModeratedTestimonialPreviewCard,
            ModeratedTestimonialArtifactRole.TranscriptCard => MediaRenderJobType.ModeratedTestimonialTranscriptCard,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported moderated testimonial artifact role.")
        };

    private static string BuildScopedDeduplicationKey(
        ModeratedTestimonialRenderRequest request,
        ModeratedTestimonialArtifactRenderRequest artifact)
    {
        var fields = new[]
        {
            "moderated-testimonial",
            request.PublicationId,
            request.ModerationCaseId,
            request.SourceReceiptId,
            request.ConsentReceiptId,
            request.RenderingId,
            artifact.Role.ToString(),
            artifact.Category,
            artifact.OutputFormat,
            artifact.AssetRef,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "moderated-testimonial:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildReceiptId(
        ModeratedTestimonialRenderRequest request,
        ModeratedTestimonialArtifactRenderRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(request, artifact),
            BuildRefHashSegment("caption", artifact.CaptionRefs),
            BuildRefHashSegment("preview", artifact.PreviewRefs));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "moderated_testimonial_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildRefHashSegment(string prefix, IReadOnlyList<string> refs) =>
        string.Join(
            "\n",
            new[] { $"{prefix}:{refs.Count}" }
                .Concat(refs.Select(static value => $"{value.Length}:{value}")));

    private static string BuildModerationState(MediaRenderJobStatus status) =>
        status.ApprovalState switch
        {
            AssetApprovalState.Approved => "approved",
            AssetApprovalState.Rejected => "rejected",
            AssetApprovalState.Pending => "pending-review",
            _ => status.State == MediaRenderJobState.Succeeded ? "rendered" : "pending-review"
        };

    private static DateTimeOffset MaxTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;

    private static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> values) =>
        values
            .GroupBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderBy(static value => value, StringComparer.Ordinal).First())
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    private static JsonScopePayload ParseJsonScopePayload(string payload, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return JsonScopePayload.JsonPayloadMissingScopeField;
            }

            if (!TryGetJsonStringProperty(document.RootElement, propertyName, out var scopeValue))
            {
                return JsonScopePayload.JsonPayloadMissingScopeField;
            }

            return new JsonScopePayload(
                IsJsonPayload: true,
                HasScopeField: true,
                ScopeValue: scopeValue);
        }
        catch (JsonException)
        {
            return JsonScopePayload.NotJson;
        }
    }

    private static bool TryGetJsonStringProperty(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.String)
        {
            value = TrimScopeValue(property.GetString());
            return value.Length > 0;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                && candidate.Value.ValueKind is JsonValueKind.String)
            {
                value = TrimScopeValue(candidate.Value.GetString());
                return value.Length > 0;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryParseScopeFromTextPayload(string payload, string propertyName, out string value)
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
        bool HasScopeField,
        string ScopeValue)
    {
        public static JsonScopePayload NotJson => new(
            IsJsonPayload: false,
            HasScopeField: false,
            ScopeValue: string.Empty);

        public static JsonScopePayload JsonPayloadMissingScopeField => new(
            IsJsonPayload: true,
            HasScopeField: false,
            ScopeValue: string.Empty);
    }
}
