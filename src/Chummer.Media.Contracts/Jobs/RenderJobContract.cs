using Chummer.Media.Contracts.Rendering;

namespace Chummer.Media.Contracts.Jobs;

/// <summary>
/// Queue-owned job envelope for deterministic render execution and dedupe tracking.
/// </summary>
public sealed record RenderJobContract(
    string RenderJobId,
    string QueueName,
    string DedupeKey,
    RenderJobDedupeScope DedupeScope,
    MediaRenderRequest Request,
    RenderJobStatus Status,
    int AttemptCount,
    int MaxAttemptCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset AvailableAtUtc,
    DateTimeOffset? ClaimedAtUtc,
    DateTimeOffset? LastAttemptedAtUtc,
    DateTimeOffset? RetryAfterUtc,
    DateTimeOffset? CompletedAtUtc,
    string? FailureCode,
    string? SupersededByRenderJobId);
