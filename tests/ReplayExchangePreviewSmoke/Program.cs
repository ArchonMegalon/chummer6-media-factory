using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var previews = new ReplayExchangePreviewRenderingService(jobs);

var request = new ReplayExchangePreviewRenderRequest(
    RenderingId: "replay-exchange-preview-render-001",
    Source: "replay-exchange-preview-smoke",
    RequestedAtUtc: DateTimeOffset.UtcNow,
    Bundles:
    [
        CreateBundle(
            ReplayExchangePreviewBundleKind.Recap,
            "artifact://recap/main",
            "lineage://recap/001",
            "compat://recap/001",
            "prov://recap/001",
            "loss://recap/001",
            "preview://shared/card",
            "caption://shared/en-US.vtt"),
        CreateBundle(
            ReplayExchangePreviewBundleKind.Replay,
            "artifact://replay/main",
            "lineage://replay/001",
            "compat://replay/001",
            "prov://replay/001",
            "loss://replay/001",
            "preview://shared/card",
            "caption://replay/en-US.vtt"),
        CreateBundle(
            ReplayExchangePreviewBundleKind.Exchange,
            "artifact://exchange/main",
            "lineage://exchange/001",
            "compat://exchange/001",
            "prov://exchange/001",
            "loss://exchange/001",
            "preview://exchange/card",
            "caption://shared/en-US.vtt")
    ]);

var receipt = await previews.RenderAsync(request);
Assert(receipt.Artifacts.Count == 6, "Replay/exchange preview rendering should receipt each requested sibling.");
Assert(receipt.BundleReceipts.Count == 3, "Replay/exchange preview rendering should publish one bundle receipt per bundle.");
Assert(receipt.KindReceiptGroups.Count == 3, "Replay/exchange preview rendering should publish one kind receipt group per bundle kind.");
Assert(receipt.PreviewCardReceiptIds.Count == 3, "Replay/exchange preview rendering should publish one preview-card receipt per bundle.");
Assert(receipt.InspectableSiblingReceiptIds.Count == 3, "Replay/exchange preview rendering should publish one inspectable-sibling receipt per bundle.");
Assert(receipt.BundleRefs.Count == 3, "Replay/exchange preview rendering should preserve every bundle ref.");
Assert(receipt.LineageRefs.Count == 3, "Replay/exchange preview rendering should preserve every lineage ref.");
Assert(receipt.JobIds.Count == 6, "Replay/exchange preview rendering should expose every media job id directly.");
Assert(receipt.ReadyRefs.Count == 6, "Replay/exchange preview rendering should publish one ready ref per artifact.");
Assert(receipt.ArtifactRefReceipts.Count == 6, "Replay/exchange preview rendering should publish one first-class artifact ref receipt per artifact.");
Assert(receipt.Artifacts.All(static artifact => artifact.JobState == MediaRenderJobState.Succeeded), "Replay/exchange preview receipts must wait for completed media jobs.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetId)), "Replay/exchange preview receipts must preserve concrete asset ids.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl)), "Replay/exchange preview receipts must preserve concrete asset urls.");
Assert(receipt.Artifacts.All(static artifact => artifact.ApprovalState == AssetApprovalState.Approved), "Replay/exchange preview receipts must preserve asset approval truth.");
Assert(receipt.Artifacts.All(static artifact => artifact.RetentionState == AssetRetentionState.CacheOnly), "Replay/exchange preview receipts must preserve asset retention truth.");
Assert(receipt.Artifacts.All(static artifact => artifact.StorageClass == AssetStorageClass.ObjectStorage), "Replay/exchange preview receipts must preserve asset storage truth.");
Assert(receipt.BundleReceipts.All(static bundle => bundle.JobIds.Count == 2 && bundle.ArtifactReceipts.Count == 2), "Replay/exchange preview bundle receipts must preserve both sibling jobs and artifact rows.");
Assert(receipt.BundleReceipts.Any(static bundle => bundle.BundleKind == ReplayExchangePreviewBundleKind.Recap && bundle.PreviewCardReceiptId != bundle.InspectableSiblingReceiptId), "Replay/exchange preview bundle receipts must keep preview-card and sibling receipt ids distinct.");
Assert(receipt.KindReceiptGroups.All(static group => group.BundleRefs.Count == 1 && group.ReceiptIds.Count == 2 && group.JobIds.Count == 2), "Replay/exchange preview kind groups must preserve bundle refs, receipt ids, and job ids.");
Assert(receipt.CaptionRefReceipts.Any(static row => row.Ref == "caption://shared/en-US.vtt" && row.ReceiptIds.Count == 2), "Shared replay/exchange caption refs must preserve aggregate receipt ids.");
Assert(receipt.PreviewRefReceipts.Any(static row => row.Ref == "preview://shared/card" && row.JobIds.Count == 4), "Shared replay/exchange preview refs must preserve aggregate job ids.");
Assert(receipt.ReadyRefs.All(static row => !string.IsNullOrWhiteSpace(row.Ref) && !string.IsNullOrWhiteSpace(row.ReceiptId) && !string.IsNullOrWhiteSpace(row.JobId) && !string.IsNullOrWhiteSpace(row.AssetId) && !string.IsNullOrWhiteSpace(row.AssetUrl)), "Replay/exchange preview ready refs must preserve ref, receipt, job, asset id, and asset url.");
Assert(receipt.ArtifactRefReceipts.All(static row => !string.IsNullOrWhiteSpace(row.Ref) && !string.IsNullOrWhiteSpace(row.ReceiptId) && !string.IsNullOrWhiteSpace(row.JobId) && !string.IsNullOrWhiteSpace(row.AssetId) && !string.IsNullOrWhiteSpace(row.AssetUrl)), "Replay/exchange preview artifact ref receipts must preserve ref, receipt, job, asset id, and asset url.");
Assert(receipt.ReadyRefs.Any(static row => row.BundleKind == ReplayExchangePreviewBundleKind.Replay && row.Role == ReplayExchangePreviewArtifactRole.InspectableSibling && row.CaptionRefs.Count == 1 && row.PreviewRefs.Count == 1), "Replay inspectable siblings must preserve caption and preview refs.");
Assert(receipt.ArtifactRefReceipts.Any(static row => row.BundleKind == ReplayExchangePreviewBundleKind.Exchange && row.Role == ReplayExchangePreviewArtifactRole.PreviewCard && row.CaptionRefs.Count == 0 && row.PreviewRefs.Count == 1), "Exchange preview cards must preserve preview refs without inventing caption refs.");

