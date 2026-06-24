using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var bundles = new CampaignBriefingBundleService(jobs);

var request = new CampaignBriefingBundleRequest(
    BundleId: "campaign-briefing-bundle-001",
    CampaignPrimerId: "primer-001",
    MissionBriefingId: "briefing-001",
    RequestedLocale: "en-US",
    Source: "campaign-briefing-smoke",
    RequestedAtUtc: DateTimeOffset.UtcNow,
    Entries:
    [
        BuildEntry(CampaignBriefingBundleSlot.ColdOpen, "en-US", false, "cold-open", "mp4"),
        BuildEntry(CampaignBriefingBundleSlot.MissionBriefing, "en-US", false, "mission-briefing", "mp4"),
        BuildEntry(CampaignBriefingBundleSlot.ColdOpen, "de-AT", true, "cold-open-fallback", "mp4"),
        BuildEntry(CampaignBriefingBundleSlot.MissionBriefing, "de-AT", true, "mission-briefing-fallback", "mp4")
    ]);

var receipt = await bundles.RenderAsync(request);
Assert(receipt.LocaleReceipts.Count == 4, "Campaign briefing bundle should receipt each locale entry.");
Assert(receipt.LocaleBundleReceipts.Count == 2, "Campaign briefing bundle should group locale-matched primary and fallback bundles.");
Assert(receipt.LocaleBundleReceipts.All(static row => !string.IsNullOrWhiteSpace(row.ReceiptId)), "Locale bundle receipts must preserve stable receipt ids.");
Assert(receipt.ColdOpenReceiptIds.Count == 2, "Cold-open media receipts must include primary and fallback siblings.");
Assert(receipt.MissionBriefingReceiptIds.Count == 2, "Mission briefing media receipts must include primary and fallback siblings.");
Assert(receipt.CaptionReceiptIds.Count == 4, "Every locale entry must render a caption sibling.");
Assert(receipt.PreviewReceiptIds.Count == 4, "Every locale entry must render a preview sibling.");
Assert(receipt.FallbackSiblingReceipts.Count == 1, "Fallback siblings must stay explicitly bounded as locale-matched bundles.");
Assert(receipt.JobIds.Count == 12, "Each locale entry should emit media, caption, and preview jobs.");
Assert(receipt.LocaleReceipts.Any(static row => row.Slot == CampaignBriefingBundleSlot.ColdOpen && row.Locale == "en-US" && !row.IsFallbackSibling), "Requested locale cold-open receipt is required.");
Assert(receipt.LocaleReceipts.Any(static row => row.Slot == CampaignBriefingBundleSlot.MissionBriefing && row.Locale == "en-US" && !row.IsFallbackSibling), "Requested locale mission briefing receipt is required.");
Assert(receipt.LocaleBundleReceipts.Any(static row => row.Locale == "en-US" && !row.IsFallbackSibling && row.CaptionReceiptIds.Count == 2 && row.PreviewReceiptIds.Count == 2), "Requested locale bundle receipt must preserve cold-open and mission sibling caption and preview ids.");
Assert(receipt.RequestedLocaleBundleReceiptId == receipt.LocaleBundleReceipts.Single(static row => row.Locale == "en-US" && !row.IsFallbackSibling).ReceiptId, "Requested locale bundle receipt id must stay first-class on the bundle receipt.");
var requestedLocaleBundle = receipt.LocaleBundleReceipts.Single(static row => row.Locale == "en-US" && !row.IsFallbackSibling);
Assert(!string.IsNullOrWhiteSpace(requestedLocaleBundle.ColdOpenCaptionReceiptId) && !string.IsNullOrWhiteSpace(requestedLocaleBundle.MissionBriefingCaptionReceiptId), "Requested locale bundle receipt must preserve slot-aware caption sibling ids.");
Assert(!string.IsNullOrWhiteSpace(requestedLocaleBundle.ColdOpenPreviewReceiptId) && !string.IsNullOrWhiteSpace(requestedLocaleBundle.MissionBriefingPreviewReceiptId), "Requested locale bundle receipt must preserve slot-aware preview sibling ids.");
Assert(requestedLocaleBundle.CaptionReceiptIds.SequenceEqual(new[] { requestedLocaleBundle.ColdOpenCaptionReceiptId, requestedLocaleBundle.MissionBriefingCaptionReceiptId }.OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)), "Requested locale bundle caption ids must stay aligned with the slot-aware caption summary fields.");
Assert(requestedLocaleBundle.PreviewReceiptIds.SequenceEqual(new[] { requestedLocaleBundle.ColdOpenPreviewReceiptId, requestedLocaleBundle.MissionBriefingPreviewReceiptId }.OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)), "Requested locale bundle preview ids must stay aligned with the slot-aware preview summary fields.");
Assert(receipt.FallbackLocales.SequenceEqual(["de-AT"]), "Fallback locales must stay first-class and locale-ordered.");
Assert(receipt.FallbackLocaleBundleReceiptIds.SequenceEqual([receipt.LocaleBundleReceipts.Single(static row => row.Locale == "de-AT" && row.IsFallbackSibling).ReceiptId]), "Fallback locale bundle receipt ids must stay first-class and aligned with fallback bundle rows.");
Assert(!string.IsNullOrWhiteSpace(receipt.FallbackSiblingReceipts[0].ReceiptId), "Fallback sibling receipts must preserve stable receipt ids.");
Assert(receipt.FallbackSiblingReceipts[0].Locale == "de-AT" && receipt.FallbackSiblingReceipts[0].CaptionReceiptIds.Count == 2 && receipt.FallbackSiblingReceipts[0].PreviewReceiptIds.Count == 2, "Fallback locale bundle receipt must preserve both slot siblings.");
Assert(!string.IsNullOrWhiteSpace(receipt.FallbackSiblingReceipts[0].ColdOpenCaptionReceiptId) && !string.IsNullOrWhiteSpace(receipt.FallbackSiblingReceipts[0].MissionBriefingCaptionReceiptId), "Fallback sibling receipts must preserve slot-aware caption sibling ids.");
Assert(!string.IsNullOrWhiteSpace(receipt.FallbackSiblingReceipts[0].ColdOpenPreviewReceiptId) && !string.IsNullOrWhiteSpace(receipt.FallbackSiblingReceipts[0].MissionBriefingPreviewReceiptId), "Fallback sibling receipts must preserve slot-aware preview sibling ids.");
Assert(receipt.FallbackSiblingReceipts[0].CaptionReceiptIds.SequenceEqual(new[] { receipt.FallbackSiblingReceipts[0].ColdOpenCaptionReceiptId, receipt.FallbackSiblingReceipts[0].MissionBriefingCaptionReceiptId }.OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)), "Fallback sibling caption ids must stay aligned with the slot-aware caption summary fields.");
Assert(receipt.FallbackSiblingReceipts[0].PreviewReceiptIds.SequenceEqual(new[] { receipt.FallbackSiblingReceipts[0].ColdOpenPreviewReceiptId, receipt.FallbackSiblingReceipts[0].MissionBriefingPreviewReceiptId }.OrderBy(static receiptId => receiptId, StringComparer.OrdinalIgnoreCase)), "Fallback sibling preview ids must stay aligned with the slot-aware preview summary fields.");
Assert(receipt.ArtifactReceipts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetUrl)), "Artifact receipts must preserve asset urls.");
Assert(receipt.ArtifactReceipts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetId) && artifact.JobState == MediaRenderJobState.Succeeded), "Artifact receipts must preserve completed job and asset identity.");
Assert(receipt.ArtifactReceipts.All(static artifact => artifact.ApprovalState == AssetApprovalState.Approved && artifact.RetentionState == AssetRetentionState.CacheOnly && artifact.StorageClass == AssetStorageClass.ObjectStorage), "Artifact receipts must preserve lifecycle truth.");

