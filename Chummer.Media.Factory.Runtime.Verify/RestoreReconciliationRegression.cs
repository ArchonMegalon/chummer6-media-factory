using System.Text.Json;
using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;
using Chummer.Run.Contracts.Observability;

internal static class RestoreReconciliationRegression
{
    private const string RecoveryRequired = "media-render-asset-recovery-required";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public static async Task RunAsync()
    {
        await ValidAssetAndRepeatedRestoreAsync();
        await ExpiryUsesReusedAssetAgeAsync();
        await MissingAssetRequiresRecoveryAsync();
        await PersistentRetentionAndReleaseAsync();
        await UncertaintySurvivesAgeAndRestoreAsync();
        await OldExpiryCannotEvictReplacementAsync();
        Console.WriteLine("restore reconciliation regressions ok (6 groups; synthetic assets and clock only)");
    }

    private static async Task ValidAssetAndRepeatedRestoreAsync()
    {
        var clock = new Clock();
        var lane = new Lane(clock);
        var first = await SucceedAsync(lane);
        var originalAsset = lane.Assets.Inspect(first.AssetId!)!;
        for (var restore = 0; restore < 3; restore++)
        {
            clock.Advance(TimeSpan.FromMinutes(2));
            lane = Restore(Backup(lane), clock);
            var status = lane.Jobs.Get(first.JobId)!;
            var replay = await lane.Jobs.EnqueueAsync(Request());
            Check(status.State == MediaRenderJobState.Succeeded && status.AssetUrl == first.AssetUrl &&
                replay.JobId == first.JobId && replay.State == MediaRenderJobState.Succeeded,
                "A present, nonexpired restored asset must support query and same-job replay.");
            Check(lane.Assets.Inspect(first.AssetId!)!.ExpiresAtUtc == originalAsset.ExpiresAtUtc &&
                lane.Jobs.ExportBackup().Jobs.Single().CompletedAtUtc == first.CompletedAtUtc &&
                lane.Store.Dispatches == 0, "Repeated restore must preserve expiry/completion anchors without dispatch.");
        }

        clock.Advance(TimeSpan.FromMinutes(4)); // Original asset TTL, not last restore + TTL.
        Check(lane.Jobs.List().Single().State == MediaRenderJobState.Expired &&
            lane.Jobs.Get(first.JobId)!.AssetUrl is null, "Time passing must expire a restored asset and job at the original deadline.");
        Check(lane.Jobs.GetMediaPipelineProjection().Idempotency.TrackedKeys == 0,
            "Confirmed expiry must release its reservation.");
    }

    private static async Task ExpiryUsesReusedAssetAgeAsync()
    {
        var clock = new Clock();
        var lane = new Lane(clock);
        var oldAsset = await lane.Assets.StoreAsync("test", "payload", "synthetic-restore",
            new AssetLifecyclePolicy(Ttl, false, 4096));
        clock.Advance(TimeSpan.FromMinutes(9));
        var first = await SucceedAsync(lane);
        Check(first.AssetId == oldAsset.AssetId, "Fixture must reuse an asset older than the job.");
        var backup = Backup(lane);
        clock.Advance(TimeSpan.FromMinutes(2));
        lane = Restore(backup, clock);
        var status = lane.Jobs.Get(first.JobId)!;
        Check(status.State == MediaRenderJobState.Expired && status.AssetUrl is null &&
            status.RetentionState == AssetRetentionState.Expired && lane.Assets.Resolve(oldAsset.AssetId) is null,
            "An aged backup must not renew a reused asset from job completion or restore time.");
        Check(lane.Store.Dispatches == 0 && lane.Jobs.ExportBackup().Jobs.Single().State == MediaRenderJobState.Expired,
            "Restore and export must reconcile expiry without dispatch.");

        var replacement = await SucceedAsync(lane);
        Check(replacement.JobId != first.JobId && replacement.AssetId != first.AssetId && lane.Store.Dispatches == 1,
            "An explicit enqueue after confirmed expiry may allocate one replacement.");
        Check((await lane.Jobs.EnqueueAsync(Request())).JobId == replacement.JobId && lane.Store.Dispatches == 1,
            "The replacement must deduplicate normally.");
        Check(lane.Jobs.Get(first.JobId)!.State == MediaRenderJobState.Expired,
            "Old expiry must remain inspectable after replacement.");
    }

