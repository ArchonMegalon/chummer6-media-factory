using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var bundles = new RunsiteOrientationBundleService(jobs);

var request = new RunsiteOrientationBundleRequest(
    BundleId: "orientation-bundle-001",
    ApprovedRunsitePackId: "approved-runsite-pack-001",
    RouteSummaryId: "route-summary-001",
    Source: "runsite-orientation-smoke",
    RequestedAtUtc: DateTimeOffset.UtcNow,
    Artifacts:
    [
        new RunsiteOrientationArtifactRenderRequest(
            Role: RunsiteOrientationArtifactRole.HostClip,
            Category: "runsite/orientation/host-clip",
            Payload: "{\"clip\":\"host\"}",
            OutputFormat: "mp4",
            RouteSegmentId: "entry",
            DeduplicationKey: "host-entry",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new RunsiteOrientationArtifactRenderRequest(
            Role: RunsiteOrientationArtifactRole.RoutePreview,
            Category: "runsite/orientation/route-preview",
            Payload: "{\"preview\":\"route\"}",
            OutputFormat: "png",
            RouteSegmentId: "entry-to-vault",
            DeduplicationKey: "preview-entry-vault",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new RunsiteOrientationArtifactRenderRequest(
            Role: RunsiteOrientationArtifactRole.AudioCompanion,
            Category: "runsite/orientation/audio",
            Payload: "{\"audio\":\"briefing\"}",
            OutputFormat: "mp3",
            RouteSegmentId: "entry",
            DeduplicationKey: "audio-entry",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096)
    ]);

var receipt = await bundles.RenderAsync(request);
Assert(receipt.Artifacts.Count == 3, "Orientation bundle should receipt each requested artifact.");
Assert(receipt.HostClipReceiptIds.Count == 1, "Host clips must be addressable as role-specific receipt ids.");
Assert(receipt.RoutePreviewReceiptIds.Count == 1, "Route previews must be addressable as route-linked receipt ids.");
Assert(receipt.RoutePreviewArtifactReceipts.Count == 1, "Route previews must publish route-linked artifact receipt rows.");
Assert(receipt.RoutePreviewArtifactReceipts[0].RouteSegmentId == "entry-to-vault", "Route preview receipt must preserve the route segment.");
Assert(receipt.RoutePreviewArtifactReceipts[0].ReceiptId == receipt.RoutePreviewReceiptIds[0], "Route preview link must point at the artifact receipt.");
Assert(receipt.RoutePreviewArtifactReceipts[0].JobId == receipt.Artifacts.Single(static artifact => artifact.Role == RunsiteOrientationArtifactRole.RoutePreview).JobId, "Route preview link must expose the media job id.");
Assert(receipt.AudioCompanionReceiptIds.Count == 1, "Audio companions must be addressable as role-specific receipt ids.");
Assert(receipt.TourSiblingReceiptIds.Count == 0, "Absent tour siblings should not emit receipt ids.");
Assert(receipt.PreviewTruthPosture == RunsiteOrientationBundleService.PreviewTruthPosture, "Bundle must disclose preview-only posture.");
Assert(receipt.Artifacts.Any(static artifact => artifact.Role == RunsiteOrientationArtifactRole.HostClip), "Host clip receipt is required.");
Assert(receipt.Artifacts.Any(static artifact => artifact.Role == RunsiteOrientationArtifactRole.RoutePreview), "Route preview receipt is required.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.JobId)), "Every artifact receipt must carry a media job id.");

foreach (var artifact in receipt.Artifacts)
{
    await WaitForSucceededJobAsync(jobs, artifact.JobId);
}

var replayed = await bundles.RenderAsync(request);
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(replayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep orientation bundle artifact jobs stable.");

try
{
    await bundles.RenderAsync(request with
    {
        BundleId = "missing-host",
        Artifacts = request.Artifacts
            .Where(static artifact => artifact.Role != RunsiteOrientationArtifactRole.HostClip)
            .ToArray()
    });
    throw new InvalidOperationException("Missing host clip validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("host clip", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await bundles.RenderAsync(request with
    {
        BundleId = "missing-route-preview",
        Artifacts = request.Artifacts
            .Where(static artifact => artifact.Role != RunsiteOrientationArtifactRole.RoutePreview)
            .ToArray()
    });
    throw new InvalidOperationException("Missing route preview validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("route preview", StringComparison.OrdinalIgnoreCase))
{
}

var collisionRequest = new RunsiteOrientationBundleRequest(
    BundleId: "collision:bundle",
    ApprovedRunsitePackId: "pack:alpha",
    RouteSummaryId: "route:summary",
    Source: "runsite-orientation-smoke",
    RequestedAtUtc: DateTimeOffset.UtcNow,
    Artifacts:
    [
        new RunsiteOrientationArtifactRenderRequest(
            Role: RunsiteOrientationArtifactRole.HostClip,
            Category: "runsite/orientation/host-clip",
            Payload: "{\"clip\":\"host\"}",
            OutputFormat: "mp4",
            RouteSegmentId: "entry",
            DeduplicationKey: "shared:key",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new RunsiteOrientationArtifactRenderRequest(
            Role: RunsiteOrientationArtifactRole.RoutePreview,
            Category: "runsite/orientation/preview:alpha",
            Payload: "{\"preview\":\"route-a\"}",
            OutputFormat: "png",
            RouteSegmentId: "segment:01",
            DeduplicationKey: "shared:key",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new RunsiteOrientationArtifactRenderRequest(
            Role: RunsiteOrientationArtifactRole.RoutePreview,
            Category: "runsite/orientation/preview",
            Payload: "{\"preview\":\"route-b\"}",
            OutputFormat: "png:alt",
            RouteSegmentId: "segment",
            DeduplicationKey: "01:shared:key",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096)
    ]);

var collisionReceipt = await bundles.RenderAsync(collisionRequest);
var collisionPreviewJobs = collisionReceipt.RoutePreviewArtifactReceipts
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
var collisionPreviewReceipts = collisionReceipt.RoutePreviewArtifactReceipts
    .Select(static artifact => artifact.ReceiptId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

Assert(collisionPreviewJobs.Length == 2, "Delimiter-heavy route preview variants must not collapse onto one media job.");
Assert(collisionPreviewReceipts.Length == 2, "Delimiter-heavy route preview variants must not collapse onto one receipt id.");

Console.WriteLine("runsite orientation bundle smoke ok");

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
