#pragma warning disable CS1591
namespace Chummer.Media.Contracts.Kernel;

public sealed record RetentionSweepRequest(
    DateTimeOffset WatermarkUtc,
    string SweepKey,
    bool IncludePurge,
    IReadOnlyDictionary<string, string> PolicyHints);

public sealed record RetentionSweepAssetTransition(
    string AssetId,
    DateTimeOffset? ExpiredAtUtc,
    DateTimeOffset? MarkedPurgeCandidateAtUtc,
    DateTimeOffset? PurgedAtUtc,
    bool LifecycleOnlyMutation);

public sealed record RetentionSweepResult(
    string SweepKey,
    DateTimeOffset WatermarkUtc,
    bool Replayed,
    int ExaminedAssetCount,
    int ExpiredCount,
    int PurgeCandidateCount,
    int PurgedCount,
    IReadOnlyList<RetentionSweepAssetTransition> Transitions);
#pragma warning restore CS1591
