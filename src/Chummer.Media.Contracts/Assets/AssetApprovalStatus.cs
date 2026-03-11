namespace Chummer.Media.Contracts.Assets;

/// <summary>
/// Approval result for a rendered asset after factory-side review state is persisted.
/// </summary>
public enum AssetApprovalStatus
{
    /// <summary>
    /// Awaiting a factory-side review decision.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Accepted for persisted/canonical downstream usage.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Explicitly declined during review.
    /// </summary>
    Rejected = 2,
}
