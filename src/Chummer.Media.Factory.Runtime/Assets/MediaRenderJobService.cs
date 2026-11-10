using Chummer.Media.Contracts;
using Chummer.Run.Contracts.Observability;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Chummer.Run.AI.Services.Assets;

public interface IMediaRenderJobService
{
    Task<MediaRenderJobStatus> EnqueueAsync(
        MediaRenderJobEnqueueRequest request,
        CancellationToken cancellationToken = default);

    MediaRenderJobStatus? Get(string jobId);
    IReadOnlyList<MediaRenderJobStatus> List();
    PipelineProjection GetMediaPipelineProjection();
    MediaRenderJobBackupPackage ExportBackup();
    void RestoreBackup(MediaRenderJobBackupPackage backup);
}

/// <summary>
/// Instance-local job reservations. Hosts must share one instance per intake lane.
/// This store has no cross-process uniqueness or crash-durable dispatch fence; it
/// must not be used to claim multi-process exactly-once provider execution.
/// </summary>
public sealed class MediaRenderJobService : IMediaRenderJobService
{
    private sealed class MediaRenderJobRow
    {
        public required string JobId { get; init; }
        public required MediaRenderJobType JobType { get; init; }
        public required string DeduplicationKey { get; init; }
        public string? OwnerId { get; init; }
        public required string RequestFingerprint { get; init; }
        public bool DispatchStarted { get; set; }
        public required string Category { get; init; }
        public required string Payload { get; init; }
        public required string Source { get; init; }
        public required AssetLifecyclePolicy Policy { get; init; }
        public required DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public DateTimeOffset? RestoredAtUtc { get; set; }
        public string? AssetId { get; set; }
        public string? Error { get; set; }
        public MediaRenderJobState State { get; set; }
    }

    private readonly ConcurrentDictionary<string, MediaRenderJobRow> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly record struct ReservationKey(string? OwnerId, string Key);

    // Only accessed under _sync. A row reference is the reservation ownership token,
    // including when a backup reuses a job ID while an old continuation is in flight.
    private readonly Dictionary<ReservationKey, MediaRenderJobRow> _reservations = new();
    private readonly ConcurrentQueue<PipelineDeadLetterEntry> _deadLetters = new();
    private readonly IAssetLifecycleService _assetLifecycle;
    private readonly object _sync = new();
    private long _enqueueCount;
    private long _dedupeReuseCount;

    public MediaRenderJobService(IAssetLifecycleService assetLifecycle)
    {
        _assetLifecycle = assetLifecycle;
    }

    public Task<MediaRenderJobStatus> EnqueueAsync(
        MediaRenderJobEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedRequest = Normalize(request);

        var policy = BuildPolicy(normalizedRequest);
        var fingerprint = BuildRequestFingerprint(normalizedRequest.JobType, normalizedRequest.Category,
            normalizedRequest.Payload, normalizedRequest.Source, policy);
        MediaRenderJobRow row;
        lock (_sync)
        {
            var key = BuildReservationKey(normalizedRequest.OwnerId, normalizedRequest.DeduplicationKey);
            if (TryGetReusableJob(key, out var existing))
            {
                if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    EnqueueDeadLetter(existing.JobId, "deduplication-request-conflict", fingerprint);
                    throw new MediaRenderJobConflictException(existing.JobId);
                }

                Interlocked.Increment(ref _dedupeReuseCount);
                return Task.FromResult(ToStatus(existing));
            }

            row = new MediaRenderJobRow
            {
                JobId = $"job_{Guid.NewGuid():N}",
                JobType = normalizedRequest.JobType,
                DeduplicationKey = normalizedRequest.DeduplicationKey,
                OwnerId = normalizedRequest.OwnerId,
                RequestFingerprint = fingerprint,
                Category = normalizedRequest.Category,
                Payload = normalizedRequest.Payload,
                Source = normalizedRequest.Source,
                Policy = policy,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                State = MediaRenderJobState.Queued
            };

            _jobs[row.JobId] = row;
            _reservations.Add(key, row);
            Interlocked.Increment(ref _enqueueCount);
        }

        _ = ProcessAsync(row);