foreach (var artifact in receipt.ArtifactReceipts)
{
    await WaitForSucceededJobAsync(jobs, artifact.JobId);
}

var replayed = await bundles.RenderAsync(request);
Assert(
    receipt.JobIds.SequenceEqual(replayed.JobIds),
    "Replay-safe dedupe should keep campaign briefing jobs stable.");
Assert(
    receipt.LocaleReceipts.Select(static row => row.EntryReceiptId).SequenceEqual(replayed.LocaleReceipts.Select(static row => row.EntryReceiptId)),
    "Replay-safe dedupe should keep locale receipt ids stable.");
Assert(
    receipt.LocaleBundleReceipts.Select(static row => (row.Locale, row.ColdOpenEntryReceiptId, row.MissionBriefingEntryReceiptId))
        .SequenceEqual(replayed.LocaleBundleReceipts.Select(static row => (row.Locale, row.ColdOpenEntryReceiptId, row.MissionBriefingEntryReceiptId))),
    "Replay-safe dedupe should keep locale bundle receipt ids stable.");
Assert(
    receipt.LocaleBundleReceipts.Select(static row => row.ReceiptId)
        .SequenceEqual(replayed.LocaleBundleReceipts.Select(static row => row.ReceiptId)),
    "Replay-safe dedupe should keep locale bundle row receipt ids stable.");
