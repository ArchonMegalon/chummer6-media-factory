#pragma warning disable CS1591

namespace Chummer.Media.Contracts;

public enum AssetStorageClass
{
    ObjectStorage,
    LongTermObjectStorage
}

public enum AssetApprovalState
{
    Pending,
    Approved,
    Rejected
}

public enum AssetRetentionState
{
    CacheOnly,
    ApprovalPending,
    Persisted,
    Pinned,
    Rejected,
    Expired
}

public sealed record AssetLifecyclePolicy(
    TimeSpan CacheTtl,
    bool LongTermCache,
    int MaxBytes,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    AssetStorageClass StorageClass = AssetStorageClass.ObjectStorage,
    bool AllowPersistentPinning = true);

public sealed record AssetCatalogItem(
    string AssetId,
    string Url,
    string Category,
    string Version,
    string? Source,
    AssetLifecyclePolicy? Policy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    string? StorageKey = null,
    AssetStorageClass StorageClass = AssetStorageClass.ObjectStorage,
    AssetApprovalState ApprovalState = AssetApprovalState.Pending,
    AssetRetentionState RetentionState = AssetRetentionState.CacheOnly,
    bool IsPinned = false,
    int CacheHitCount = 0,
    DateTimeOffset? LastAccessedAtUtc = null,
    DateTimeOffset? ApprovedAtUtc = null);

public sealed record AssetRenderResult(
    string AssetId,
    string Url,
    AssetLifecyclePolicy? Policy = null,
    AssetApprovalState ApprovalState = AssetApprovalState.Pending,
    AssetRetentionState RetentionState = AssetRetentionState.CacheOnly,
    string? StorageKey = null,
    AssetStorageClass StorageClass = AssetStorageClass.ObjectStorage,
    bool CacheReused = false);

public sealed record AssetLifecycleMutationRequest(
    AssetApprovalState? ApprovalState = null,
    bool? Pin = null,
    bool? Persist = null,
    string? Reason = null);

public sealed record AssetLifecycleSweepResult(
    int ExpiredAssetCount,
    int ActiveAssetCount,
    DateTimeOffset SweptAtUtc);

public enum MediaRenderJobType
{
    PortraitImageVariant,
    NarrativeBriefVideo,
    CinematicPreviewImage,
    CinematicVideo,
    PersonaMessageVideo,
    DocumentPreviewImage,
    DocumentPdf,
    DocumentThumbnailImage,
    StructuredRecipeVideo,
    StructuredRecipeAudio,
    StructuredRecipePreviewCard,
    StructuredRecipePacketBundle
}

public enum MediaRenderJobState
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Expired
}

public sealed record MediaRenderJobEnqueueRequest(
    MediaRenderJobType JobType,
    string DeduplicationKey,
    string Category,
    string Payload,
    string Source,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    bool AllowPersistentPinning = true);

public sealed record MediaRenderJobStatus(
    string JobId,
    MediaRenderJobType JobType,
    MediaRenderJobState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? AssetId,
    TimeSpan? CacheTtl,
    string? Error);

public enum PacketArtifactRole
{
    Preview,
    Pdf,
    Thumbnail
}

public sealed record PacketArtifactHandle(
    PacketArtifactRole Role,
    string Category,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    TimeSpan? CacheTtl = null);

public enum RouteCinemaArtifactRole
{
    Preview,
    Video
}

public sealed record RouteCinemaArtifactHandle(
    RouteCinemaArtifactRole Role,
    string Category,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    TimeSpan? CacheTtl = null);

[global::System.Obsolete("EXTRACT-008A quarantine: packet authoring compatibility shim remains temporarily for upstream migration and must not expand inside Chummer.Media.Contracts.")]
public sealed record PacketFactoryRequest(
    string Title,
    string Subject,
    IReadOnlyList<string>? References = null,
    IReadOnlyList<PacketAttachmentRequest>? Attachments = null);

[global::System.Obsolete("EXTRACT-008A quarantine: packet attachment targeting belongs upstream and remains temporarily for compatibility only.")]
public enum PacketAttachmentTargetKind
{
    Route,
    Message,
    Export
}

[global::System.Obsolete("EXTRACT-008A quarantine: packet attachment targeting belongs upstream and remains temporarily for compatibility only.")]
public sealed record PacketAttachmentRequest(
    PacketAttachmentTargetKind TargetKind,
    string TargetId,
    string? TargetLabel = null);

[global::System.Obsolete("EXTRACT-008A quarantine: packet attachment batching belongs upstream and remains temporarily for compatibility only.")]
public sealed record PacketAttachmentBatchRequest(
    IReadOnlyList<PacketAttachmentRequest> Attachments);

[global::System.Obsolete("EXTRACT-008A quarantine: packet attachment records belong upstream and remain temporarily for compatibility only.")]
public sealed record PacketAttachmentRecord(
    string AttachmentId,
    string PacketId,
    PacketAttachmentTargetKind TargetKind,
    string TargetId,
    string? TargetLabel,
    DateTimeOffset AttachedAtUtc,
    IReadOnlyList<PacketArtifactHandle> Artifacts);

