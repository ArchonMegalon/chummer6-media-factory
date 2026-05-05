using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chummer.Run.AI.Services.Assets;

public interface ICreatorPromoKitRenderingService
{
    Task<CreatorPromoKitRenderReceipt> RenderAsync(
        CreatorPromoKitRenderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CreatorPromoKitRenderingService : ICreatorPromoKitRenderingService
{
    private readonly IMediaRenderJobService _jobs;

    public CreatorPromoKitRenderingService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<CreatorPromoKitRenderReceipt> RenderAsync(
        CreatorPromoKitRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        // This render-only lane renders creator promo siblings from approved manifests without taking publication ownership.
        var normalized = Normalize(request);
        var receipts = new List<CreatorPromoKitArtifactReceipt>(normalized.Artifacts.Count);
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

            receipts.Add(new CreatorPromoKitArtifactReceipt(
                ReceiptId: BuildReceiptId(normalized, artifact),
                Role: artifact.Role,
                Category: artifact.Category,
                OutputFormat: artifact.OutputFormat,
                ArtifactRef: artifact.ArtifactRef,
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

        return new CreatorPromoKitRenderReceipt(
            RenderingId: normalized.RenderingId,
            ApprovedManifestId: normalized.ApprovedManifestId,
            ManifestRevisionId: normalized.ManifestRevisionId,
            Source: normalized.Source,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: renderedAtUtc ?? normalized.RequestedAtUtc,
            Artifacts: receipts,
            PromoVideoReceiptIds: ReceiptIdsFor(receipts, CreatorPromoKitArtifactRole.PromoVideo),
            PromoPosterReceiptIds: ReceiptIdsFor(receipts, CreatorPromoKitArtifactRole.PromoPoster),
            PreviewCardReceiptIds: ReceiptIdsFor(receipts, CreatorPromoKitArtifactRole.PreviewCard),
            JobIds: receipts
                .Select(static receipt => receipt.JobId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static jobId => jobId, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ArtifactRefs: OrderedDistinct(receipts.Select(static receipt => receipt.ArtifactRef)),
            ReadyRefs: BuildReadyRefs(receipts),
            CaptionRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.CaptionRefs)),
            PreviewRefs: OrderedDistinct(receipts.SelectMany(static receipt => receipt.PreviewRefs)),
            RoleReceiptGroups: BuildRoleReceiptGroups(receipts),
            ArtifactRefReceipts: BuildArtifactRefReceipts(receipts),
            CaptionRefReceipts: BuildCaptionRefReceipts(receipts),
            PreviewRefReceipts: BuildPreviewRefReceipts(receipts));
    }

    private static CreatorPromoKitRenderRequest Normalize(CreatorPromoKitRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RenderingId, nameof(request.RenderingId));
        RequireText(request.ApprovedManifestId, nameof(request.ApprovedManifestId));
        RequireText(request.ManifestRevisionId, nameof(request.ManifestRevisionId));
        RequireText(request.Source, nameof(request.Source));
        if (request.Artifacts is null)
        {
            throw new ArgumentNullException(nameof(CreatorPromoKitRenderRequest.Artifacts));
        }

        if (request.Artifacts.Count == 0)
        {
            throw new ArgumentException("At least one creator promo kit artifact is required.", nameof(request));
        }

        var artifacts = request.Artifacts
            .Select((artifact, index) => NormalizeArtifact(artifact, index))
            .ToArray();
        var renderingId = request.RenderingId.Trim();
        var approvedManifestId = request.ApprovedManifestId.Trim();
        var manifestRevisionId = request.ManifestRevisionId.Trim();
        var source = request.Source.Trim();
        var normalizedRequest = request with
        {
            RenderingId = renderingId,
            ApprovedManifestId = approvedManifestId,
            ManifestRevisionId = manifestRevisionId,
            Source = source,
        };

        RequirePayloadScope(artifacts, normalizedRequest);
        RequireRole(artifacts, CreatorPromoKitArtifactRole.PromoVideo, request);
        RequireRole(artifacts, CreatorPromoKitArtifactRole.PromoPoster, request);
        RequireRole(artifacts, CreatorPromoKitArtifactRole.PreviewCard, request);
        RequireUniqueArtifactRefs(artifacts);

        return normalizedRequest with
        {
            Artifacts = artifacts
                .OrderBy(static artifact => artifact.Role)
                .ThenBy(static artifact => artifact.ArtifactRef, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.OutputFormat, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static artifact => artifact.DeduplicationKey, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static CreatorPromoKitArtifactRenderRequest NormalizeArtifact(
        CreatorPromoKitArtifactRenderRequest? artifact,
        int index)
    {
        if (artifact is null)
        {
            throw new ArgumentException(
                $"Creator promo kit artifacts[{index}] is required.",
                nameof(CreatorPromoKitRenderRequest.Artifacts));
        }

        RequireText(artifact.Category, nameof(artifact.Category));
        RequireText(artifact.Payload, nameof(artifact.Payload));
        RequireText(artifact.OutputFormat, nameof(artifact.OutputFormat));
        RequireText(artifact.ArtifactRef, nameof(artifact.ArtifactRef));
        RequireText(artifact.DeduplicationKey, nameof(artifact.DeduplicationKey));

        var captionRefs = NormalizeRefs(artifact.CaptionRefs, nameof(artifact.CaptionRefs));
        var previewRefs = NormalizeRefs(artifact.PreviewRefs, nameof(artifact.PreviewRefs));
        if (artifact.Role is CreatorPromoKitArtifactRole.PromoVideo && captionRefs.Count == 0)
        {
            throw new ArgumentException("Creator promo video artifacts require at least one caption ref.", nameof(artifact));
        }

        if ((artifact.Role is CreatorPromoKitArtifactRole.PromoVideo
                or CreatorPromoKitArtifactRole.PromoPoster
                or CreatorPromoKitArtifactRole.PreviewCard) &&
            previewRefs.Count == 0)
        {
            throw new ArgumentException("creator promo artifacts require at least one preview ref.", nameof(artifact));
        }

        return artifact with
        {
            Category = artifact.Category.Trim(),
            Payload = artifact.Payload.Trim(),
            OutputFormat = artifact.OutputFormat.Trim(),
            ArtifactRef = artifact.ArtifactRef.Trim(),
            CaptionRefs = captionRefs,
            PreviewRefs = previewRefs,
            DeduplicationKey = artifact.DeduplicationKey.Trim(),
        };
    }

    private static void RequireRole(
        IReadOnlyCollection<CreatorPromoKitArtifactRenderRequest> artifacts,
        CreatorPromoKitArtifactRole role,
        CreatorPromoKitRenderRequest request)
    {
        if (!artifacts.Any(artifact => artifact.Role == role))
        {
            throw new ArgumentException($"Creator promo kit rendering requires at least one {role} artifact.", nameof(request));
        }
    }

    private static void RequireUniqueArtifactRefs(IReadOnlyCollection<CreatorPromoKitArtifactRenderRequest> artifacts)
    {
        var duplicate = artifacts
            .GroupBy(static artifact => artifact.ArtifactRef, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Creator promo artifact refs must be unique per approved manifest: {duplicate.Key}.",
                nameof(CreatorPromoKitRenderRequest.Artifacts));
        }
    }

    private static void RequirePayloadScope(
        IReadOnlyCollection<CreatorPromoKitArtifactRenderRequest> artifacts,
        CreatorPromoKitRenderRequest request)
    {
        var missingApprovedManifestId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesApprovedManifestId(
                artifact.Payload,
                request.ApprovedManifestId));
        if (missingApprovedManifestId is not null)
        {
            throw new ArgumentException(
                "Creator promo kit payloads must stay scoped to the approved manifest id.",
                nameof(CreatorPromoKitRenderRequest.Artifacts));
        }

        var missingRevisionId = artifacts.FirstOrDefault(
            artifact => !PayloadMatchesRevisionId(
                artifact.Payload,
                request.ManifestRevisionId));
        if (missingRevisionId is not null)
        {
            throw new ArgumentException(
                "Creator promo kit payloads must stay scoped to the manifest revision id.",
                nameof(CreatorPromoKitRenderRequest.Artifacts));
        }
    }

    private static bool PayloadMatchesApprovedManifestId(string payload, string approvedManifestId)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.ApprovedManifestId, approvedManifestId, StringComparison.Ordinal);
        }

        string scopedApprovedManifestId;
        if (TryParseScopeFromTextPayload(payload, "approvedManifestId", out scopedApprovedManifestId))
        {
            return string.Equals(scopedApprovedManifestId, approvedManifestId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, approvedManifestId);
    }

    private static bool PayloadMatchesRevisionId(string payload, string manifestRevisionId)
    {
        var jsonScope = ParseJsonScopePayload(payload);
        if (jsonScope.IsJsonPayload)
        {
            return jsonScope.HasScopeFields &&
                   string.Equals(jsonScope.ManifestRevisionId, manifestRevisionId, StringComparison.Ordinal);
        }

        string scopedRevisionId;
        if (TryParseScopeFromTextPayload(payload, "manifestRevisionId", out scopedRevisionId))
        {
            return string.Equals(scopedRevisionId, manifestRevisionId, StringComparison.Ordinal);
        }

        return ContainsDelimitedScopeValue(payload, manifestRevisionId);
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

            if (!TryGetJsonStringProperty(document.RootElement, "approvedManifestId", out var approvedManifestId) ||
                !TryGetJsonStringProperty(document.RootElement, "manifestRevisionId", out var manifestRevisionId))
            {
                return JsonScopePayload.JsonPayloadMissingScopeFields;
            }

            return new JsonScopePayload(
                IsJsonPayload: true,
                HasScopeFields: true,
                ApprovedManifestId: approvedManifestId,
                ManifestRevisionId: manifestRevisionId);
        }
        catch (JsonException)
        {
            return JsonScopePayload.NotJson;
        }
    }

