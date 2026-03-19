using Chummer.Media.Contracts;
using Chummer.Run.Contracts.Observability;

namespace Chummer.Run.AI.Services.Assets;

public static class MediaFactoryRuntimeBackup
{
    public const string ContractFamily = "media_factory_state_backup_v1";

    public static MediaFactoryRuntimeBackupPackage Export(IAssetLifecycleService assets, IMediaRenderJobService jobs) =>
        new(
            ContractFamily,
            assets.ExportBackup(),
            jobs.ExportBackup());

    public static void Restore(
        IAssetLifecycleService assets,
        IMediaRenderJobService jobs,
        MediaFactoryRuntimeBackupPackage backup)
    {
        ArgumentNullException.ThrowIfNull(backup);
        if (!string.Equals(backup.ContractFamily, ContractFamily, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported media-factory backup contract family '{backup.ContractFamily}'.");
        }

        assets.RestoreBackup(backup.Assets);
        jobs.RestoreBackup(backup.Jobs);
    }
}

public sealed record MediaFactoryRuntimeBackupPackage(
    string ContractFamily,
    AssetLifecycleBackupPackage Assets,
    MediaRenderJobBackupPackage Jobs);

public sealed record AssetLifecycleBackupPackage(
    string ContractFamily,
    IReadOnlyList<AssetLifecycleBackupAsset> Assets,
    long StoreCount,
    long LifecycleMutations,
    long ReplayMutations,
    DateTimeOffset? LastReplayAtUtc,
    IReadOnlyList<string> MutationFingerprints,
    IReadOnlyList<PipelineDeadLetterEntry> DeadLetters);

public sealed record AssetLifecycleBackupAsset(
    string AssetId,
    string Url,
    string Category,
    string Source,
    AssetLifecyclePolicy Policy,
    DateTimeOffset CreatedAtUtc,
    string Version,
    string Content,
    string StorageKey,
    string CacheKey,
    AssetStorageClass StorageClass,
    AssetApprovalState ApprovalState,
    AssetRetentionState RetentionState,
    bool IsPinned,
    DateTimeOffset? LastAccessedAtUtc,
    int CacheHitCount,
    DateTimeOffset? ApprovedAtUtc);

public sealed record MediaRenderJobBackupPackage(
    string ContractFamily,
    IReadOnlyList<MediaRenderJobBackupRow> Jobs,
    long EnqueueCount,
    long DedupeReuseCount,
    IReadOnlyList<PipelineDeadLetterEntry> DeadLetters);

public sealed record MediaRenderJobBackupRow(
    string JobId,
    MediaRenderJobType JobType,
    string DeduplicationKey,
    string Category,
    string Payload,
    string Source,
    AssetLifecyclePolicy Policy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? AssetId,
    string? Error,
    MediaRenderJobState State);
