#pragma warning disable CS1591
using Chummer.Media.Contracts.Jobs;
using Chummer.Media.Contracts.Rendering;

namespace Chummer.Media.Contracts.Kernel;

public sealed record SubmitRenderJobRequest(
    string IdempotencyKey,
    string QueueName,
    string DedupeKey,
    RenderJobDedupeScope DedupeScope,
    MediaRenderRequest Request,
    DateTimeOffset AvailableAtUtc);

public sealed record SubmitRenderJobResult(
    bool Accepted,
    RenderJobContract Job,
    bool ReplayedFromIdempotencyKey);

public sealed record ClaimRenderJobRequest(
    string QueueName,
    string WorkerId,
    DateTimeOffset ClaimedAtUtc);

public sealed record ClaimRenderJobResult(
    bool Claimed,
    RenderJobContract? Job);

public sealed record CompleteRenderJobRequest(
    string RenderJobId,
    RenderJobStatus FinalStatus,
    DateTimeOffset CompletedAtUtc,
    string? FailureCode);

public sealed record CompleteRenderJobResult(
    bool Updated,
    RenderJobContract? Job,
    string? RejectionReason);

public sealed record RetryRenderJobRequest(
    string RenderJobId,
    DateTimeOffset RetryAfterUtc,
    string Reason);

public sealed record RetryRenderJobResult(
    bool Retried,
    RenderJobContract? Job,
    string? RejectionReason);

public sealed record SupersedeRenderJobRequest(
    string RenderJobId,
    string SupersededByRenderJobId,
    string Reason);

public sealed record SupersedeRenderJobResult(
    bool Superseded,
    RenderJobContract? Job,
    string? RejectionReason);
#pragma warning restore CS1591