    private static bool TryGetJsonStringProperty(JsonElement element, string propertyName, out string value)
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
        bool HasScopeFields,
        string ApprovedManifestId,
        string ManifestRevisionId)
    {
        public static JsonScopePayload NotJson => new(
            IsJsonPayload: false,
            HasScopeFields: false,
            ApprovedManifestId: string.Empty,
            ManifestRevisionId: string.Empty);

        public static JsonScopePayload JsonPayloadMissingScopeFields => new(
            IsJsonPayload: true,
            HasScopeFields: false,
            ApprovedManifestId: string.Empty,
            ManifestRevisionId: string.Empty);
    }

    private static IReadOnlyList<CreatorPromoKitRoleReceiptGroup> BuildRoleReceiptGroups(
        IEnumerable<CreatorPromoKitArtifactReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => receipt.Role)
            .OrderBy(static group => group.Key)
            .Select(static group =>
            {
                var rows = group
                    .OrderBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.ReceiptId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new CreatorPromoKitRoleReceiptGroup(
                    Role: group.Key,
                    ReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.ReceiptId)),
                    JobIds: OrderedDistinct(rows.Select(static receipt => receipt.JobId)),
                    ArtifactRefs: OrderedDistinct(rows.Select(static receipt => receipt.ArtifactRef)),
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

    private static MediaRenderJobType ToJobType(CreatorPromoKitArtifactRole role) =>
        role switch
        {
            CreatorPromoKitArtifactRole.PromoVideo => MediaRenderJobType.CreatorPromoVideo,
            CreatorPromoKitArtifactRole.PromoPoster => MediaRenderJobType.CreatorPromoPoster,
            CreatorPromoKitArtifactRole.PreviewCard => MediaRenderJobType.CreatorPromoPreviewCard,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported creator promo kit artifact role.")
        };

    private static string BuildScopedDeduplicationKey(
        CreatorPromoKitRenderRequest request,
        CreatorPromoKitArtifactRenderRequest artifact)
    {
        var fields = new[]
        {
            "creator-promo-kit",
            request.ApprovedManifestId,
            request.ManifestRevisionId,
            request.RenderingId,
            artifact.Role.ToString(),
            artifact.Category,
            artifact.OutputFormat,
            artifact.ArtifactRef,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "creator-promo-kit:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildReceiptId(
        CreatorPromoKitRenderRequest request,
        CreatorPromoKitArtifactRenderRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(request, artifact),
            BuildRefHashSegment("caption", artifact.CaptionRefs),
            BuildRefHashSegment("preview", artifact.PreviewRefs));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "creator_promo_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
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
        IEnumerable<CreatorPromoKitArtifactReceipt> receipts,
        CreatorPromoKitArtifactRole role) =>
        receipts
            .Where(receipt => receipt.Role == role)
            .Select(static receipt => receipt.ReceiptId)
            .OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<CreatorPromoKitArtifactRefReceipt> BuildArtifactRefReceipts(
        IEnumerable<CreatorPromoKitArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new CreatorPromoKitArtifactRefReceipt(
                Ref: receipt.ArtifactRef,
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

    private static IReadOnlyList<CreatorPromoKitReadyRef> BuildReadyRefs(
        IEnumerable<CreatorPromoKitArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new CreatorPromoKitReadyRef(
                Ref: receipt.ArtifactRef,
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

    private static IReadOnlyList<CreatorPromoCaptionRefReceipt> BuildCaptionRefReceipts(
        IEnumerable<CreatorPromoKitArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.CaptionRefs.Select(captionRef => (captionRef, receipt)))
            .GroupBy(static item => item.captionRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new CreatorPromoCaptionRefReceipt(
                Ref: CanonicalizeGroupedRef(group.Select(static item => item.captionRef)),
                ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId)),
                JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId)),
                ArtifactRefs: OrderedDistinct(group.Select(static item => item.receipt.ArtifactRef)),
                Roles: group
                    .Select(static item => item.receipt.Role)
                    .Distinct()
                    .OrderBy(static role => role)
                    .ToArray(),
                ArtifactReceipts: BuildGroupedArtifactReceipts(group.Select(static item => item.receipt))))
            .ToArray();

    private static IReadOnlyList<CreatorPromoPreviewRefReceipt> BuildPreviewRefReceipts(
        IEnumerable<CreatorPromoKitArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.PreviewRefs.Select(previewRef => (previewRef, receipt)))
            .GroupBy(static item => item.previewRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new CreatorPromoPreviewRefReceipt(
                Ref: CanonicalizeGroupedRef(group.Select(static item => item.previewRef)),
                ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId)),
                JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId)),
                ArtifactRefs: OrderedDistinct(group.Select(static item => item.receipt.ArtifactRef)),
                Roles: group
                    .Select(static item => item.receipt.Role)
                    .Distinct()
                    .OrderBy(static role => role)
                    .ToArray(),
                ArtifactReceipts: BuildGroupedArtifactReceipts(group.Select(static item => item.receipt))))
            .ToArray();

    private static string CanonicalizeGroupedRef(IEnumerable<string> refs) =>
        OrderedDistinct(refs).First();

    private static IReadOnlyList<CreatorPromoKitGroupedArtifactReceipt> BuildGroupedArtifactReceipts(
        IEnumerable<CreatorPromoKitArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new CreatorPromoKitGroupedArtifactReceipt(
                ReceiptId: receipt.ReceiptId,
                Role: receipt.Role,
                Category: receipt.Category,
                ArtifactRef: receipt.ArtifactRef,
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

    private async Task<MediaRenderJobStatus> WaitForTerminalStatusAsync(string jobId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = _jobs.Get(jobId);
            if (status is null)
            {
                throw new InvalidOperationException($"Creator promo kit job {jobId} disappeared before receipt emission.");
            }

            if (status.State is MediaRenderJobState.Succeeded)
            {
                return status;
            }

            if (status.State is MediaRenderJobState.Failed or MediaRenderJobState.Expired)
            {
                throw new InvalidOperationException($"Creator promo kit job {jobId} ended as {status.State}: {status.Error ?? "unknown"}");
            }

            await Task.Delay(20, cancellationToken);
        }

        throw new TimeoutException($"Creator promo kit job {jobId} did not finish before receipt emission.");
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
                $"Creator promo kit job {status.JobId} succeeded without asset and lifecycle truth required for receipt emission.");
        }
    }

    private static DateTimeOffset MaxTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;
}