        return Task.FromResult(ToStatus(row));
    }

    public MediaRenderJobStatus? Get(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId) || !_jobs.TryGetValue(jobId, out var row))
        {
            return null;
        }

        RefreshExpiry(row);
        return ToStatus(row);
    }

    public IReadOnlyList<MediaRenderJobStatus> List() =>
        _jobs.Values
            .OrderByDescending(static row => row.CreatedAtUtc)
            .Select(row =>
            {
                RefreshExpiry(row);
                return ToStatus(row);
            })
            .ToArray();

    public PipelineProjection GetMediaPipelineProjection()
    {
        lock (_sync)
        {
            var queued = 0;
            var running = 0;
            var succeeded = 0;
            var failed = 0;
            var expired = 0;
            foreach (var row in _jobs.Values)
            {
                RefreshExpiry(row);
                switch (row.State)
                {
                    case MediaRenderJobState.Queued:
                        queued++;
                        break;
                    case MediaRenderJobState.Running:
                        running++;
                        break;
                    case MediaRenderJobState.Succeeded:
                        succeeded++;
                        break;
                    case MediaRenderJobState.Failed:
                        failed++;
                        break;
                    case MediaRenderJobState.Expired:
                        expired++;
                        break;
                }
            }

            return new PipelineProjection(
                Pipeline: "media",
                Observability: new PipelineObservabilityProjection(
                    ProcessedCount: ToInt(_enqueueCount),
                    ActiveCount: queued + running,
                    SucceededCount: succeeded,
                    FailedCount: failed,
                    DuplicateCount: ToInt(_dedupeReuseCount),
                    IgnoredCount: expired),
                Idempotency: new PipelineIdempotencyProjection(
                    TrackedKeys: _reservations.Count,
                    ReplayCount: ToInt(_dedupeReuseCount),
                    LastReplayAtUtc: null),
                Cost: new PipelineCostProjection(
                    EstimatedUsd: 0,
                    BudgetUnitsConsumed: 0),
                DeadLetter: new PipelineDeadLetterProjection(
                    Count: _deadLetters.Count,
                    Recent: _deadLetters.Take(25).ToArray()));
        }
    }

    public MediaRenderJobBackupPackage ExportBackup()
    {
        lock (_sync)
        {
            return new MediaRenderJobBackupPackage(
                ContractFamily: MediaFactoryRuntimeBackup.ContractFamily,
                Jobs: _jobs.Values
                    .OrderBy(static row => row.JobId, StringComparer.OrdinalIgnoreCase)
                    .Select(static row => new MediaRenderJobBackupRow(
                        JobId: row.JobId,
                        JobType: row.JobType,
                        DeduplicationKey: row.DeduplicationKey,
                        Category: row.Category,
                        Payload: row.Payload,
                        Source: row.Source,
                        Policy: row.Policy,
                        CreatedAtUtc: row.CreatedAtUtc,
                        StartedAtUtc: row.StartedAtUtc,
                        CompletedAtUtc: row.CompletedAtUtc,
                        AssetId: row.AssetId,
                        Error: row.Error,
                        State: row.State,
                        OwnerId: row.OwnerId,
                        DispatchStarted: row.DispatchStarted))
                    .ToArray(),
                EnqueueCount: _enqueueCount,
                DedupeReuseCount: _dedupeReuseCount,
                DeadLetters: _deadLetters.ToArray());
        }
    }

    public void RestoreBackup(MediaRenderJobBackupPackage backup)
    {
        ArgumentNullException.ThrowIfNull(backup);
        if (!string.Equals(backup.ContractFamily, MediaFactoryRuntimeBackup.ContractFamily, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported media-job backup contract family '{backup.ContractFamily}'.");
        }

        lock (_sync)
        {
            var restoredJobs = new Dictionary<string, MediaRenderJobRow>(StringComparer.OrdinalIgnoreCase);
            var restoredReservations = new Dictionary<ReservationKey, MediaRenderJobRow>();
            foreach (var job in backup.Jobs)
            {
                var restored = new MediaRenderJobRow
                {
                    JobId = job.JobId,
                    JobType = job.JobType,
                    DeduplicationKey = job.DeduplicationKey,
                    OwnerId = NormalizeOwner(job.OwnerId),
                    RequestFingerprint = BuildRequestFingerprint(job.JobType, job.Category, job.Payload, job.Source, job.Policy),
                    // A legacy failed row cannot prove that no dispatch happened.
                    DispatchStarted = job.DispatchStarted ?? job.State != MediaRenderJobState.Queued,
                    Category = job.Category,
                    Payload = job.Payload,
                    Source = job.Source,
                    Policy = job.Policy,
                    CreatedAtUtc = job.CreatedAtUtc,
                    StartedAtUtc = job.StartedAtUtc,
                    CompletedAtUtc = job.CompletedAtUtc,
                    RestoredAtUtc = job.State == MediaRenderJobState.Succeeded ? DateTimeOffset.UtcNow : null,
                    AssetId = job.AssetId,
                    Error = job.Error,
                    State = job.State
                };
                restoredJobs.Add(restored.JobId, restored);
                if (restored.State != MediaRenderJobState.Expired &&
                    (restored.State != MediaRenderJobState.Failed || restored.DispatchStarted))
                {
                    if (!restoredReservations.TryAdd(BuildReservationKey(restored.OwnerId, restored.DeduplicationKey), restored))
                    {
                        throw new InvalidOperationException("Backup contains duplicate active owner-scoped reservations.");
                    }
                }
            }

            // Validate all reservations before replacing live state.
            _jobs.Clear();
            _reservations.Clear();
            foreach (var (id, row) in restoredJobs)
            {
                _jobs[id] = row;
            }
            foreach (var (key, row) in restoredReservations)
            {
                _reservations.Add(key, row);
            }
            while (_deadLetters.TryDequeue(out _))
            {
            }

            foreach (var deadLetter in backup.DeadLetters)
            {
                _deadLetters.Enqueue(deadLetter);
            }

            Interlocked.Exchange(ref _enqueueCount, backup.EnqueueCount);
            Interlocked.Exchange(ref _dedupeReuseCount, backup.DedupeReuseCount);
        }
    }

    private async Task ProcessAsync(MediaRenderJobRow row)
    {
        try
        {
            lock (_sync)
            {
                if (row.State != MediaRenderJobState.Queued || !OwnsReservation(row))
                {
                    return;
                }

                row.State = MediaRenderJobState.Running;
                row.StartedAtUtc = DateTimeOffset.UtcNow;
            }

            var payloadBytes = Encoding.UTF8.GetByteCount(row.Payload);
            if (row.Policy.MaxBytes > 0 && payloadBytes > row.Policy.MaxBytes)
            {
                throw new InvalidOperationException($"Payload exceeds MaxBytes policy ({row.Policy.MaxBytes}).");
            }

            lock (_sync)
            {
                if (!OwnsReservation(row))
                {
                    return;
                }
                // Once dispatch begins, a thrown exception has an uncertain outcome.
                // Keep its reservation so enqueue cannot automatically replay it.
                row.DispatchStarted = true;
            }

            var asset = await _assetLifecycle.StoreAsync(
                category: row.Category,
                content: row.Payload,
                source: row.Source,
                policy: row.Policy,
                cancellationToken: CancellationToken.None);

            lock (_sync)
            {
                row.AssetId = asset.AssetId;
                row.State = MediaRenderJobState.Succeeded;
                row.CompletedAtUtc = DateTimeOffset.UtcNow;
                row.RestoredAtUtc = null;
                row.Error = null;
            }
        }
        catch (Exception exception)
        {
            lock (_sync)
            {
                row.Error = exception.Message;
                row.State = MediaRenderJobState.Failed;
                row.CompletedAtUtc = DateTimeOffset.UtcNow;
                row.RestoredAtUtc = null;
                EnqueueDeadLetter(row.JobId, exception.Message, row.RequestFingerprint);
                if (!row.DispatchStarted)
                {
                    ReleaseReservation(row);
                }
            }
        }
    }

    // Caller holds _sync for the complete lookup/create/reserve operation.
    private bool TryGetReusableJob(ReservationKey key, out MediaRenderJobRow row)
    {
        if (!_reservations.TryGetValue(key, out var existing))
        {
            row = null!;
            return false;
        }

        RefreshExpiry(existing);
        if (existing.State == MediaRenderJobState.Expired ||
            (existing.State == MediaRenderJobState.Failed && !existing.DispatchStarted))
        {
            ReleaseReservation(existing);
            row = null!;
            return false;
        }

        row = existing;
        return true;
    }

    private bool OwnsReservation(MediaRenderJobRow row) =>
        _reservations.TryGetValue(BuildReservationKey(row.OwnerId, row.DeduplicationKey), out var owner)
        && ReferenceEquals(owner, row);

    private void ReleaseReservation(MediaRenderJobRow row)
    {
        if (OwnsReservation(row))
        {
            _reservations.Remove(BuildReservationKey(row.OwnerId, row.DeduplicationKey));
        }
    }

    private void EnqueueDeadLetter(string jobId, string reason, string fingerprint)
    {
        _deadLetters.Enqueue(new PipelineDeadLetterEntry(jobId, reason, DateTimeOffset.UtcNow, fingerprint));
        while (_deadLetters.Count > 200 && _deadLetters.TryDequeue(out _))
        {
        }
    }

    private static ReservationKey BuildReservationKey(string? ownerId, string key) =>
        new(ownerId, key.Trim().ToUpperInvariant());

    private static string? NormalizeOwner(string? ownerId)
    {
        if (ownerId is not null && string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("OwnerId must be nonempty when supplied.", nameof(ownerId));
        }
        return ownerId?.Trim();
    }

    private static string BuildRequestFingerprint(MediaRenderJobType jobType, string category,
        string payload, string source, AssetLifecyclePolicy policy) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = 1,
            JobType = (int)jobType,
            Category = category,
            Payload = payload,
            Source = source,
            CacheTtlTicks = policy.CacheTtl.Ticks,
            policy.LongTermCache,
            policy.MaxBytes,
            policy.RequiresApproval,
            policy.PersistOnApproval,
            StorageClass = (int)policy.StorageClass,
            policy.AllowPersistentPinning
        })));

    private void RefreshExpiry(MediaRenderJobRow row)
    {
        lock (_sync)
        {
            var expiryAnchorUtc = row.RestoredAtUtc ?? row.CompletedAtUtc;
            if (row.State == MediaRenderJobState.Succeeded && !row.Policy.LongTermCache &&
                row.Policy.CacheTtl > TimeSpan.Zero && expiryAnchorUtc is { } &&
                expiryAnchorUtc + row.Policy.CacheTtl <= DateTimeOffset.UtcNow)
            {
                row.State = MediaRenderJobState.Expired;
                row.RestoredAtUtc = null;
                ReleaseReservation(row);
            }
        }
    }

    private static MediaRenderJobEnqueueRequest Normalize(MediaRenderJobEnqueueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeduplicationKey))
        {
            throw new ArgumentException("DeduplicationKey is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            throw new ArgumentException("Category is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Payload))
        {
            throw new ArgumentException("Payload is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Source))
        {
            throw new ArgumentException("Source is required.", nameof(request));
        }

        return request with
        {
            OwnerId = NormalizeOwner(request.OwnerId),
            DeduplicationKey = request.DeduplicationKey.Trim(),
            Category = request.Category.Trim(),
            Payload = request.Payload.Trim(),
            Source = request.Source.Trim()
        };
    }

    private static AssetLifecyclePolicy BuildPolicy(MediaRenderJobEnqueueRequest request)
    {
        var cacheTtl = request.CacheTtl is { } ttl && ttl > TimeSpan.Zero
            ? ttl
            : TimeSpan.FromDays(7);
        var maxBytes = request.MaxBytes > 0
            ? request.MaxBytes
            : Math.Max(4096, Encoding.UTF8.GetByteCount(request.Payload) * 2);

        return new AssetLifecyclePolicy(
            CacheTtl: cacheTtl,
            LongTermCache: false,
            MaxBytes: maxBytes,
            RequiresApproval: request.RequiresApproval,
            PersistOnApproval: request.PersistOnApproval,
            StorageClass: AssetStorageClass.ObjectStorage,
            AllowPersistentPinning: request.AllowPersistentPinning);
    }

    private MediaRenderJobStatus ToStatus(MediaRenderJobRow row)
    {
        lock (_sync)
        {
            var asset = row.AssetId is null ? null : _assetLifecycle.Resolve(row.AssetId);
            return new MediaRenderJobStatus(
                JobId: row.JobId,
                JobType: row.JobType,
                State: row.State,
                CreatedAtUtc: row.CreatedAtUtc,
                StartedAtUtc: row.StartedAtUtc,
                CompletedAtUtc: row.CompletedAtUtc,
                AssetId: row.AssetId,
                AssetUrl: asset?.Url,
                CacheTtl: row.Policy.CacheTtl,
                Error: row.Error,
                ApprovalState: asset?.ApprovalState,
                RetentionState: asset?.RetentionState,
                StorageClass: asset?.StorageClass);
        }
    }

    private static int ToInt(long value) => value > int.MaxValue ? int.MaxValue : (int)value;
}

/// <summary>A reserved owner/key cannot be reused for a different canonical render request.</summary>
public sealed class MediaRenderJobConflictException(string existingJobId)
    : InvalidOperationException("The deduplication key is already reserved for a different render request.")
{
    public string Code => "media-render-idempotency-conflict";
    public string ExistingJobId { get; } = existingJobId;
}
