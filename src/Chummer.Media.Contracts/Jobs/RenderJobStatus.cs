namespace Chummer.Media.Contracts.Jobs;

/// <summary>
/// Lifecycle states for render job execution.
/// </summary>
public enum RenderJobStatus
{
    /// <summary>
    /// Accepted by the queue and waiting for claim.
    /// </summary>
    Queued = 0,

    /// <summary>
    /// Claimed by a worker but not yet rendering.
    /// </summary>
    Claimed = 1,

    /// <summary>
    /// Actively executing a render attempt.
    /// </summary>
    Rendering = 2,

    /// <summary>
    /// Completed successfully and emitted output artifacts.
    /// </summary>
    Succeeded = 3,

    /// <summary>
    /// Completed with failure after an attempt.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Stopped by caller/system cancellation.
    /// </summary>
    Cancelled = 5,
}
