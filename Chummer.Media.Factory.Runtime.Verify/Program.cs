using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;

var assets = new AssetLifecycleService();
var jobs = new MediaRenderJobService(assets);

var approvalAsset = await assets.StoreAsync(
    category: "portrait/canon",
    content: "{\"portrait\":\"canon\"}",
    source: "restore-drill",
    policy: new AssetLifecyclePolicy(
        CacheTtl: TimeSpan.FromMinutes(5),
        LongTermCache: false,
        MaxBytes: 4096,
        RequiresApproval: true,
        PersistOnApproval: true,
        StorageClass: AssetStorageClass.ObjectStorage,
        AllowPersistentPinning: true));

await assets.ApplyLifecycleAsync(
    approvalAsset.AssetId,
    new AssetLifecycleMutationRequest(
        ApprovalState: AssetApprovalState.Approved,
        Pin: true,
        Persist: true,
        Reason: "pin canon"));
await assets.ApplyLifecycleAsync(
    approvalAsset.AssetId,
    new AssetLifecycleMutationRequest(
        ApprovalState: AssetApprovalState.Approved,
        Pin: true,
        Persist: true,
        Reason: "pin canon"));

var cacheOnlyAsset = await assets.StoreAsync(
    category: "packet/preview",
    content: "<html>preview</html>",
    source: "restore-drill",
    policy: new AssetLifecyclePolicy(
        CacheTtl: TimeSpan.FromMilliseconds(80),
        LongTermCache: false,
        MaxBytes: 4096,
        RequiresApproval: false,
        PersistOnApproval: false,
        StorageClass: AssetStorageClass.ObjectStorage,
        AllowPersistentPinning: false));

var firstJob = await jobs.EnqueueAsync(new MediaRenderJobEnqueueRequest(
    JobType: MediaRenderJobType.DocumentPreviewImage,
    DeduplicationKey: "restore-drill:preview",
    Category: "packet/preview",
    Payload: "<html>preview</html>",
    Source: "restore-drill",
    CacheTtl: TimeSpan.FromMilliseconds(80),
    MaxBytes: 4096,
    RequiresApproval: false,
    PersistOnApproval: false,
    AllowPersistentPinning: false));

var succeededJob = await WaitForSucceededJobAsync(jobs, firstJob.JobId);
var replayedJob = await jobs.EnqueueAsync(new MediaRenderJobEnqueueRequest(
    JobType: MediaRenderJobType.DocumentPreviewImage,
    DeduplicationKey: "restore-drill:preview",
    Category: "packet/preview",
    Payload: "<html>preview</html>",
    Source: "restore-drill",
    CacheTtl: TimeSpan.FromMilliseconds(80),
    MaxBytes: 4096,
    RequiresApproval: false,
    PersistOnApproval: false,
    AllowPersistentPinning: false));

Assert(string.Equals(firstJob.JobId, replayedJob.JobId, StringComparison.Ordinal), "Replay-safe dedupe should reuse the same job before expiry.");

var backup = MediaFactoryRuntimeBackup.Export(assets, jobs);
Assert(string.Equals(backup.ContractFamily, MediaFactoryRuntimeBackup.ContractFamily, StringComparison.Ordinal), "Backup contract family must stay stable.");
Assert(backup.Assets.Assets.Count >= 2, "Backup must retain both approval and cache assets.");
Assert(backup.Jobs.Jobs.Count >= 1, "Backup must retain render-job rows.");

var restoredAssets = new AssetLifecycleService();
var restoredJobs = new MediaRenderJobService(restoredAssets);
MediaFactoryRuntimeBackup.Restore(restoredAssets, restoredJobs, backup);

var restoredPinned = restoredAssets.Resolve(approvalAsset.AssetId);
Assert(restoredPinned is not null, "Restore must retain pinned approval assets.");
Assert(restoredPinned!.RetentionState == AssetRetentionState.Pinned, "Pinned approval assets must stay pinned after restore.");
Assert(restoredPinned.StorageClass == AssetStorageClass.LongTermObjectStorage, "Pinned approval assets must keep long-term storage after restore.");

var restoredPipeline = restoredAssets.GetApprovalPipelineProjection();
Assert(restoredPipeline.Idempotency.ReplayCount >= 1, "Restore must preserve lifecycle replay counters.");

var restoredJob = restoredJobs.Get(succeededJob.JobId);
Assert(restoredJob is not null, "Restore must retain succeeded render jobs.");
Assert(restoredJob!.State == MediaRenderJobState.Succeeded, "Succeeded render jobs must remain succeeded immediately after restore.");

var restoredMediaProjection = restoredJobs.GetMediaPipelineProjection();
Assert(restoredMediaProjection.Idempotency.ReplayCount >= 1, "Restore must preserve media dedupe replay counts.");

await Task.Delay(120);
var sweep = restoredAssets.SweepExpired(DateTimeOffset.UtcNow);
Assert(sweep.ExpiredAssetCount >= 1, "Retention sweep after restore must expire cache-only assets.");

var expiredCacheAsset = restoredAssets.Resolve(cacheOnlyAsset.AssetId);
Assert(expiredCacheAsset is null, "Expired cache-only assets must not resolve after restore and sweep.");

var expiredJob = restoredJobs.Get(succeededJob.JobId);
Assert(expiredJob is not null, "Expired render jobs must remain inspectable after restore.");
Assert(expiredJob!.State == MediaRenderJobState.Expired, "Render-job expiry must still work after restore.");

Console.WriteLine("Media factory runtime verification passed.");

static async Task<MediaRenderJobStatus> WaitForSucceededJobAsync(IMediaRenderJobService jobs, string jobId)
{
    for (var attempt = 0; attempt < 100; attempt++)
    {
        var job = jobs.Get(jobId);
        if (job is not null && job.State == MediaRenderJobState.Succeeded)
        {
            return job;
        }

        await Task.Delay(20);
    }

    throw new InvalidOperationException($"Timed out waiting for media job '{jobId}' to succeed.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