    private static async Task MissingAssetRequiresRecoveryAsync()
    {
        var clock = new Clock();
        var original = new Lane(clock);
        var first = await SucceedAsync(original);
        var backup = Backup(original);
        var missing = backup with { Assets = backup.Assets with { Assets = Array.Empty<AssetLifecycleBackupAsset>() } };
        var lane = Restore(missing, clock);
        await CheckRecoveryAsync(lane, first.JobId);
        Check(lane.Jobs.List().Single().State == MediaRenderJobState.Failed &&
            lane.Jobs.GetMediaPipelineProjection().Observability.SucceededCount == 0,
            "List and pipeline projection must not report phantom success.");
        await ExpectConflictAsync(() => lane.Jobs.EnqueueAsync(Request() with { Payload = "different" }));

        lane = Restore(Backup(lane), clock);
        await CheckRecoveryAsync(lane, first.JobId);
        lane.Assets.RestoreBackup(backup.Assets);
        var recovered = await lane.Jobs.EnqueueAsync(Request());
        Check(recovered.JobId == first.JobId && recovered.State == MediaRenderJobState.Succeeded &&
            recovered.AssetUrl == first.AssetUrl && recovered.Error is null && lane.Store.Dispatches == 0,
            "Restoring the original usable asset must recover the same job without dispatch.");

        lane = Restore(missing, clock);
        clock.Advance(TimeSpan.FromDays(2));
        lane = Restore(Backup(lane), clock);
        await CheckRecoveryAsync(lane, first.JobId);
        // Missing metadata cannot prove expiry: retention may have changed. Only
        // restoring the actual aged asset supplies a confirmed expiry tombstone.
        lane.Assets.RestoreBackup(backup.Assets);
        Check(lane.Jobs.Get(first.JobId)!.State == MediaRenderJobState.Expired && lane.Store.Dispatches == 0,
            "Recovering an expired asset must expire, never resurrect, its job.");

        foreach (var legacyFlag in new bool?[] { null, false })
        {
            var noId = missing with { Jobs = missing.Jobs with
            {
                Jobs = missing.Jobs.Jobs.Select(row => row with { AssetId = null, DispatchStarted = legacyFlag }).ToArray()
            } };
            lane = Restore(noId, clock);
            await CheckRecoveryAsync(lane, first.JobId);
        }
    }