[global::System.Obsolete("EXTRACT-008A quarantine: packet authoring result semantics remain temporarily for upstream migration and must not expand inside Chummer.Media.Contracts.")]
public sealed record PacketFactoryResult(
    string PacketId,
    string Title,
    string Subject,
    string Html,
    string? PreviewAssetId,
    string? PdfAssetId = null,
    string? ThumbnailAssetId = null,
    IReadOnlyList<PacketArtifactHandle>? Artifacts = null,
    IReadOnlyList<PacketAttachmentRecord>? Attachments = null,
    IReadOnlyList<string>? Evidence = null);

[global::System.Obsolete("EXTRACT-008A quarantine: route authoring compatibility shim remains temporarily for upstream migration and must not expand inside Chummer.Media.Contracts.")]
public sealed record RouteCinemaRequest(
    string SourceNode,
    string TargetNode);

[global::System.Obsolete("EXTRACT-008A quarantine: route narration and review compatibility shim remains temporarily for upstream migration and must not expand inside Chummer.Media.Contracts.")]
public sealed record RouteCinemaResult(
    string RouteCinemaId,
    string SourceNode,
    string TargetNode,
    IReadOnlyList<string> Waypoints,
    IReadOnlyList<string> WaypointScript,
    string TravelSummary,
    string ProjectionFingerprint,
    AssetApprovalState ApprovalState,
    AssetRetentionState RetentionState,
    string ReviewState,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    string? PreviewAssetId,
    string? RouteVideoAssetId,
    string PreviewJobId,
    MediaRenderJobState PreviewJobState,
    string RouteVideoJobId,
    MediaRenderJobState RouteVideoJobState,
    IReadOnlyList<RouteCinemaArtifactHandle> Artifacts,
    TimeSpan? CacheTtl);

public enum StructuredMediaRecipeFamily
{
    Release,
    Support,
    Publication,
    ProofShelf
}

public enum StructuredMediaRecipeArtifactRole
{
    Video,
    Audio,
    PreviewCard,
    PacketBundle
}

public sealed record StructuredMediaRecipeArtifactRequest(
    StructuredMediaRecipeArtifactRole Role,
    string Category,
    string Payload,
    string OutputFormat,
    string PublicationRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string DeduplicationKey,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    bool AllowPersistentPinning = true);

public sealed record StructuredMediaRecipeRequest(
    string RecipeExecutionId,
    StructuredMediaRecipeFamily RecipeFamily,
    string ApprovedSourcePackId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<StructuredMediaRecipeArtifactRequest> Artifacts);

public sealed record StructuredMediaRecipeArtifactReceipt(
    string ReceiptId,
    StructuredMediaRecipeArtifactRole Role,
    string Category,
    string OutputFormat,
    string PublicationRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    TimeSpan? CacheTtl = null);

public sealed record StructuredMediaRecipePublicationRefReceipt(
    string Ref,
    StructuredMediaRecipeArtifactRole Role,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    string? AssetId = null,
    TimeSpan? CacheTtl = null);

public sealed record StructuredMediaRecipePublicationReadyRef(
    string Ref,
    StructuredMediaRecipeArtifactRole Role,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    TimeSpan? CacheTtl = null);

public sealed record StructuredMediaRecipeRefArtifactReceipt(
    string ReceiptId,
    StructuredMediaRecipeArtifactRole Role,
    string PublicationRef,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    string? AssetId = null,
    TimeSpan? CacheTtl = null);

public sealed record StructuredMediaRecipeCaptionRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<StructuredMediaRecipeArtifactRole> Roles,
    IReadOnlyList<StructuredMediaRecipeRefArtifactReceipt> ArtifactReceipts);

public sealed record StructuredMediaRecipePreviewRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<StructuredMediaRecipeArtifactRole> Roles,
    IReadOnlyList<StructuredMediaRecipeRefArtifactReceipt> ArtifactReceipts);

public sealed record StructuredMediaRecipeRoleReceiptGroup(
    StructuredMediaRecipeArtifactRole Role,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> PublicationRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<StructuredMediaRecipeRefArtifactReceipt> ArtifactReceipts);

public sealed record StructuredMediaRecipeBundleReceipt(
    string RecipeExecutionId,
    StructuredMediaRecipeFamily RecipeFamily,
    string ApprovedSourcePackId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    IReadOnlyList<StructuredMediaRecipeArtifactReceipt> Artifacts,
    IReadOnlyList<string> VideoReceiptIds,
    IReadOnlyList<string> AudioReceiptIds,
    IReadOnlyList<string> PreviewReceiptIds,
    IReadOnlyList<string> PacketReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> PublicationRefs,
    IReadOnlyList<StructuredMediaRecipePublicationReadyRef> PublicationReadyRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<StructuredMediaRecipeRoleReceiptGroup> RoleReceiptGroups,
    IReadOnlyList<StructuredMediaRecipePublicationRefReceipt> PublicationRefReceipts,
    IReadOnlyList<StructuredMediaRecipeCaptionRefReceipt> CaptionRefReceipts,
    IReadOnlyList<StructuredMediaRecipePreviewRefReceipt> PreviewRefReceipts);

#pragma warning restore CS1591
