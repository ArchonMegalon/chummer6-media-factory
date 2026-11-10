using System.Text.Json;
using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;
using Chummer.Run.Contracts.Observability;

internal static class LifecycleReservationRegression
{
    public static async Task RunAsync()
    {
        await RejectedMutationsAreAtomicAsync();
        await PersistenceIntentSurvivesOmissionAndRestoreAsync();
        await ConcurrentReservationsAreScopedAndBoundAsync();
        await UncertainDispatchIsNotReplayedAsync();
        await FailedOwnerCannotRemoveReplacementAsync();
        await PredispatchFailureCanBeRetriedAsync();
        Console.WriteLine("lifecycle/reservation regressions ok (6 groups; fake storage dispatch only)");
    }

    private static async Task RejectedMutationsAreAtomicAsync()
    {
        var assets = new AssetLifecycleService();
        var deniedPin = await assets.StoreAsync("test", "payload", policy: new(
            TimeSpan.FromMinutes(1), false, 4096, RequiresApproval: true, AllowPersistentPinning: false));
        await RejectWithoutChangesAsync(assets, deniedPin.AssetId,
            new(ApprovalState: AssetApprovalState.Approved, Pin: true, Persist: true));

        var approved = await assets.StoreAsync("test", "other", policy: new(
            TimeSpan.FromMinutes(1), false, 4096, RequiresApproval: true));
        await assets.ApplyLifecycleAsync(approved.AssetId,
            new(ApprovalState: AssetApprovalState.Approved, Pin: true, Persist: true));
        await RejectWithoutChangesAsync(assets, approved.AssetId,
            new(ApprovalState: AssetApprovalState.Rejected, Pin: true));
        await RejectWithoutChangesAsync(assets, approved.AssetId,
            new(ApprovalState: AssetApprovalState.Pending, Pin: false, Persist: true));
        var rejected = await assets.ApplyLifecycleAsync(approved.AssetId,
            new(ApprovalState: AssetApprovalState.Rejected));
        Check(rejected is { RetentionState: AssetRetentionState.Rejected, IsPinned: false },
            "An accepted rejection must still clear pinning and record rejection.");
    }

    private static async Task RejectWithoutChangesAsync(AssetLifecycleService assets, string id,
        AssetLifecycleMutationRequest mutation)
    {
        var before = assets.ExportBackup();
        await ThrowsAsync<InvalidOperationException>(() => assets.ApplyLifecycleAsync(id, mutation));
        var after = assets.ExportBackup();
        Check(before.Assets.SequenceEqual(after.Assets), "Rejected mutation changed asset fields.");
        Check(before.LifecycleMutations == after.LifecycleMutations &&
            before.ReplayMutations == after.ReplayMutations &&
            before.MutationFingerprints.SequenceEqual(after.MutationFingerprints),
            "Rejected mutation must not count as a committed mutation or replay.");
        Check(after.DeadLetters.Count == before.DeadLetters.Count + 1,
            "Rejected mutation must remain observable.");
    }

    private static async Task PersistenceIntentSurvivesOmissionAndRestoreAsync()
    {
        var assets = new AssetLifecycleService();
        var asset = await assets.StoreAsync("test", "persist", policy: new(TimeSpan.FromMinutes(1), false, 4096));
        await assets.ApplyLifecycleAsync(asset.AssetId, new(Persist: true));
        Check((await assets.ApplyLifecycleAsync(asset.AssetId, new(Reason: "note")))?.RetentionState
            == AssetRetentionState.Persisted, "Omitted Persist must retain explicit persistence.");
        await assets.ApplyLifecycleAsync(asset.AssetId, new(Pin: true));
        Check((await assets.ApplyLifecycleAsync(asset.AssetId, new()))?.IsPinned == true,
            "Omitted Pin must preserve the pin.");
        var restored = new AssetLifecycleService();
        restored.RestoreBackup(RoundTrip(assets.ExportBackup()));
        Check((await restored.ApplyLifecycleAsync(asset.AssetId, new(Pin: false))) is
            { RetentionState: AssetRetentionState.Persisted, StorageClass: AssetStorageClass.LongTermObjectStorage },
            "Persistence intent hidden under a pin must survive backup and unpin.");
        Check((await restored.ApplyLifecycleAsync(asset.AssetId, new(Persist: false))) is
            { RetentionState: AssetRetentionState.CacheOnly, StorageClass: AssetStorageClass.ObjectStorage },
            "Explicit false must clear manual persistence.");

        await restored.ApplyLifecycleAsync(asset.AssetId, new(Pin: true));
        var pinnedOnly = new AssetLifecycleService();
        pinnedOnly.RestoreBackup(RoundTrip(restored.ExportBackup()));
        Check((await pinnedOnly.ApplyLifecycleAsync(asset.AssetId, new(Pin: false)))?.RetentionState
            == AssetRetentionState.CacheOnly, "A pin alone must not invent persistence intent.");

        var legacy = assets.ExportBackup();
        legacy = legacy with { Assets = legacy.Assets.Select(row => row with
            { RetentionState = AssetRetentionState.Persisted, IsPinned = false, ExplicitPersist = null }).ToArray() };
        restored.RestoreBackup(RoundTrip(legacy));
        Check((await restored.ApplyLifecycleAsync(asset.AssetId, new()))?.RetentionState
            == AssetRetentionState.Persisted, "Legacy manual persistence must survive an omitted field.");
    }

