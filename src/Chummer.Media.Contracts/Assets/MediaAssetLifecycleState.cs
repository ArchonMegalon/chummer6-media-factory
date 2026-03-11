namespace Chummer.Media.Contracts.Assets;

/// <summary>
/// Asset lifecycle state owned by media-factory, including approval and persistence checkpoints.
/// </summary>
public sealed record MediaAssetLifecycleState(
    AssetApprovalStatus ApprovalStatus,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? RejectedAtUtc,
    DateTimeOffset? PersistedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? PurgedAtUtc);
