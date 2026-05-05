using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace Chummer.Run.AI.Services.Assets;

public interface IReplayExchangePreviewRenderingService
{
    Task<ReplayExchangePreviewRenderReceipt> RenderAsync(
        ReplayExchangePreviewRenderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ReplayExchangePreviewRenderingService : IReplayExchangePreviewRenderingService
{
    private readonly IMediaRenderJobService _jobs;

    public ReplayExchangePreviewRenderingService(IMediaRenderJobService jobs)
    {
        _jobs = jobs;
    }

    public async Task<ReplayExchangePreviewRenderReceipt> RenderAsync(
        ReplayExchangePreviewRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var receipts = new List<ReplayExchangePreviewArtifactReceipt>(normalized.Bundles.Count * 2);
        DateTimeOffset? renderedAtUtc = null;

        foreach (var bundle in normalized.Bundles)
        {
            foreach (var artifact in EnumerateArtifacts(bundle))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var enqueued = await _jobs.EnqueueAsync(
                    new MediaRenderJobEnqueueRequest(
                        JobType: ToJobType(bundle.BundleKind, artifact.Role),
                        DeduplicationKey: BuildScopedDeduplicationKey(bundle, artifact),
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

                receipts.Add(new ReplayExchangePreviewArtifactReceipt(
                    ReceiptId: BuildReceiptId(bundle, artifact),
                    BundleKind: bundle.BundleKind,
                    Role: artifact.Role,
                    BundleRef: bundle.BundleRef,
                    ArtifactRef: artifact.ArtifactRef,
                    LineageRef: bundle.LineageRef,
                    CompatibilityReceiptId: bundle.CompatibilityReceiptId,
                    ProvenanceReceiptId: bundle.ProvenanceReceiptId,
                    BoundedLossReceiptId: bundle.BoundedLossReceiptId,
                    Category: artifact.Category,
                    OutputFormat: artifact.OutputFormat,
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
        }

        return new ReplayExchangePreviewRenderReceipt(
            RenderingId: normalized.RenderingId,
            Source: normalized.Source,
            RequestedAtUtc: normalized.RequestedAtUtc,
            RenderedAtUtc: renderedAtUtc ?? normalized.RequestedAtUtc,
            Artifacts: receipts,
            BundleReceipts: BuildBundleReceipts(normalized.Bundles, receipts),
            KindReceiptGroups: BuildKindReceiptGroups(receipts),
            PreviewCardReceiptIds: ReceiptIdsFor(receipts, ReplayExchangePreviewArtifactRole.PreviewCard),
            InspectableSiblingReceiptIds: ReceiptIdsFor(receipts, ReplayExchangePreviewArtifactRole.InspectableSibling),
            BundleRefs: OrderedDistinct(receipts.Select(static receipt => receipt.BundleRef)),
            LineageRefs: OrderedDistinct(receipts.Select(static receipt => receipt.LineageRef)),
            CompatibilityReceiptIds: OrderedDistinct(receipts.Select(static receipt => receipt.CompatibilityReceiptId)),
            ProvenanceReceiptIds: OrderedDistinct(receipts.Select(static receipt => receipt.ProvenanceReceiptId)),
            BoundedLossReceiptIds: OrderedDistinct(receipts.Select(static receipt => receipt.BoundedLossReceiptId)),
            JobIds: OrderedDistinct(receipts.Select(static receipt => receipt.JobId)),
            ReadyRefs: BuildReadyRefs(receipts),
            ArtifactRefReceipts: BuildArtifactRefReceipts(receipts),
            CaptionRefReceipts: BuildCaptionRefReceipts(receipts),
            PreviewRefReceipts: BuildPreviewRefReceipts(receipts));
    }

    private static ReplayExchangePreviewRenderRequest Normalize(ReplayExchangePreviewRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RenderingId, nameof(request.RenderingId));
        RequireText(request.Source, nameof(request.Source));
        if (request.Bundles is null)
        {
            throw new ArgumentNullException(nameof(ReplayExchangePreviewRenderRequest.Bundles));
        }

        if (request.Bundles.Count == 0)
        {
            throw new ArgumentException("At least one replay, recap, or exchange preview bundle is required.", nameof(request));
        }

        var bundles = request.Bundles
            .Select((bundle, index) => NormalizeBundle(bundle, index))
            .ToArray();
        RequireBundleKind(bundles, ReplayExchangePreviewBundleKind.Recap, request);
        RequireBundleKind(bundles, ReplayExchangePreviewBundleKind.Replay, request);
        RequireBundleKind(bundles, ReplayExchangePreviewBundleKind.Exchange, request);
        RequireUniqueBundleRefs(bundles);
        RequireUniqueArtifactRefs(bundles);

        return request with
        {
            RenderingId = request.RenderingId.Trim(),
            Source = request.Source.Trim(),
            Bundles = bundles
                .OrderBy(static bundle => bundle.BundleKind)
                .ThenBy(static bundle => bundle.BundleRef, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static bundle => bundle.LineageRef, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static ReplayExchangePreviewBundleRenderRequest NormalizeBundle(
        ReplayExchangePreviewBundleRenderRequest? bundle,
        int index)
    {
        if (bundle is null)
        {
            throw new ArgumentException(
                $"Replay/exchange preview bundles[{index}] is required.",
                nameof(ReplayExchangePreviewRenderRequest.Bundles));
        }

        RequireText(bundle.BundleRef, nameof(bundle.BundleRef));
        RequireText(bundle.LineageRef, nameof(bundle.LineageRef));
        RequireText(bundle.CompatibilityReceiptId, nameof(bundle.CompatibilityReceiptId));
        RequireText(bundle.ProvenanceReceiptId, nameof(bundle.ProvenanceReceiptId));
        RequireText(bundle.BoundedLossReceiptId, nameof(bundle.BoundedLossReceiptId));
        ArgumentNullException.ThrowIfNull(bundle.PreviewCard, nameof(bundle.PreviewCard));
        ArgumentNullException.ThrowIfNull(bundle.InspectableSibling, nameof(bundle.InspectableSibling));

        var previewCard = NormalizeArtifact(bundle.PreviewCard, ReplayExchangePreviewArtifactRole.PreviewCard);
        var inspectableSibling = NormalizeArtifact(bundle.InspectableSibling, ReplayExchangePreviewArtifactRole.InspectableSibling);

        if (previewCard.Role != ReplayExchangePreviewArtifactRole.PreviewCard)
        {
            throw new ArgumentException("Replay/exchange preview bundles must carry a PreviewCard artifact in PreviewCard.", nameof(bundle));
        }

        if (inspectableSibling.Role != ReplayExchangePreviewArtifactRole.InspectableSibling)
        {
            throw new ArgumentException("Replay/exchange preview bundles must carry an InspectableSibling artifact in InspectableSibling.", nameof(bundle));
        }

        return bundle with
        {
            BundleRef = bundle.BundleRef.Trim(),
            LineageRef = bundle.LineageRef.Trim(),
            CompatibilityReceiptId = bundle.CompatibilityReceiptId.Trim(),
            ProvenanceReceiptId = bundle.ProvenanceReceiptId.Trim(),
            BoundedLossReceiptId = bundle.BoundedLossReceiptId.Trim(),
            PreviewCard = previewCard,
            InspectableSibling = inspectableSibling
        };
    }

    private static ReplayExchangePreviewArtifactRenderRequest NormalizeArtifact(
        ReplayExchangePreviewArtifactRenderRequest? artifact,
        ReplayExchangePreviewArtifactRole expectedRole)
    {
        if (artifact is null)
        {
            throw new ArgumentException($"Replay/exchange preview {expectedRole} artifact is required.", nameof(ReplayExchangePreviewRenderRequest.Bundles));
        }

        RequireText(artifact.Category, nameof(artifact.Category));
        RequireText(artifact.Payload, nameof(artifact.Payload));
        RequireText(artifact.OutputFormat, nameof(artifact.OutputFormat));
        RequireText(artifact.ArtifactRef, nameof(artifact.ArtifactRef));
        RequireText(artifact.DeduplicationKey, nameof(artifact.DeduplicationKey));
        var captionRefs = NormalizeRefs(artifact.CaptionRefs, nameof(artifact.CaptionRefs));
        var previewRefs = NormalizeRefs(artifact.PreviewRefs, nameof(artifact.PreviewRefs));
        if (previewRefs.Count == 0)
        {
            throw new ArgumentException("Replay/exchange preview artifacts require at least one preview ref.", nameof(artifact));
        }

        return artifact with
        {
            Role = expectedRole,
            Category = artifact.Category.Trim(),
            Payload = artifact.Payload.Trim(),
            OutputFormat = artifact.OutputFormat.Trim(),
            ArtifactRef = artifact.ArtifactRef.Trim(),
            CaptionRefs = captionRefs,
            PreviewRefs = previewRefs,
            DeduplicationKey = artifact.DeduplicationKey.Trim()
        };
    }

    private static void RequireBundleKind(
        IReadOnlyCollection<ReplayExchangePreviewBundleRenderRequest> bundles,
        ReplayExchangePreviewBundleKind bundleKind,
        ReplayExchangePreviewRenderRequest request)
    {
        if (!bundles.Any(bundle => bundle.BundleKind == bundleKind))
        {
            throw new ArgumentException($"Replay/exchange preview rendering requires at least one {bundleKind} bundle.", nameof(request));
        }
    }

    private static void RequireUniqueBundleRefs(IReadOnlyCollection<ReplayExchangePreviewBundleRenderRequest> bundles)
    {
        var duplicate = bundles
            .GroupBy(static bundle => bundle.BundleRef, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Replay/exchange bundle refs must be unique per render request: {duplicate.Key}.",
                nameof(ReplayExchangePreviewRenderRequest.Bundles));
        }
    }

    private static void RequireUniqueArtifactRefs(IReadOnlyCollection<ReplayExchangePreviewBundleRenderRequest> bundles)
    {
        var duplicate = bundles
            .SelectMany(EnumerateArtifacts)
            .GroupBy(static artifact => artifact.ArtifactRef, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Replay/exchange preview artifact refs must be unique per render request: {duplicate.Key}.",
                nameof(ReplayExchangePreviewRenderRequest.Bundles));
        }
    }

    private static IEnumerable<ReplayExchangePreviewArtifactRenderRequest> EnumerateArtifacts(
        ReplayExchangePreviewBundleRenderRequest bundle)
    {
        yield return bundle.PreviewCard;
        yield return bundle.InspectableSibling;
    }

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
        ReplayExchangePreviewBundleKind bundleKind,
        ReplayExchangePreviewArtifactRole role) =>
        (bundleKind, role) switch
        {
            (ReplayExchangePreviewBundleKind.Recap, ReplayExchangePreviewArtifactRole.PreviewCard) => MediaRenderJobType.RecapPreviewCard,
            (ReplayExchangePreviewBundleKind.Recap, ReplayExchangePreviewArtifactRole.InspectableSibling) => MediaRenderJobType.RecapInspectableSibling,
            (ReplayExchangePreviewBundleKind.Replay, ReplayExchangePreviewArtifactRole.PreviewCard) => MediaRenderJobType.ReplayPreviewCard,
            (ReplayExchangePreviewBundleKind.Replay, ReplayExchangePreviewArtifactRole.InspectableSibling) => MediaRenderJobType.ReplayInspectableSibling,
            (ReplayExchangePreviewBundleKind.Exchange, ReplayExchangePreviewArtifactRole.PreviewCard) => MediaRenderJobType.ExchangePreviewCard,
            (ReplayExchangePreviewBundleKind.Exchange, ReplayExchangePreviewArtifactRole.InspectableSibling) => MediaRenderJobType.ExchangeInspectableSibling,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported replay/exchange preview artifact role.")
        };

    private static string BuildScopedDeduplicationKey(
        ReplayExchangePreviewBundleRenderRequest bundle,
        ReplayExchangePreviewArtifactRenderRequest artifact)
    {
        var fields = new[]
        {
            "replay-exchange-preview",
            bundle.BundleKind.ToString(),
            bundle.BundleRef,
            bundle.LineageRef,
            bundle.CompatibilityReceiptId,
            bundle.ProvenanceReceiptId,
            bundle.BoundedLossReceiptId,
            artifact.Role.ToString(),
            artifact.Category,
            artifact.OutputFormat,
            artifact.ArtifactRef,
            artifact.DeduplicationKey
        };
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "replay-exchange-preview:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildReceiptId(
        ReplayExchangePreviewBundleRenderRequest bundle,
        ReplayExchangePreviewArtifactRenderRequest artifact)
    {
        var input = string.Join(
            "\n",
            BuildScopedDeduplicationKey(bundle, artifact),
            BuildRefHashSegment("caption", artifact.CaptionRefs),
            BuildRefHashSegment("preview", artifact.PreviewRefs));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "replay_exchange_preview_receipt_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildBundleReceiptId(
        ReplayExchangePreviewBundleRenderRequest bundle,
        IReadOnlyList<ReplayExchangePreviewArtifactReceipt> artifactReceipts)
    {
        var fields = new[]
        {
            "replay-exchange-bundle",
            bundle.BundleKind.ToString(),
            bundle.BundleRef,
            bundle.LineageRef,
            bundle.CompatibilityReceiptId,
            bundle.ProvenanceReceiptId,
            bundle.BoundedLossReceiptId,
        }.Concat(artifactReceipts.Select(static receipt => receipt.ReceiptId));
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "replay_exchange_bundle_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildKindReceiptGroupId(
        ReplayExchangePreviewBundleKind bundleKind,
        IReadOnlyList<ReplayExchangePreviewArtifactReceipt> receipts)
    {
        var fields = new[] { "replay-exchange-kind", bundleKind.ToString() }
            .Concat(receipts.Select(static receipt => receipt.ReceiptId));
        var input = string.Join("\n", fields.Select(static field => $"{field.Length}:{field}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "replay_exchange_kind_" + Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static string BuildRefHashSegment(string prefix, IReadOnlyList<string> refs) =>
        string.Join(
            "\n",
            new[] { $"{prefix}:{refs.Count}" }
                .Concat(refs.Select(static value => $"{value.Length}:{value}")));

    private static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> values) =>
        values
            .GroupBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .Select(static group => CanonicalizeDistinctValue(group))
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    private static string CanonicalizeDistinctValue(IEnumerable<string> values) =>
        values
            .OrderBy(static value => CountUppercaseLetters(value))
            .ThenByDescending(static value => CountLowercaseLetters(value))
            .ThenBy(static value => value, StringComparer.Ordinal)
            .First();

    private static int CountUppercaseLetters(string value) =>
        value.Count(static ch => char.IsUpper(ch));

    private static int CountLowercaseLetters(string value) =>
        value.Count(static ch => char.IsLower(ch));

    private static IReadOnlyList<string> ReceiptIdsFor(
        IEnumerable<ReplayExchangePreviewArtifactReceipt> receipts,
        ReplayExchangePreviewArtifactRole role) =>
        receipts
            .Where(receipt => receipt.Role == role)
            .Select(static receipt => receipt.ReceiptId)
            .OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<ReplayExchangePreviewBundleReceipt> BuildBundleReceipts(
        IReadOnlyList<ReplayExchangePreviewBundleRenderRequest> bundles,
        IReadOnlyList<ReplayExchangePreviewArtifactReceipt> receipts) =>
        bundles
            .Select(bundle =>
            {
                var bundleReceipts = receipts
                    .Where(receipt =>
                        receipt.BundleKind == bundle.BundleKind &&
                        string.Equals(receipt.BundleRef, bundle.BundleRef, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(static receipt => receipt.Role)
                    .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new ReplayExchangePreviewBundleReceipt(
                    ReceiptId: BuildBundleReceiptId(bundle, bundleReceipts),
                    BundleKind: bundle.BundleKind,
                    BundleRef: bundle.BundleRef,
                    LineageRef: bundle.LineageRef,
                    CompatibilityReceiptId: bundle.CompatibilityReceiptId,
                    ProvenanceReceiptId: bundle.ProvenanceReceiptId,
                    BoundedLossReceiptId: bundle.BoundedLossReceiptId,
                    PreviewCardReceiptId: bundleReceipts.Single(static receipt => receipt.Role == ReplayExchangePreviewArtifactRole.PreviewCard).ReceiptId,
                    InspectableSiblingReceiptId: bundleReceipts.Single(static receipt => receipt.Role == ReplayExchangePreviewArtifactRole.InspectableSibling).ReceiptId,
                    JobIds: OrderedDistinct(bundleReceipts.Select(static receipt => receipt.JobId)),
                    ArtifactReceipts: bundleReceipts);
            })
            .OrderBy(static receipt => receipt.BundleKind)
            .ThenBy(static receipt => receipt.BundleRef, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<ReplayExchangePreviewKindReceiptGroup> BuildKindReceiptGroups(
        IEnumerable<ReplayExchangePreviewArtifactReceipt> receipts) =>
        receipts
            .GroupBy(static receipt => receipt.BundleKind)
            .OrderBy(static group => group.Key)
            .Select(static group =>
            {
                var rows = group
                    .OrderBy(static receipt => receipt.BundleRef, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static receipt => receipt.Role)
                    .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new ReplayExchangePreviewKindReceiptGroup(
                    ReceiptId: BuildKindReceiptGroupId(group.Key, rows),
                    BundleKind: group.Key,
                    BundleRefs: OrderedDistinct(rows.Select(static receipt => receipt.BundleRef)),
                    LineageRefs: OrderedDistinct(rows.Select(static receipt => receipt.LineageRef)),
                    CompatibilityReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.CompatibilityReceiptId)),
                    ProvenanceReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.ProvenanceReceiptId)),
                    BoundedLossReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.BoundedLossReceiptId)),
                    BundleReceiptIds: OrderedDistinct(rows.GroupBy(static receipt => (receipt.BundleKind, receipt.BundleRef)).Select(static bundle => bundle.First().BundleRef + "|" + bundle.Key.BundleKind)),
                    ArtifactRefs: OrderedDistinct(rows.Select(static receipt => receipt.ArtifactRef)),
                    ReceiptIds: OrderedDistinct(rows.Select(static receipt => receipt.ReceiptId)),
                    JobIds: OrderedDistinct(rows.Select(static receipt => receipt.JobId)),
                    ArtifactReceipts: rows);
            })
            .ToArray();

    private static IReadOnlyList<ReplayExchangePreviewReadyRef> BuildReadyRefs(
        IEnumerable<ReplayExchangePreviewArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.BundleKind)
            .ThenBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new ReplayExchangePreviewReadyRef(
                Ref: receipt.ArtifactRef,
                BundleKind: receipt.BundleKind,
                Role: receipt.Role,
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

    private static IReadOnlyList<ReplayExchangePreviewArtifactRefReceipt> BuildArtifactRefReceipts(
        IEnumerable<ReplayExchangePreviewArtifactReceipt> receipts) =>
        receipts
            .OrderBy(static receipt => receipt.BundleKind)
            .ThenBy(static receipt => receipt.Role)
            .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
            .Select(static receipt => new ReplayExchangePreviewArtifactRefReceipt(
                Ref: receipt.ArtifactRef,
                BundleKind: receipt.BundleKind,
                Role: receipt.Role,
                ReceiptId: receipt.ReceiptId,
                JobId: receipt.JobId,
                CaptionRefs: receipt.CaptionRefs,
                PreviewRefs: receipt.PreviewRefs,
                AssetId: receipt.AssetId,
                AssetUrl: receipt.AssetUrl))
            .ToArray();

    private static IReadOnlyList<ReplayExchangePreviewCaptionRefReceipt> BuildCaptionRefReceipts(
        IEnumerable<ReplayExchangePreviewArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.CaptionRefs.Select(captionRef => (captionRef, receipt)))
            .GroupBy(static item => item.captionRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new ReplayExchangePreviewCaptionRefReceipt(
                Ref: CanonicalizeGroupedRef(group.Select(static item => item.captionRef)),
                ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId)),
                JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId)),
                ArtifactRefs: OrderedDistinct(group.Select(static item => item.receipt.ArtifactRef)),
                ArtifactReceipts: group
                    .Select(static item => item.receipt)
                    .OrderBy(static receipt => receipt.BundleKind)
                    .ThenBy(static receipt => receipt.Role)
                    .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

    private static IReadOnlyList<ReplayExchangePreviewPreviewRefReceipt> BuildPreviewRefReceipts(
        IEnumerable<ReplayExchangePreviewArtifactReceipt> receipts) =>
        receipts
            .SelectMany(static receipt => receipt.PreviewRefs.Select(previewRef => (previewRef, receipt)))
            .GroupBy(static item => item.previewRef, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new ReplayExchangePreviewPreviewRefReceipt(
                Ref: CanonicalizeGroupedRef(group.Select(static item => item.previewRef)),
                ReceiptIds: OrderedDistinct(group.Select(static item => item.receipt.ReceiptId)),
                JobIds: OrderedDistinct(group.Select(static item => item.receipt.JobId)),
                ArtifactRefs: OrderedDistinct(group.Select(static item => item.receipt.ArtifactRef)),
                ArtifactReceipts: group
                    .Select(static item => item.receipt)
                    .OrderBy(static receipt => receipt.BundleKind)
                    .ThenBy(static receipt => receipt.Role)
                    .ThenBy(static receipt => receipt.ArtifactRef, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

    private static string CanonicalizeGroupedRef(IEnumerable<string> refs) =>
        OrderedDistinct(refs).First();

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
                throw new InvalidOperationException($"Replay/exchange preview job {jobId} disappeared before receipt emission.");
            }

            if (status.State is MediaRenderJobState.Succeeded)
            {
                return status;
            }

            if (status.State is MediaRenderJobState.Failed or MediaRenderJobState.Expired)
            {
                throw new InvalidOperationException($"Replay/exchange preview job {jobId} ended as {status.State}: {status.Error ?? "unknown"}");
            }

            await Task.Delay(20, cancellationToken);
        }

        throw new TimeoutException($"Replay/exchange preview job {jobId} did not finish before receipt emission.");
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
                $"Replay/exchange preview job {status.JobId} succeeded without asset and lifecycle truth required for receipt emission.");
        }
    }

    private static DateTimeOffset MaxTimestamp(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;
}