foreach (var artifact in receipt.Artifacts)
{
    await WaitForSucceededJobAsync(jobs, artifact.JobId);
}

var replayed = await previews.RenderAsync(request);
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(replayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep replay/exchange preview jobs stable.");
Assert(receipt.RenderedAtUtc == replayed.RenderedAtUtc, "Replay-safe dedupe should keep replay/exchange preview rendered timestamps stable.");

var metadataReplayed = await previews.RenderAsync(request with
{
    Source = "replay-exchange-preview-smoke-replayed",
    RequestedAtUtc = request.RequestedAtUtc.AddHours(4)
});
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(metadataReplayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay/exchange preview job ids should stay stable when only source and requested timestamps drift.");
Assert(
    receipt.Artifacts.Select(static artifact => artifact.ReceiptId).SequenceEqual(metadataReplayed.Artifacts.Select(static artifact => artifact.ReceiptId)),
    "Replay/exchange preview receipt ids should stay stable when only source and requested timestamps drift.");
Assert(
    receipt.ReadyRefs.Select(static row => (row.Ref, row.ReceiptId, row.JobId)).SequenceEqual(metadataReplayed.ReadyRefs.Select(static row => (row.Ref, row.ReceiptId, row.JobId))),
    "Replay/exchange preview ready refs should stay stable when only source and requested timestamps drift.");

var reordered = await previews.RenderAsync(request with
{
    Bundles =
    [
        request.Bundles[2] with
        {
            PreviewCard = request.Bundles[2].PreviewCard with { PreviewRefs = [" preview://exchange/card ", "PREVIEW://EXCHANGE/CARD"] },
            InspectableSibling = request.Bundles[2].InspectableSibling with { CaptionRefs = [" caption://shared/en-US.vtt ", "CAPTION://SHARED/EN-US.VTT"] }
        },
        request.Bundles[0] with
        {
            PreviewCard = request.Bundles[0].PreviewCard with { PreviewRefs = ["PREVIEW://SHARED/CARD", " preview://shared/card "] },
            InspectableSibling = request.Bundles[0].InspectableSibling with { CaptionRefs = [" caption://shared/en-US.vtt ", "CAPTION://SHARED/EN-US.VTT"] }
        },
        request.Bundles[1]
    ]
});
Assert(receipt.PreviewCardReceiptIds.SequenceEqual(reordered.PreviewCardReceiptIds), "Preview-card receipt ids should stay stable when callers reorder replay/exchange preview bundles.");
Assert(receipt.InspectableSiblingReceiptIds.SequenceEqual(reordered.InspectableSiblingReceiptIds), "Inspectable-sibling receipt ids should stay stable when callers reorder replay/exchange preview bundles.");
Assert(receipt.ReadyRefs.Select(static row => row.ReceiptId).SequenceEqual(reordered.ReadyRefs.Select(static row => row.ReceiptId)), "Ready refs should stay stable when callers reorder replay/exchange preview bundles.");
Assert(receipt.CaptionRefReceipts.Select(static row => row.Ref).SequenceEqual(reordered.CaptionRefReceipts.Select(static row => row.Ref)), "Caption ref receipt rows should stay stable when callers reorder replay/exchange preview bundles.");
Assert(receipt.PreviewRefReceipts.Select(static row => row.Ref).SequenceEqual(reordered.PreviewRefReceipts.Select(static row => row.Ref)), "Preview ref receipt rows should stay stable when callers reorder replay/exchange preview bundles.");
Assert(receipt.KindReceiptGroups.Select(static row => row.ReceiptId).SequenceEqual(reordered.KindReceiptGroups.Select(static row => row.ReceiptId)), "Kind receipt groups should stay stable when callers reorder replay/exchange preview bundles.");

var collisionReceipt = await previews.RenderAsync(request with
{
    RenderingId = "replay-exchange-preview-render-collision-proof",
    Bundles =
    [
        .. request.Bundles,
        request.Bundles[0] with
        {
            BundleRef = "artifact://recap/variant",
            PreviewCard = request.Bundles[0].PreviewCard with
            {
                OutputFormat = "webp",
                ArtifactRef = "artifact://recap/variant/preview",
                PreviewRefs = ["preview://recap/variant-card"],
                DeduplicationKey = request.Bundles[0].PreviewCard.DeduplicationKey
            },
            InspectableSibling = request.Bundles[0].InspectableSibling with
            {
                ArtifactRef = "artifact://recap/variant/sibling",
                PreviewRefs = ["preview://recap/variant-card"],
                DeduplicationKey = request.Bundles[0].InspectableSibling.DeduplicationKey
            }
        }
    ]
});
var collidingPreviewJobs = collisionReceipt.Artifacts
    .Where(static artifact => artifact.BundleKind == ReplayExchangePreviewBundleKind.Recap && artifact.Role == ReplayExchangePreviewArtifactRole.PreviewCard)
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(collidingPreviewJobs.Length == 2, "Different replay/exchange preview output refs must not collapse onto one preview render job when request dedupe keys collide.");

var receiptDelimiterCollision = await previews.RenderAsync(request with
{
    RenderingId = "replay-exchange-preview-render-receipt-collision-proof",
    Bundles =
    [
        request.Bundles[0] with
        {
            BundleRef = "artifact://recap/receipt-delimiter-a",
            PreviewCard = request.Bundles[0].PreviewCard with
            {
                OutputFormat = "gif",
                ArtifactRef = "artifact://recap/receipt-delimiter/a",
                CaptionRefs = ["caption", "variant|one"],
                PreviewRefs = ["preview://recap/receipt/card-a"],
                DeduplicationKey = "recap-preview-receipt-a"
            },
            InspectableSibling = request.Bundles[0].InspectableSibling with
            {
                ArtifactRef = "artifact://recap/receipt-delimiter/a/sibling",
                PreviewRefs = ["preview://recap/receipt/card-a"],
                DeduplicationKey = "recap-sibling-receipt-a"
            }
        },
        request.Bundles[0] with
        {
            BundleRef = "artifact://recap/receipt-delimiter-b",
            PreviewCard = request.Bundles[0].PreviewCard with
            {
                OutputFormat = "bmp",
                ArtifactRef = "artifact://recap/receipt-delimiter/b",
                CaptionRefs = ["caption|variant", "one"],
                PreviewRefs = ["preview://recap/receipt/card-b"],
                DeduplicationKey = "recap-preview-receipt-b"
            },
            InspectableSibling = request.Bundles[0].InspectableSibling with
            {
                ArtifactRef = "artifact://recap/receipt-delimiter/b/sibling",
                PreviewRefs = ["preview://recap/receipt/card-b"],
                DeduplicationKey = "recap-sibling-receipt-b"
            }
        },
        request.Bundles[1],
        request.Bundles[2]
    ]
});
var delimiterReceiptIds = receiptDelimiterCollision.Artifacts
    .Where(static artifact => artifact.ArtifactRef.StartsWith("artifact://recap/receipt-delimiter/", StringComparison.OrdinalIgnoreCase) && artifact.Role == ReplayExchangePreviewArtifactRole.PreviewCard)
    .Select(static artifact => artifact.ReceiptId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(delimiterReceiptIds.Length == 2, "Delimiter-heavy replay/exchange preview caption refs must not collapse onto one receipt id.");

try
{
    await previews.RenderAsync(request with
    {
        RenderingId = "replay-exchange-preview-duplicate-bundle-ref",
        Bundles =
        [
            request.Bundles[0],
            request.Bundles[1] with { BundleRef = request.Bundles[0].BundleRef },
            request.Bundles[2]
        ]
    });
    throw new InvalidOperationException("Duplicate replay/exchange bundle ref validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("bundle refs must be unique", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await previews.RenderAsync(request with
    {
        RenderingId = "replay-exchange-preview-duplicate-artifact-ref",
        Bundles =
        [
            request.Bundles[0] with
            {
                InspectableSibling = request.Bundles[0].InspectableSibling with { ArtifactRef = request.Bundles[0].PreviewCard.ArtifactRef }
            },
            request.Bundles[1],
            request.Bundles[2]
        ]
    });
    throw new InvalidOperationException("Duplicate replay/exchange preview artifact ref validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("artifact refs must be unique", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await previews.RenderAsync(request with
    {
        RenderingId = "replay-exchange-preview-missing-kind",
        Bundles =
        [
            request.Bundles[0],
            request.Bundles[1]
        ]
    });
    throw new InvalidOperationException("Replay/exchange preview missing-kind validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("Exchange bundle", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await previews.RenderAsync(request with
    {
        RenderingId = "replay-exchange-preview-null-bundle-entry",
        Bundles =
        [
            request.Bundles[0],
            null!,
            request.Bundles[2]
        ]
    });
    throw new InvalidOperationException("Null replay/exchange preview bundle entry validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("bundles[1] is required", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await previews.RenderAsync(request with
    {
        RenderingId = "replay-exchange-preview-null-bundle-list",
        Bundles = null!
    });
    throw new InvalidOperationException("Null replay/exchange preview bundle list validation did not fail.");
}
catch (ArgumentNullException ex) when (string.Equals(ex.ParamName, "Bundles", StringComparison.Ordinal))
{
}

try
{
    await previews.RenderAsync(request with
    {
        RenderingId = "replay-exchange-preview-missing-preview-ref",
        Bundles =
        [
            request.Bundles[0] with
            {
                PreviewCard = request.Bundles[0].PreviewCard with { PreviewRefs = [] }
            },
            request.Bundles[1],
            request.Bundles[2]
        ]
    });
    throw new InvalidOperationException("Replay/exchange preview ref validation did not fail.");
}
catch (ArgumentException ex) when (ex.Message.Contains("require at least one preview ref", StringComparison.OrdinalIgnoreCase))
{
}

static ReplayExchangePreviewBundleRenderRequest CreateBundle(
    ReplayExchangePreviewBundleKind kind,
    string bundleRef,
    string lineageRef,
    string compatibilityReceiptId,
    string provenanceReceiptId,
    string boundedLossReceiptId,
    string previewRef,
    string captionRef) =>
    new(
        BundleKind: kind,
        BundleRef: bundleRef,
        LineageRef: lineageRef,
        CompatibilityReceiptId: compatibilityReceiptId,
        ProvenanceReceiptId: provenanceReceiptId,
        BoundedLossReceiptId: boundedLossReceiptId,
        PreviewCard: new ReplayExchangePreviewArtifactRenderRequest(
            Role: ReplayExchangePreviewArtifactRole.PreviewCard,
            Category: $"{kind.ToString().ToLowerInvariant()}/preview-card",
            Payload: $"{{\"bundleKind\":\"{kind}\",\"bundleRef\":\"{bundleRef}\"}}",
            OutputFormat: "png",
            ArtifactRef: $"{bundleRef}/preview-card",
            CaptionRefs: [],
            PreviewRefs: [previewRef],
            DeduplicationKey: $"{kind.ToString().ToLowerInvariant()}-preview",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        InspectableSibling: new ReplayExchangePreviewArtifactRenderRequest(
            Role: ReplayExchangePreviewArtifactRole.InspectableSibling,
            Category: $"{kind.ToString().ToLowerInvariant()}/inspectable",
            Payload: $"{{\"bundleKind\":\"{kind}\",\"bundleRef\":\"{bundleRef}\",\"lineageRef\":\"{lineageRef}\"}}",
            OutputFormat: "html",
            ArtifactRef: $"{bundleRef}/inspectable",
            CaptionRefs: [captionRef],
            PreviewRefs: [previewRef],
            DeduplicationKey: $"{kind.ToString().ToLowerInvariant()}-inspectable",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096));

static async Task WaitForSucceededJobAsync(IMediaRenderJobService jobs, string jobId)
{
    for (var attempt = 0; attempt < 100; attempt++)
    {
        var status = jobs.Get(jobId);
        if (status?.State == MediaRenderJobState.Succeeded)
        {
            return;
        }

        await Task.Delay(20);
    }

    throw new InvalidOperationException($"Replay/exchange preview job {jobId} did not reach Succeeded during smoke verification.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