Assert(
    receipt.FallbackSiblingReceipts.Select(static row => row.ReceiptId)
        .SequenceEqual(replayed.FallbackSiblingReceipts.Select(static row => row.ReceiptId)),
    "Replay-safe dedupe should keep fallback sibling receipt ids stable.");
Assert(
    receipt.FallbackLocaleBundleReceiptIds.SequenceEqual(replayed.FallbackLocaleBundleReceiptIds),
    "Replay-safe dedupe should keep fallback locale bundle receipt ids stable.");
Assert(
    receipt.RequestedLocaleBundleReceiptId == replayed.RequestedLocaleBundleReceiptId,
    "Replay-safe dedupe should keep the requested locale bundle receipt id stable.");

var reorderedReceipt = await bundles.RenderAsync(request with
{
    Entries = request.Entries
        .Reverse()
        .ToArray()
});
Assert(
    receipt.LocaleReceipts.Select(static row => (row.Locale, row.Slot, row.IsFallbackSibling, row.EntryReceiptId))
        .SequenceEqual(reorderedReceipt.LocaleReceipts.Select(static row => (row.Locale, row.Slot, row.IsFallbackSibling, row.EntryReceiptId))),
    "Normalized locale-bundle ordering should keep locale receipts stable when callers reorder the same campaign briefing entries.");
Assert(
    receipt.LocaleBundleReceipts.Select(static row => row.ReceiptId)
        .SequenceEqual(reorderedReceipt.LocaleBundleReceipts.Select(static row => row.ReceiptId)),
    "Normalized locale-bundle ordering should keep locale bundle receipt rows stable when callers reorder the same campaign briefing entries.");
Assert(
    receipt.FallbackSiblingReceipts.Select(static row => row.ReceiptId)
        .SequenceEqual(reorderedReceipt.FallbackSiblingReceipts.Select(static row => row.ReceiptId)),
    "Normalized locale-bundle ordering should keep fallback sibling receipt rows stable when callers reorder the same campaign briefing entries.");
Assert(
    receipt.FallbackLocales.SequenceEqual(reorderedReceipt.FallbackLocales),
    "Normalized locale-bundle ordering should keep fallback locale summaries stable when callers reorder the same campaign briefing entries.");
Assert(
    receipt.JobIds.SequenceEqual(reorderedReceipt.JobIds),
    "Normalized locale-bundle ordering should keep bundle job summaries stable when callers reorder the same campaign briefing entries.");

var trimmedRequest = request with
{
    BundleId = " campaign-briefing-bundle-trimmed ",
    CampaignPrimerId = " primer-001 ",
    MissionBriefingId = " briefing-001 ",
    RequestedLocale = " en-US ",
    Source = " campaign-briefing-smoke "
};
var trimmedReceipt = await bundles.RenderAsync(trimmedRequest);
Assert(trimmedReceipt.BundleId == "campaign-briefing-bundle-trimmed", "Bundle ids should be trimmed before receipt emission.");
Assert(trimmedReceipt.CampaignPrimerId == "primer-001", "Campaign primer ids should be trimmed before receipt emission.");
Assert(trimmedReceipt.MissionBriefingId == "briefing-001", "Mission briefing ids should be trimmed before receipt emission.");
Assert(trimmedReceipt.RequestedLocale == "en-US", "Requested locales should be trimmed before locale validation and receipt emission.");
Assert(trimmedReceipt.Source == "campaign-briefing-smoke", "Source should be trimmed before receipt emission.");
Assert(trimmedReceipt.LocaleBundleReceipts.Any(static row => row.Locale == "en-US" && !row.IsFallbackSibling), "Trimmed requested locales should still produce the primary locale bundle.");