    private static async Task PersistentRetentionAndReleaseAsync()
    {
        foreach (var retention in new[] { "persist", "pin", "approval", "long-term" })
        {
            var clock = new Clock();
            var lane = new Lane(clock);
            var request = Request() with { RequiresApproval = retention == "approval", PersistOnApproval = retention == "approval" };
            var first = await SucceedAsync(lane, request);
            if (retention == "persist") await lane.Assets.ApplyLifecycleAsync(first.AssetId!, new(Persist: true));
            if (retention == "pin") await lane.Assets.ApplyLifecycleAsync(first.AssetId!, new(Pin: true));
            if (retention == "approval") await lane.Assets.ApplyLifecycleAsync(first.AssetId!, new(ApprovalState: AssetApprovalState.Approved));
            var backup = Backup(lane);
            if (retention == "long-term")
            {
                backup = backup with
                {
                    Assets = backup.Assets with { Assets = backup.Assets.Assets.Select(row => row with
                    {
                        Policy = row.Policy with { LongTermCache = true },
                        RetentionState = AssetRetentionState.Persisted, StorageClass = AssetStorageClass.LongTermObjectStorage
                    }).ToArray() },
                    Jobs = backup.Jobs with { Jobs = backup.Jobs.Jobs.Select(row => row with
                        { Policy = row.Policy with { LongTermCache = true } }).ToArray() }
                };
            }
            clock.Advance(TimeSpan.FromDays(1));
            lane = Restore(backup, clock);
            Check(lane.Jobs.Get(first.JobId)!.State == MediaRenderJobState.Succeeded && lane.Store.Dispatches == 0,
                $"Actual {retention} retention must outlive the job's cache TTL.");
            lane = Restore(Backup(lane), clock);
            Check(lane.Jobs.Get(first.JobId)!.State == MediaRenderJobState.Succeeded,
                $"Repeated restore must preserve {retention} retention.");

            // A missing durable asset is still recoverable after the cache TTL.
            lane.Assets.RestoreBackup(backup.Assets with { Assets = Array.Empty<AssetLifecycleBackupAsset>() });
            Check(lane.Jobs.Get(first.JobId) is { State: MediaRenderJobState.Failed, Error: RecoveryRequired },
                "Durable-retention metadata in a job is no substitute for its actual asset.");
            lane.Assets.RestoreBackup(backup.Assets);
            Check(lane.Jobs.Get(first.JobId)!.State == MediaRenderJobState.Succeeded && lane.Store.Dispatches == 0,
                "Recovering durable assets must clear only the asset-recovery failure.");

            if (retention == "long-term") continue; // Policy persistence has no manual release.
            await lane.Assets.ApplyLifecycleAsync(first.AssetId!, retention switch
            {
                "persist" => new(Persist: false),
                "pin" => new(Pin: false),
                _ => new AssetLifecycleMutationRequest(ApprovalState: AssetApprovalState.Rejected)
            });
            Check(lane.Jobs.Get(first.JobId)!.State == MediaRenderJobState.Expired &&
                lane.Jobs.GetMediaPipelineProjection().Idempotency.TrackedKeys == 0,
                "Releasing persistence/pinning must use original asset age and release known-expired keys.");
        }
    }

    private static async Task UncertaintySurvivesAgeAndRestoreAsync()
    {
        var clock = new Clock();
        var original = new Lane(clock);
        var first = await SucceedAsync(original);
        var backup = Backup(original);
        clock.Advance(TimeSpan.FromDays(2));
        foreach (var state in new[] { MediaRenderJobState.Failed, MediaRenderJobState.Running, MediaRenderJobState.Queued })
        foreach (var dispatchFlag in new bool?[] { true, null })
        {
            var uncertain = backup with { Jobs = backup.Jobs with
            {
                Jobs = backup.Jobs.Jobs.Select(row => row with
                    { State = state, Error = "synthetic uncertain dispatch", DispatchStarted = dispatchFlag }).ToArray()
            } };
            var lane = Restore(uncertain, clock);
            lane = Restore(Backup(lane), clock);
            var replay = await lane.Jobs.EnqueueAsync(Request());
            Check(replay.JobId == first.JobId && replay.State == state &&
                replay.Error == "synthetic uncertain dispatch" && lane.Store.Dispatches == 0,
                "Asset age/expiry and restore must not clear an unresolved dispatch fence or restart restored intake.");
            await ExpectConflictAsync(() => lane.Jobs.EnqueueAsync(Request() with { Payload = "different" }));
        }
    }

    private static async Task OldExpiryCannotEvictReplacementAsync()
    {
        var clock = new Clock();
        var assets = new AssetLifecycleService(clock);
        var policy = new AssetLifecyclePolicy(Ttl, false, 4096);
        var old = await assets.StoreAsync("test", "payload", policy: policy);
        var oldBackup = assets.ExportBackup().Assets.Single();
        clock.Advance(Ttl);
        var fresh = await assets.StoreAsync("test", "payload", policy: policy);
        var backup = assets.ExportBackup();
        var freshBackup = backup.Assets.Single(row => row.AssetId == fresh.AssetId);
        // Order deliberately puts an unswept old row after its current replacement.
        assets.RestoreBackup(backup with { Assets = new[] { freshBackup, oldBackup } });
        Check(assets.Inspect(old.AssetId) is { RetentionState: AssetRetentionState.Expired } &&
            (await assets.StoreAsync("test", "payload", policy: policy)).AssetId == fresh.AssetId,
            "Reconciling an older expired row must not evict a current cache reservation.");
    }