    private static async Task ConcurrentReservationsAreScopedAndBoundAsync()
    {
        var gate = NewGate();
        var fake = new ControlledAssets { BeforeStore = _ => gate.Task };
        var jobs = new MediaRenderJobService(fake);
        var request = Request();
        var start = NewGate();
        var calls = Enumerable.Range(0, 64).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            return await jobs.EnqueueAsync(request);
        })).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(5));
        var winner = results[0];
        Check(results.All(row => row.JobId == winner.JobId) && fake.Dispatches == 1,
            "Parallel identical calls must reserve one job and dispatch once.");

        var canonical = request with { OwnerId = " owner-A ", DeduplicationKey = " KEY ",
            Category = " test ", Payload = " payload ", Source = " test-source ",
            CacheTtl = TimeSpan.FromDays(7), MaxBytes = 4096 };
        Check((await jobs.EnqueueAsync(canonical)).JobId == winner.JobId,
            "Equivalent normalized requests and effective defaults must dedupe.");
        var mismatches = new[]
        {
            request with { Payload = "different" }, request with { Category = "different" },
            request with { Source = "different" }, request with { JobType = MediaRenderJobType.DocumentPdf },
            request with { CacheTtl = TimeSpan.FromDays(1) }, request with { MaxBytes = 512 },
            request with { RequiresApproval = true }, request with { PersistOnApproval = true },
            request with { AllowPersistentPinning = false }
        };
        foreach (var mismatch in mismatches)
        {
            var conflict = await ThrowsAsync<MediaRenderJobConflictException>(() => jobs.EnqueueAsync(mismatch));
            Check(conflict.Code == "media-render-idempotency-conflict" && conflict.ExistingJobId == winner.JobId,
                "A mismatch must expose a stable explicit conflict.");
        }
        Check(fake.Dispatches == 1, "Conflicts must not dispatch.");
        Check(jobs.GetMediaPipelineProjection().DeadLetter.Count == mismatches.Length,
            "Conflicts must appear in the bounded dead-letter projection.");
        var otherOwner = await jobs.EnqueueAsync(request with { OwnerId = "owner-B", Payload = "different" });
        var legacy = await jobs.EnqueueAsync(request with { OwnerId = null });
        Check(otherOwner.JobId != winner.JobId && legacy.JobId != winner.JobId && fake.Dispatches == 3,
            "Owners and the legacy namespace must have separate reservations.");
        await ThrowsAsync<ArgumentException>(() => jobs.EnqueueAsync(request with { OwnerId = " " }));
        gate.SetResult();
        await WaitAsync(() => jobs.Get(winner.JobId)?.State == MediaRenderJobState.Succeeded);

        var backup = RoundTrip(jobs.ExportBackup());
        var restored = new MediaRenderJobService(fake);
        restored.RestoreBackup(backup);
        Check((await restored.EnqueueAsync(request)).JobId == winner.JobId,
            "Owner scope and canonical binding must survive backup.");
        await ThrowsAsync<MediaRenderJobConflictException>(() => restored.EnqueueAsync(mismatches[0]));
        var duplicate = backup.Jobs.Single(row => row.JobId == winner.JobId) with { JobId = "duplicate" };
        await ThrowsAsync<InvalidOperationException>(() =>
        {
            restored.RestoreBackup(backup with { Jobs = backup.Jobs.Append(duplicate).ToArray() });
            return Task.CompletedTask;
        });
        Check((await restored.EnqueueAsync(request)).JobId == winner.JobId,
            "Rejected duplicate reservations in a backup must not replace live state.");
    }

    private static async Task UncertainDispatchIsNotReplayedAsync()
    {
        var gate = NewGate();
        var fake = new ControlledAssets { BeforeStore = async _ =>
        {
            await gate.Task;
            throw new TimeoutException("fake uncertain completion");
        } };
        var jobs = new MediaRenderJobService(fake);
        var first = await jobs.EnqueueAsync(Request());
        gate.SetResult();
        await WaitAsync(() => jobs.Get(first.JobId)?.State == MediaRenderJobState.Failed);
        Check((await jobs.EnqueueAsync(Request())).JobId == first.JobId && fake.Dispatches == 1,
            "A post-dispatch failure must not trigger automatic replay.");
        await ThrowsAsync<MediaRenderJobConflictException>(() => jobs.EnqueueAsync(Request() with { Payload = "other" }));
        var backup = RoundTrip(jobs.ExportBackup());
        foreach (var legacy in new[] { false, true })
        {
            var restoredFake = new ControlledAssets();
            var restored = new MediaRenderJobService(restoredFake);
            restored.RestoreBackup(legacy ? backup with
            {
                Jobs = backup.Jobs.Select(row => row with { DispatchStarted = null }).ToArray()
            } : backup);
            Check((await restored.EnqueueAsync(Request())).JobId == first.JobId && restoredFake.Dispatches == 0,
                "Restore must retain an uncertain dispatch fence, including legacy failed rows.");
        }
    }

    private static async Task FailedOwnerCannotRemoveReplacementAsync()
    {
        var oldGate = NewGate();
        var winnerGate = NewGate();
        var fake = new ControlledAssets { BeforeStore = async call =>
        {
            if (call == 1)
            {
                await oldGate.Task;
                throw new InvalidOperationException("late failure of detached owner");
            }
            await winnerGate.Task;
        } };
        var jobs = new MediaRenderJobService(fake);
        await jobs.EnqueueAsync(Request());
        var backup = jobs.ExportBackup();
        // Simulate an explicit recovery replacing the live reservation while its
        // old continuation is still pending. This is not automatic replay logic.
        jobs.RestoreBackup(backup with { Jobs = backup.Jobs.Select(row => row with
            { State = MediaRenderJobState.Failed, DispatchStarted = false }).ToArray() });
        var winner = await jobs.EnqueueAsync(Request());
        oldGate.SetResult();
        await WaitAsync(() => jobs.GetMediaPipelineProjection().DeadLetter.Count == 1);
        Check((await jobs.EnqueueAsync(Request())).JobId == winner.JobId && fake.Dispatches == 2,
            "Late failed-owner cleanup must not remove the replacement reservation.");
        winnerGate.SetResult();
        await WaitAsync(() => jobs.Get(winner.JobId)?.State == MediaRenderJobState.Succeeded);
    }

    private static async Task PredispatchFailureCanBeRetriedAsync()
    {
        var fake = new ControlledAssets();
        var jobs = new MediaRenderJobService(fake);
        var oversized = Request() with { MaxBytes = 1 };
        var first = await jobs.EnqueueAsync(oversized);
        var retry = await jobs.EnqueueAsync(oversized);
        Check(first.State == MediaRenderJobState.Failed && retry.State == MediaRenderJobState.Failed &&
            first.JobId != retry.JobId && fake.Dispatches == 0,
            "Known pre-dispatch failures may release only their own reservation.");
        Check(jobs.GetMediaPipelineProjection().Idempotency.TrackedKeys == 0,
            "Known pre-dispatch failures must release their keys.");
    }

    private static MediaRenderJobEnqueueRequest Request() => new(
        MediaRenderJobType.DocumentPreviewImage, "key", "test", "payload", "test-source", OwnerId: "owner-A");

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T exception) { return exception; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
        throw new TimeoutException("Regression condition did not complete.");
    }

    private sealed class ControlledAssets : IAssetLifecycleService
    {
        private readonly AssetLifecycleService _inner = new();
        private int _dispatches;
        public int Dispatches => Volatile.Read(ref _dispatches);
        public Func<int, Task> BeforeStore { get; init; } = _ => Task.CompletedTask;
        public async Task<AssetRenderResult> StoreAsync(string category, string content, string? source = null,
            AssetLifecyclePolicy? policy = null, CancellationToken cancellationToken = default)
        {
            await BeforeStore(Interlocked.Increment(ref _dispatches));
            return await _inner.StoreAsync(category, content, source, policy, cancellationToken);
        }
        public AssetCatalogItem? Resolve(string assetId) => _inner.Resolve(assetId);
        public IReadOnlyList<AssetCatalogItem> List() => _inner.List();
        public Task<AssetCatalogItem?> ApplyLifecycleAsync(string assetId, AssetLifecycleMutationRequest request,
            CancellationToken cancellationToken = default) => _inner.ApplyLifecycleAsync(assetId, request, cancellationToken);
        public AssetLifecycleSweepResult SweepExpired(DateTimeOffset? utcNow = null) => _inner.SweepExpired(utcNow);
        public PipelineProjection GetApprovalPipelineProjection() => _inner.GetApprovalPipelineProjection();
        public AssetLifecycleBackupPackage ExportBackup() => _inner.ExportBackup();
        public void RestoreBackup(AssetLifecycleBackupPackage backup) => _inner.RestoreBackup(backup);
    }
}