try
{
    await bundles.RenderAsync(request with
    {
        BundleId = "missing-cold-open",
        Entries = request.Entries
            .Where(static entry => !(entry.Slot == CampaignBriefingBundleSlot.ColdOpen && !entry.IsFallbackSibling))
            .ToArray()
    });
    throw new InvalidOperationException("Missing requested-locale cold-open validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("ColdOpen", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await bundles.RenderAsync(request with
    {
        BundleId = "too-many-fallback-locales",
        Entries =
        [
            ..request.Entries,
            BuildEntry(CampaignBriefingBundleSlot.ColdOpen, "fr-FR", true, "cold-open-fallback-fr", "mp4"),
            BuildEntry(CampaignBriefingBundleSlot.MissionBriefing, "es-ES", true, "mission-briefing-fallback-es", "mp4")
        ]
    });
    throw new InvalidOperationException("Fallback locale bound validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("fallback locales", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await bundles.RenderAsync(request with
    {
        BundleId = "missing-fallback-mission-briefing",
        Entries = request.Entries
            .Where(static entry => !(entry.Slot == CampaignBriefingBundleSlot.MissionBriefing && entry.Locale == "de-AT" && entry.IsFallbackSibling))
            .ToArray()
    });
    throw new InvalidOperationException("Incomplete fallback locale bundle validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("locale-matched cold-open and mission briefing siblings", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await bundles.RenderAsync(request with
    {
        BundleId = "requested-locale-fallback",
        Entries = request.Entries
            .Select(entry => entry.Locale == "en-US" && entry.Slot == CampaignBriefingBundleSlot.ColdOpen
                ? entry with { IsFallbackSibling = true }
                : entry)
            .ToArray()
    });
    throw new InvalidOperationException("Requested-locale fallback posture validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("requested locale as the primary sibling", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await bundles.RenderAsync(request with
    {
        BundleId = "non-requested-primary",
        Entries = request.Entries
            .Select(entry => entry.Locale == "de-AT"
                ? entry with { IsFallbackSibling = false }
                : entry)
            .ToArray()
    });
    throw new InvalidOperationException("Non-requested primary posture validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("fallback sibling", StringComparison.OrdinalIgnoreCase))
{
}

var collisionRequest = new CampaignBriefingBundleRequest(
    BundleId: "campaign:briefing:collision",
    CampaignPrimerId: "primer:alpha",
    MissionBriefingId: "briefing:alpha",
    RequestedLocale: "en-US",
    Source: "campaign-briefing-smoke",
    RequestedAtUtc: DateTimeOffset.UtcNow,
    Entries:
    [
        BuildEntry(CampaignBriefingBundleSlot.ColdOpen, "en-US", false, "collision", "mp4"),
        new CampaignBriefingBundleEntryRequest(
            Slot: CampaignBriefingBundleSlot.ColdOpen,
            Locale: "en:US",
            IsFallbackSibling: true,
            Media: new CampaignBriefingBundleArtifactRequest("campaign/briefing", "{\"video\":\"cold-open-b\"}", "mp4", "01:shared:key", CacheTtl: TimeSpan.FromMinutes(10), MaxBytes: 4096),
            Caption: new CampaignBriefingBundleArtifactRequest("campaign/caption", "{\"caption\":\"cold-open-b\"}", "vtt", "01:shared:key", CacheTtl: TimeSpan.FromMinutes(10), MaxBytes: 4096),
            Preview: new CampaignBriefingBundleArtifactRequest("campaign/preview", "{\"preview\":\"cold-open-b\"}", "png", "01:shared:key", CacheTtl: TimeSpan.FromMinutes(10), MaxBytes: 4096)),
        new CampaignBriefingBundleEntryRequest(
            Slot: CampaignBriefingBundleSlot.MissionBriefing,
            Locale: "en-US",
            IsFallbackSibling: false,
            Media: new CampaignBriefingBundleArtifactRequest("campaign/briefing:video", "{\"video\":\"a\"}", "mp4:alt", "shared:key", CacheTtl: TimeSpan.FromMinutes(10), MaxBytes: 4096),
            Caption: new CampaignBriefingBundleArtifactRequest("campaign/briefing:caption", "{\"caption\":\"a\"}", "vtt:alt", "shared:key", CacheTtl: TimeSpan.FromMinutes(10), MaxBytes: 4096),
            Preview: new CampaignBriefingBundleArtifactRequest("campaign/briefing:preview", "{\"preview\":\"a\"}", "png:alt", "shared:key", CacheTtl: TimeSpan.FromMinutes(10), MaxBytes: 4096)),
        new CampaignBriefingBundleEntryRequest(
            Slot: CampaignBriefingBundleSlot.MissionBriefing,
            Locale: "en:US",
            IsFallbackSibling: true,
            Media: new CampaignBriefingBundleArtifactRequest("campaign/briefing", "{\"video\":\"b\"}", "mp4", "01:shared:key", CacheTtl: TimeSpan.FromMinutes(10), MaxBytes: 4096),
            Caption: new CampaignBriefingBundleArtifactRequest("campaign/caption", "{\"caption\":\"b\"}", "vtt", "01:shared:key", CacheTtl: TimeSpan.FromMinutes(10), MaxBytes: 4096),
            Preview: new CampaignBriefingBundleArtifactRequest("campaign/preview", "{\"preview\":\"b\"}", "png", "01:shared:key", CacheTtl: TimeSpan.FromMinutes(10), MaxBytes: 4096))
    ]);

var collisionReceipt = await bundles.RenderAsync(collisionRequest);
var collisionJobs = collisionReceipt.ArtifactReceipts
    .Where(static artifact => artifact.Slot == CampaignBriefingBundleSlot.MissionBriefing && artifact.Kind == CampaignBriefingBundleArtifactKind.Media)
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
var collisionReceipts = collisionReceipt.ArtifactReceipts
    .Where(static artifact => artifact.Slot == CampaignBriefingBundleSlot.MissionBriefing && artifact.Kind == CampaignBriefingBundleArtifactKind.Media)
    .Select(static artifact => artifact.ReceiptId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

Assert(collisionJobs.Length == 2, "Delimiter-heavy locale variants must not collapse onto one campaign briefing job.");
Assert(collisionReceipts.Length == 2, "Delimiter-heavy locale variants must not collapse onto one campaign briefing receipt id.");

try
{
    await bundles.RenderAsync(request with
    {
        BundleId = "duplicate-fallback-cold-open",
        Entries =
        [
            ..request.Entries,
            BuildEntry(CampaignBriefingBundleSlot.ColdOpen, "de-AT", true, "duplicate-cold-open", "mp4")
        ]
    });
    throw new InvalidOperationException("Duplicate locale-slot validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("only one fallback ColdOpen entry", StringComparison.OrdinalIgnoreCase))
{
}

Console.WriteLine("campaign briefing bundle smoke ok");

static CampaignBriefingBundleEntryRequest BuildEntry(
    CampaignBriefingBundleSlot slot,
    string locale,
    bool isFallbackSibling,
    string stem,
    string outputFormat) =>
    new(
        Slot: slot,
        Locale: locale,
        IsFallbackSibling: isFallbackSibling,
        Media: new CampaignBriefingBundleArtifactRequest(
            Category: $"campaign/{stem}/media",
            Payload: $"{{\"slot\":\"{slot}\",\"locale\":\"{locale}\"}}",
            OutputFormat: outputFormat,
            DeduplicationKey: $"{stem}:{locale}:media",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        Caption: new CampaignBriefingBundleArtifactRequest(
            Category: $"campaign/{stem}/caption",
            Payload: $"{{\"caption\":\"{locale}\"}}",
            OutputFormat: "vtt",
            DeduplicationKey: $"{stem}:{locale}:caption",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        Preview: new CampaignBriefingBundleArtifactRequest(
            Category: $"campaign/{stem}/preview",
            Payload: $"{{\"preview\":\"{locale}\"}}",
            OutputFormat: "png",
            DeduplicationKey: $"{stem}:{locale}:preview",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096));

static async Task<MediaRenderJobStatus> WaitForSucceededJobAsync(IMediaRenderJobService jobs, string jobId)
{
    for (var attempt = 0; attempt < 50; attempt++)
    {
        var status = jobs.Get(jobId);
        if (status?.State == MediaRenderJobState.Succeeded)
        {
            return status;
        }

        if (status?.State == MediaRenderJobState.Failed)
        {
            throw new InvalidOperationException($"Job {jobId} failed: {status.Error ?? "unknown"}");
        }

        await Task.Delay(20);
    }

    throw new TimeoutException($"Job {jobId} did not reach succeeded state in time.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
