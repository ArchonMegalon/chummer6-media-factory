using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);
var recipes = new StructuredMediaRecipeExecutionService(jobs);

var request = new StructuredMediaRecipeRequest(
    RecipeExecutionId: "recipe-execution-001",
    RecipeFamily: StructuredMediaRecipeFamily.Publication,
    ApprovedSourcePackId: "approved-source-pack-001",
    Source: "structured-media-recipe-smoke",
    RequestedAtUtc: DateTimeOffset.UtcNow,
    Artifacts:
    [
        new StructuredMediaRecipeArtifactRequest(
            Role: StructuredMediaRecipeArtifactRole.Video,
            Category: "artifact-factory/recipe/video",
            Payload: "{\"video\":\"release proof\"}",
            OutputFormat: "mp4",
            PublicationRef: "public-proof://release/video",
            CaptionRefs: ["caption://release/en-US.vtt"],
            PreviewRefs: ["preview://release/card"],
            DeduplicationKey: "release-video",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new StructuredMediaRecipeArtifactRequest(
            Role: StructuredMediaRecipeArtifactRole.Audio,
            Category: "artifact-factory/recipe/audio",
            Payload: "{\"audio\":\"release proof\"}",
            OutputFormat: "mp3",
            PublicationRef: "public-proof://release/audio",
            CaptionRefs: ["caption://release/en-US.vtt"],
            PreviewRefs: [],
            DeduplicationKey: "release-audio",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new StructuredMediaRecipeArtifactRequest(
            Role: StructuredMediaRecipeArtifactRole.PreviewCard,
            Category: "artifact-factory/recipe/preview-card",
            Payload: "{\"preview\":\"release proof\"}",
            OutputFormat: "png",
            PublicationRef: "public-proof://release/preview",
            CaptionRefs: [],
            PreviewRefs: ["preview://release/card"],
            DeduplicationKey: "release-preview-card",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096),
        new StructuredMediaRecipeArtifactRequest(
            Role: StructuredMediaRecipeArtifactRole.PacketBundle,
            Category: "artifact-factory/recipe/packet",
            Payload: "{\"packet\":\"release proof\"}",
            OutputFormat: "zip",
            PublicationRef: "public-proof://release/packet",
            CaptionRefs: [],
            PreviewRefs: ["preview://release/card"],
            DeduplicationKey: "release-packet",
            CacheTtl: TimeSpan.FromMinutes(10),
            MaxBytes: 4096)
    ]);

var receipt = await recipes.RenderAsync(request);
Assert(receipt.Artifacts.Count == 4, "Structured recipe should receipt each requested artifact.");
Assert(receipt.VideoReceiptIds.Count == 1, "Video receipt id is required.");
Assert(receipt.AudioReceiptIds.Count == 1, "Audio receipt id is required.");
Assert(receipt.PreviewReceiptIds.Count == 1, "Preview receipt id is required.");
Assert(receipt.PacketReceiptIds.Count == 1, "Packet receipt id is required.");
Assert(receipt.JobIds.Count == 4, "Bundle receipt should expose every media job id directly.");
Assert(receipt.PublicationRefs.Count == 4, "Each sibling should publish a stable ref.");
Assert(receipt.PublicationReadyRefs.Count == 4, "Each sibling should publish a structured publication-ready ref.");
Assert(receipt.Artifacts.All(static artifact => artifact.JobState == MediaRenderJobState.Succeeded), "Recipe execution receipts must wait for completed media jobs.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.AssetId)), "Executed recipe receipts must preserve concrete asset ids.");
Assert(receipt.Artifacts.All(static artifact => artifact.ApprovalState == AssetApprovalState.Approved), "Executed recipe receipts must preserve asset approval truth.");
Assert(receipt.Artifacts.All(static artifact => artifact.RetentionState == AssetRetentionState.CacheOnly), "Executed recipe receipts must preserve asset retention truth.");
Assert(receipt.Artifacts.All(static artifact => artifact.StorageClass == AssetStorageClass.ObjectStorage), "Executed recipe receipts must preserve asset storage truth.");
Assert(receipt.PublicationReadyRefs.All(static row => !string.IsNullOrWhiteSpace(row.Ref) && !string.IsNullOrWhiteSpace(row.ReceiptId) && !string.IsNullOrWhiteSpace(row.JobId) && !string.IsNullOrWhiteSpace(row.AssetId)), "Publication-ready refs must preserve ref, receipt, job, and asset ids.");
Assert(receipt.PublicationReadyRefs.All(static row => row.JobState == MediaRenderJobState.Succeeded && row.ApprovalState == AssetApprovalState.Approved && row.RetentionState == AssetRetentionState.CacheOnly && row.StorageClass == AssetStorageClass.ObjectStorage), "Publication-ready refs must preserve completed job and lifecycle truth.");
Assert(receipt.PublicationReadyRefs.Any(static row => row.Role == StructuredMediaRecipeArtifactRole.Video && row.CaptionRefs.Count == 1 && row.PreviewRefs.Count == 1), "Video publication-ready refs must preserve caption and preview refs.");
Assert(receipt.PublicationReadyRefs.Any(static row => row.Role == StructuredMediaRecipeArtifactRole.PacketBundle && row.PreviewRefs.Count == 1), "Packet publication-ready refs must preserve preview refs.");
Assert(receipt.RoleReceiptGroups.Count == 4, "Each sibling role should publish a first-class receipt group.");
Assert(receipt.RoleReceiptGroups.All(static group => group.ReceiptIds.Count > 0 && group.JobIds.Count > 0 && group.PublicationRefs.Count > 0 && group.ArtifactReceipts.Count > 0), "Role receipt groups must preserve receipt, job, publication, and artifact rows.");
Assert(receipt.RoleReceiptGroups.SelectMany(static group => group.ArtifactReceipts).All(static row => row.JobState == MediaRenderJobState.Succeeded && row.ApprovalState == AssetApprovalState.Approved && row.RetentionState == AssetRetentionState.CacheOnly && row.StorageClass == AssetStorageClass.ObjectStorage), "Role receipt artifact rows must preserve lifecycle truth.");
Assert(receipt.RoleReceiptGroups.Any(static group => group.Role == StructuredMediaRecipeArtifactRole.Audio && group.CaptionRefs.Count == 1 && group.PreviewRefs.Count == 0), "Audio role group must preserve caption refs without inventing preview refs.");
Assert(receipt.RoleReceiptGroups.Any(static group => group.Role == StructuredMediaRecipeArtifactRole.PacketBundle && group.PreviewRefs.Count == 1), "Packet role group must preserve packet preview refs.");
Assert(receipt.CaptionRefs.SequenceEqual(["caption://release/en-US.vtt"]), "Caption refs should be deduped and preserved.");
Assert(receipt.PreviewRefs.SequenceEqual(["preview://release/card"]), "Preview refs should be deduped and preserved.");
Assert(receipt.PublicationRefReceipts.Count == 4, "Each publication ref should have a first-class receipt row.");
Assert(receipt.PublicationRefReceipts.All(static row => !string.IsNullOrWhiteSpace(row.ReceiptId) && !string.IsNullOrWhiteSpace(row.JobId)), "Publication ref rows must preserve receipt and job ids.");
Assert(receipt.CaptionRefReceipts.Count == 1, "Caption refs should publish first-class grouped receipt rows.");
Assert(receipt.CaptionRefReceipts[0].ReceiptIds.Count == 2, "Shared caption ref should point at video and audio receipts.");
Assert(receipt.CaptionRefReceipts[0].Roles.Contains(StructuredMediaRecipeArtifactRole.Video), "Caption ref should preserve the video role.");
Assert(receipt.CaptionRefReceipts[0].Roles.Contains(StructuredMediaRecipeArtifactRole.Audio), "Caption ref should preserve the audio role.");
Assert(receipt.CaptionRefReceipts[0].ArtifactReceipts.Count == 2, "Caption ref rows must expose publication/job artifact receipt detail.");
Assert(receipt.CaptionRefReceipts[0].ArtifactReceipts.All(static row => !string.IsNullOrWhiteSpace(row.PublicationRef) && !string.IsNullOrWhiteSpace(row.JobId) && row.JobState == MediaRenderJobState.Succeeded && row.RetentionState == AssetRetentionState.CacheOnly), "Caption artifact rows must preserve publication refs, job ids, and retention truth.");
Assert(receipt.PreviewRefReceipts.Count == 1, "Preview refs should publish first-class grouped receipt rows.");
Assert(receipt.PreviewRefReceipts[0].ReceiptIds.Count == 3, "Shared preview ref should point at video, preview-card, and packet receipts.");
Assert(receipt.PreviewRefReceipts[0].ArtifactReceipts.Count == 3, "Preview ref rows must expose publication/job artifact receipt detail.");
Assert(receipt.PreviewRefReceipts[0].ArtifactReceipts.Any(static row => row.Role == StructuredMediaRecipeArtifactRole.PacketBundle), "Packet bundles must be represented in preview ref receipt detail.");
Assert(receipt.PreviewRefReceipts[0].ArtifactReceipts.All(static row => row.JobState == MediaRenderJobState.Succeeded && row.RetentionState == AssetRetentionState.CacheOnly), "Preview ref rows must preserve completed job and retention truth.");
Assert(receipt.Artifacts.All(static artifact => !string.IsNullOrWhiteSpace(artifact.JobId)), "Every artifact receipt must carry a media job id.");
Assert(receipt.Artifacts.Select(static artifact => artifact.JobId).OrderBy(static jobId => jobId).SequenceEqual(receipt.JobIds.OrderBy(static jobId => jobId)), "Bundle job ids must match artifact receipt job ids.");

foreach (var artifact in receipt.Artifacts)
{
    await WaitForSucceededJobAsync(jobs, artifact.JobId);
}

var replayed = await recipes.RenderAsync(request);
Assert(
    receipt.Artifacts.Select(static artifact => artifact.JobId).SequenceEqual(replayed.Artifacts.Select(static artifact => artifact.JobId)),
    "Replay-safe dedupe should keep structured recipe artifact jobs stable.");

var collisionReceipt = await recipes.RenderAsync(request with
{
    RecipeExecutionId = "recipe-execution-collision-proof",
    Artifacts =
    [
        .. request.Artifacts,
        request.Artifacts[0] with
        {
            OutputFormat = "webm",
            PublicationRef = "public-proof://release/video-web",
            CaptionRefs = ["caption://release/en-US.web.vtt"],
            PreviewRefs = ["preview://release/web-card"],
            DeduplicationKey = "release-video"
        }
    ]
});
var collidingVideoJobs = collisionReceipt.Artifacts
    .Where(static artifact => artifact.Role == StructuredMediaRecipeArtifactRole.Video)
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(collidingVideoJobs.Length == 2, "Different video output refs must not collapse onto one recipe job when request dedupe keys collide.");

var delimiterCollisionReceipt = await recipes.RenderAsync(request with
{
    RecipeExecutionId = "recipe-execution-delimiter-collision-proof",
    Artifacts =
    [
        .. request.Artifacts,
        request.Artifacts[0] with
        {
            Category = "delimiter:category",
            OutputFormat = "mp4",
            PublicationRef = "public-proof://release/delimiter:video",
            DeduplicationKey = "shared"
        },
        request.Artifacts[0] with
        {
            Category = "delimiter",
            OutputFormat = "category:mp4",
            PublicationRef = "public-proof://release/delimiter",
            DeduplicationKey = "video:shared"
        }
    ]
});
var delimiterCollisionJobs = delimiterCollisionReceipt.Artifacts
    .Where(static artifact => artifact.PublicationRef.StartsWith("public-proof://release/delimiter", StringComparison.OrdinalIgnoreCase))
    .Select(static artifact => artifact.JobId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(delimiterCollisionJobs.Length == 2, "Delimiter-heavy recipe output refs must not collapse onto one recipe job.");

try
{
    await recipes.RenderAsync(request with
    {
        RecipeExecutionId = "missing-audio",
        Artifacts = request.Artifacts
            .Where(static artifact => artifact.Role != StructuredMediaRecipeArtifactRole.Audio)
            .ToArray()
    });
    throw new InvalidOperationException("Missing audio validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("Audio", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await recipes.RenderAsync(request with
    {
        RecipeExecutionId = "video-missing-caption",
        Artifacts = request.Artifacts
            .Select(static artifact => artifact.Role == StructuredMediaRecipeArtifactRole.Video
                ? artifact with { CaptionRefs = [] }
                : artifact)
            .ToArray()
    });
    throw new InvalidOperationException("Missing video caption validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("caption ref", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await recipes.RenderAsync(request with
    {
        RecipeExecutionId = "packet-missing-preview",
        Artifacts = request.Artifacts
            .Select(static artifact => artifact.Role == StructuredMediaRecipeArtifactRole.PacketBundle
                ? artifact with { PreviewRefs = [] }
                : artifact)
            .ToArray()
    });
    throw new InvalidOperationException("Missing packet preview validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("preview ref", StringComparison.OrdinalIgnoreCase))
{
}

try
{
    await recipes.RenderAsync(request with
    {
        RecipeExecutionId = "duplicate-publication-ref",
        Artifacts = request.Artifacts
            .Select(static artifact => artifact.Role == StructuredMediaRecipeArtifactRole.Audio
                ? artifact with { PublicationRef = "public-proof://release/video" }
                : artifact)
            .ToArray()
    });
    throw new InvalidOperationException("Duplicate publication ref validation did not fail.");
}
catch (ArgumentException exception) when (exception.Message.Contains("publication refs must be unique", StringComparison.OrdinalIgnoreCase))
{
}

Console.WriteLine("structured media recipe smoke ok");

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