    private static async Task CheckRecoveryAsync(Lane lane, string jobId)
    {
        var status = lane.Jobs.Get(jobId)!;
        var replay = await lane.Jobs.EnqueueAsync(Request());
        Check(status is { State: MediaRenderJobState.Failed, Error: RecoveryRequired, AssetUrl: null } &&
            replay.JobId == jobId && replay.State == MediaRenderJobState.Failed && replay.AssetUrl is null &&
            lane.Store.Dispatches == 0 && lane.Jobs.GetMediaPipelineProjection().Idempotency.TrackedKeys == 1 &&
            lane.Jobs.ExportBackup().Jobs.Single().DispatchStarted == true,
            "Missing assets must report recovery-required, retaining the same fenced job without dispatch.");
    }

    private static async Task<MediaRenderJobStatus> SucceedAsync(Lane lane, MediaRenderJobEnqueueRequest? request = null)
    {
        var result = await lane.Jobs.EnqueueAsync(request ?? Request());
        Check(result.State == MediaRenderJobState.Succeeded && !string.IsNullOrWhiteSpace(result.AssetUrl),
            "Synthetic setup must finish storage synchronously with a usable asset.");
        return result;
    }

    private static MediaRenderJobEnqueueRequest Request() => new(MediaRenderJobType.DocumentPreviewImage,
        "restore:key", "test", "payload", "synthetic-restore", Ttl, 4096, OwnerId: "synthetic-owner");
    private static MediaFactoryRuntimeBackupPackage Backup(Lane lane) =>
        JsonSerializer.Deserialize<MediaFactoryRuntimeBackupPackage>(JsonSerializer.Serialize(
            MediaFactoryRuntimeBackup.Export(lane.Store, lane.Jobs)))!;
    private static Lane Restore(MediaFactoryRuntimeBackupPackage backup, Clock clock)
    {
        var lane = new Lane(clock);
        MediaFactoryRuntimeBackup.Restore(lane.Store, lane.Jobs, backup);
        return lane;
    }
    private static async Task ExpectConflictAsync(Func<Task<MediaRenderJobStatus>> action)
    {
        try { await action(); }
        catch (MediaRenderJobConflictException) { return; }
        throw new InvalidOperationException("A retained reservation must reject a conflicting request.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
    private sealed class Lane
    {
        public AssetLifecycleService Assets { get; }
        public CountingAssets Store { get; }
        public MediaRenderJobService Jobs { get; }
        public Lane(Clock clock)
        {
            Assets = new AssetLifecycleService(clock);
            Store = new CountingAssets(Assets);
            Jobs = new MediaRenderJobService(Store, clock);
        }
    }
    private sealed class CountingAssets(AssetLifecycleService inner) : IAssetLifecycleService
    {
        public int Dispatches { get; private set; }
        public Task<AssetRenderResult> StoreAsync(string category, string content, string? source = null,
            AssetLifecyclePolicy? policy = null, CancellationToken cancellationToken = default)
        {
            Dispatches++;
            return inner.StoreAsync(category, content, source, policy, cancellationToken);
        }
        public AssetCatalogItem? Resolve(string assetId) => inner.Resolve(assetId);
        public AssetCatalogItem? Inspect(string assetId) => inner.Inspect(assetId);
        public IReadOnlyList<AssetCatalogItem> List() => inner.List();
        public Task<AssetCatalogItem?> ApplyLifecycleAsync(string assetId, AssetLifecycleMutationRequest request,
            CancellationToken cancellationToken = default) => inner.ApplyLifecycleAsync(assetId, request, cancellationToken);
        public AssetLifecycleSweepResult SweepExpired(DateTimeOffset? utcNow = null) => inner.SweepExpired(utcNow);
        public PipelineProjection GetApprovalPipelineProjection() => inner.GetApprovalPipelineProjection();
        public AssetLifecycleBackupPackage ExportBackup() => inner.ExportBackup();
        public void RestoreBackup(AssetLifecycleBackupPackage backup) => inner.RestoreBackup(backup);
    }
}
