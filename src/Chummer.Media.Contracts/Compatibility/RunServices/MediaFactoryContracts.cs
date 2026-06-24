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
    CampaignColdOpen,
    CampaignMissionBriefing,
    CampaignCaption,
    CampaignPreview,
    RunsiteHostClip,
    RunsiteRoutePreview,
    RunsiteAudioCompanion,
    RunsiteTourSibling,
    StructuredRecipeVideo,
    StructuredRecipeAudio,
    StructuredRecipePreviewCard,
    StructuredRecipePacketBundle,
    BuildExplainCompanionVideo,
    BuildExplainCompanionAudio,
    BuildExplainCompanionPreviewCard,
    BuildExplainCompanionPacketCompanion,
    CreatorPromoVideo,
    CreatorPromoPoster,
    CreatorPromoPreviewCard,
    ExplainPresenterSiblingAudio,
    ExplainPresenterSiblingPresenterVideo,
    InstallAwareReleaseExplainerVideo,
    InstallAwareReleaseExplainerAudio,
    InstallAwareReleaseExplainerPreviewCard,
    InstallAwareSupportClosureVideo,
    InstallAwareSupportClosureAudio,
    InstallAwareSupportClosurePreviewCard,
    InstallAwarePublicConciergeVideo,
    InstallAwarePublicConciergeAudio,
    InstallAwarePublicConciergePreviewCard,
    StarterPrimerVideo,
    StarterPrimerAudio,
    StarterPrimerPreviewCard,
    FirstSessionBriefingVideo,
    FirstSessionBriefingAudio,
    FirstSessionBriefingPreviewCard,
    SupportSafeOnboardingVideo,
    SupportSafeOnboardingAudio,
    SupportSafeOnboardingPreviewCard,
    GmPrepOppositionPacket,
    GmPrepOppositionPreview,
    GmPrepOppositionBriefing,
    GmPrepScenePacket,
    GmPrepScenePreview,
    GmPrepSceneBriefing,
    GmPrepLibraryPacket,
    GmPrepLibraryPreview,
    GmPrepLibraryBriefing,
    RecapPreviewCard,
    RecapInspectableSibling,
    ReplayPreviewCard,
    ReplayInspectableSibling,
    ExchangePreviewCard,
    ExchangeInspectableSibling,
    ModeratedTestimonialVideo,
    ModeratedTestimonialAudio,
    ModeratedTestimonialPreviewCard,
    ModeratedTestimonialTranscriptCard,
    OriginDossierCanonicalAudiobookAudio,
    OriginDossierAlternateAudiobookAudio
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
    string? AssetUrl,
    TimeSpan? CacheTtl,
    string? Error,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

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

public enum RunsiteOrientationArtifactRole
{
    HostClip,
    RoutePreview,
    AudioCompanion,
    TourSibling
}

public sealed record RunsiteOrientationArtifactRenderRequest(
    RunsiteOrientationArtifactRole Role,
    string Category,
    string Payload,
    string OutputFormat,
    string RouteSegmentId,
    string DeduplicationKey,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    bool AllowPersistentPinning = true);

public sealed record RunsiteOrientationBundleRequest(
    string BundleId,
    string ApprovedRunsitePackId,
    string RouteSummaryId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<RunsiteOrientationArtifactRenderRequest> Artifacts);

public sealed record RunsiteOrientationArtifactReceipt(
    string ReceiptId,
    RunsiteOrientationArtifactRole Role,
    string Category,
    string RouteSegmentId,
    string OutputFormat,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    TimeSpan? CacheTtl = null);

public sealed record RunsiteRoutePreviewArtifactReceipt(
    string RouteSegmentId,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    TimeSpan? CacheTtl = null);

public sealed record RunsiteOrientationBundleReceipt(
    string BundleId,
    string ApprovedRunsitePackId,
    string RouteSummaryId,
    string Source,
    string PreviewTruthPosture,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    IReadOnlyList<RunsiteOrientationArtifactReceipt> Artifacts,
    IReadOnlyList<string> HostClipReceiptIds,
    IReadOnlyList<string> RoutePreviewReceiptIds,
    IReadOnlyList<RunsiteRoutePreviewArtifactReceipt> RoutePreviewArtifactReceipts,
    IReadOnlyList<string> AudioCompanionReceiptIds,
    IReadOnlyList<string> TourSiblingReceiptIds);

public enum CampaignBriefingBundleSlot
{
    ColdOpen,
    MissionBriefing
}

public enum CampaignBriefingBundleArtifactKind
{
    Media,
    Caption,
    Preview
}

public sealed record CampaignBriefingBundleArtifactRequest(
    string Category,
    string Payload,
    string OutputFormat,
    string DeduplicationKey,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    bool AllowPersistentPinning = true);

public sealed record CampaignBriefingBundleEntryRequest(
    CampaignBriefingBundleSlot Slot,
    string Locale,
    bool IsFallbackSibling,
    CampaignBriefingBundleArtifactRequest Media,
    CampaignBriefingBundleArtifactRequest Caption,
    CampaignBriefingBundleArtifactRequest Preview);

public sealed record CampaignBriefingBundleRequest(
    string BundleId,
    string CampaignPrimerId,
    string MissionBriefingId,
    string RequestedLocale,
    string Source,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<CampaignBriefingBundleEntryRequest> Entries);

public sealed record CampaignBriefingArtifactReceipt(
    string ReceiptId,
    CampaignBriefingBundleSlot Slot,
    CampaignBriefingBundleArtifactKind Kind,
    string Locale,
    bool IsFallbackSibling,
    string Category,
    string OutputFormat,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record CampaignBriefingLocaleReceipt(
    string EntryReceiptId,
    CampaignBriefingBundleSlot Slot,
    string Locale,
    bool IsFallbackSibling,
    string MediaReceiptId,
    string CaptionReceiptId,
    string PreviewReceiptId,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<CampaignBriefingArtifactReceipt> ArtifactReceipts);

public sealed record CampaignBriefingLocaleBundleReceipt(
    string ReceiptId,
    string Locale,
    bool IsFallbackSibling,
    string ColdOpenEntryReceiptId,
    string MissionBriefingEntryReceiptId,
    string ColdOpenMediaReceiptId,
    string MissionBriefingMediaReceiptId,
    string ColdOpenCaptionReceiptId,
    string MissionBriefingCaptionReceiptId,
    string ColdOpenPreviewReceiptId,
    string MissionBriefingPreviewReceiptId,
    IReadOnlyList<string> CaptionReceiptIds,
    IReadOnlyList<string> PreviewReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<CampaignBriefingArtifactReceipt> ArtifactReceipts);

public sealed record CampaignBriefingFallbackSiblingReceipt(
    string ReceiptId,
    string Locale,
    string ColdOpenEntryReceiptId,
    string MissionBriefingEntryReceiptId,
    string ColdOpenMediaReceiptId,
    string MissionBriefingMediaReceiptId,
    string ColdOpenCaptionReceiptId,
    string MissionBriefingCaptionReceiptId,
    string ColdOpenPreviewReceiptId,
    string MissionBriefingPreviewReceiptId,
    IReadOnlyList<string> CaptionReceiptIds,
    IReadOnlyList<string> PreviewReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<CampaignBriefingArtifactReceipt> ArtifactReceipts);

public sealed record CampaignBriefingBundleReceipt(
    string BundleId,
    string CampaignPrimerId,
    string MissionBriefingId,
    string RequestedLocale,
    string Source,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    IReadOnlyList<CampaignBriefingLocaleReceipt> LocaleReceipts,
    IReadOnlyList<CampaignBriefingLocaleBundleReceipt> LocaleBundleReceipts,
    IReadOnlyList<CampaignBriefingArtifactReceipt> ArtifactReceipts,
    string RequestedLocaleBundleReceiptId,
    IReadOnlyList<string> FallbackLocales,
    IReadOnlyList<string> FallbackLocaleBundleReceiptIds,
    IReadOnlyList<string> ColdOpenReceiptIds,
    IReadOnlyList<string> MissionBriefingReceiptIds,
    IReadOnlyList<string> CaptionReceiptIds,
    IReadOnlyList<string> PreviewReceiptIds,
    IReadOnlyList<CampaignBriefingFallbackSiblingReceipt> FallbackSiblingReceipts,
    IReadOnlyList<string> JobIds);

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
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record StructuredMediaRecipePublicationRefReceipt(
    string Ref,
    StructuredMediaRecipeArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record StructuredMediaRecipePublicationReadyRef(
    string Ref,
    StructuredMediaRecipeArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record StructuredMediaRecipeRefArtifactReceipt(
    string ReceiptId,
    StructuredMediaRecipeArtifactRole Role,
    string Category,
    string PublicationRef,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record StructuredMediaRecipeCaptionRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> PublicationRefs,
    IReadOnlyList<StructuredMediaRecipeArtifactRole> Roles,
    IReadOnlyList<StructuredMediaRecipeRefArtifactReceipt> ArtifactReceipts);

public sealed record StructuredMediaRecipePreviewRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> PublicationRefs,
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

public enum BuildExplainCompanionArtifactRole
{
    Video,
    Audio,
    PreviewCard,
    PacketCompanion
}

public sealed record BuildExplainCompanionArtifactRenderRequest(
    BuildExplainCompanionArtifactRole Role,
    string Category,
    string Payload,
    string OutputFormat,
    string CompanionRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string DeduplicationKey,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    bool AllowPersistentPinning = true);

public sealed record BuildExplainCompanionRenderRequest(
    string RenderingId,
    string ApprovedExplainPacketId,
    string ExplainPacketRevisionId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<BuildExplainCompanionArtifactRenderRequest> Artifacts);

public sealed record BuildExplainCompanionArtifactReceipt(
    string ReceiptId,
    BuildExplainCompanionArtifactRole Role,
    string Category,
    string OutputFormat,
    string CompanionRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record BuildExplainCompanionRefReceipt(
    string Ref,
    BuildExplainCompanionArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record BuildExplainCompanionReadyRef(
    string Ref,
    BuildExplainCompanionArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record BuildExplainCompanionGroupedArtifactReceipt(
    string ReceiptId,
    BuildExplainCompanionArtifactRole Role,
    string Category,
    string CompanionRef,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record BuildExplainCaptionRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<BuildExplainCompanionArtifactRole> Roles,
    IReadOnlyList<BuildExplainCompanionGroupedArtifactReceipt> ArtifactReceipts);

public sealed record BuildExplainPreviewRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<BuildExplainCompanionArtifactRole> Roles,
    IReadOnlyList<BuildExplainCompanionGroupedArtifactReceipt> ArtifactReceipts);

public sealed record BuildExplainCompanionRoleReceiptGroup(
    BuildExplainCompanionArtifactRole Role,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<BuildExplainCompanionGroupedArtifactReceipt> ArtifactReceipts);

public sealed record BuildExplainCompanionRenderReceipt(
    string RenderingId,
    string ApprovedExplainPacketId,
    string ExplainPacketRevisionId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    IReadOnlyList<BuildExplainCompanionArtifactReceipt> Artifacts,
    IReadOnlyList<string> VideoReceiptIds,
    IReadOnlyList<string> AudioReceiptIds,
    IReadOnlyList<string> PreviewCardReceiptIds,
    IReadOnlyList<string> PacketCompanionReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<BuildExplainCompanionReadyRef> CompanionReadyRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<BuildExplainCompanionRoleReceiptGroup> RoleReceiptGroups,
    IReadOnlyList<BuildExplainCompanionRefReceipt> CompanionRefReceipts,
    IReadOnlyList<BuildExplainCaptionRefReceipt> CaptionRefReceipts,
    IReadOnlyList<BuildExplainPreviewRefReceipt> PreviewRefReceipts);

public enum ExplainPresenterSiblingArtifactRole
{
    Audio,
    PresenterVideo
}

public sealed record ExplainPresenterSiblingArtifactRenderRequest(
    ExplainPresenterSiblingArtifactRole Role,
    string Category,
    string Payload,
    string OutputFormat,
    string CompanionRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string DeduplicationKey,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    bool AllowPersistentPinning = true);

public sealed record ExplainPresenterTextFallbackReceipt(
    string ReceiptId,
    string Text,
    string GroundingScopeRef);

public sealed record ExplainPresenterSiblingRenderRequest(
    string RenderingId,
    string ApprovedExplanationPacketId,
    string ExplanationPacketRevisionId,
    string GroundingScopeRef,
    string Source,
    DateTimeOffset RequestedAtUtc,
    string FirstPartyTextFallback,
    IReadOnlyList<ExplainPresenterSiblingArtifactRenderRequest> Artifacts);

public sealed record ExplainPresenterSiblingArtifactReceipt(
    string ReceiptId,
    ExplainPresenterSiblingArtifactRole Role,
    string Category,
    string OutputFormat,
    string CompanionRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record ExplainPresenterCompanionRefReceipt(
    string Ref,
    ExplainPresenterSiblingArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record ExplainPresenterSiblingReadyRef(
    string Ref,
    ExplainPresenterSiblingArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record ExplainPresenterSiblingGroupedArtifactReceipt(
    string ReceiptId,
    ExplainPresenterSiblingArtifactRole Role,
    string Category,
    string CompanionRef,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record ExplainPresenterCaptionRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<ExplainPresenterSiblingArtifactRole> Roles,
    IReadOnlyList<ExplainPresenterSiblingGroupedArtifactReceipt> ArtifactReceipts);

public sealed record ExplainPresenterPreviewRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<ExplainPresenterSiblingArtifactRole> Roles,
    IReadOnlyList<ExplainPresenterSiblingGroupedArtifactReceipt> ArtifactReceipts);

public sealed record ExplainPresenterSiblingRoleReceiptGroup(
    ExplainPresenterSiblingArtifactRole Role,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<ExplainPresenterSiblingGroupedArtifactReceipt> ArtifactReceipts);

public sealed record ExplainPresenterSiblingRenderReceipt(
    string RenderingId,
    string ApprovedExplanationPacketId,
    string ExplanationPacketRevisionId,
    string GroundingScopeRef,
    string Source,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    string FirstPartyTextFallback,
    ExplainPresenterTextFallbackReceipt TextFallbackReceipt,
    IReadOnlyList<ExplainPresenterSiblingArtifactReceipt> Artifacts,
    IReadOnlyList<string> AudioReceiptIds,
    IReadOnlyList<string> PresenterReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<ExplainPresenterSiblingReadyRef> CompanionReadyRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<ExplainPresenterSiblingRoleReceiptGroup> RoleReceiptGroups,
    IReadOnlyList<ExplainPresenterCompanionRefReceipt> CompanionRefReceipts,
    IReadOnlyList<ExplainPresenterCaptionRefReceipt> CaptionRefReceipts,
    IReadOnlyList<ExplainPresenterPreviewRefReceipt> PreviewRefReceipts);

public enum InstallAwareConciergeBundleKind
{
    ReleaseExplainer,
    SupportClosure,
    PublicConcierge
}

public enum InstallAwareConciergeArtifactRole
{
    Video,
    Audio,
    PreviewCard
}

public sealed record InstallAwareConciergeArtifactRenderRequest(
    InstallAwareConciergeBundleKind BundleKind,
    InstallAwareConciergeArtifactRole Role,
    string Category,
    string Payload,
    string OutputFormat,
    string CompanionRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SiblingNoteRefs,
    string DeduplicationKey,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    bool AllowPersistentPinning = true);

public sealed record InstallAwareConciergeRenderRequest(
    string RenderingId,
    string InstallAwarePacketId,
    string InstalledBuildReceiptId,
    string ArtifactIdentityId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<InstallAwareConciergeArtifactRenderRequest> Artifacts);

public sealed record InstallAwareConciergeArtifactReceipt(
    string ReceiptId,
    InstallAwareConciergeBundleKind BundleKind,
    InstallAwareConciergeArtifactRole Role,
    string Category,
    string OutputFormat,
    string CompanionRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SiblingNoteRefs,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record InstallAwareConciergeCompanionReadyRef(
    string Ref,
    InstallAwareConciergeBundleKind BundleKind,
    InstallAwareConciergeArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SiblingNoteRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record InstallAwareConciergeCompanionRefReceipt(
    string Ref,
    InstallAwareConciergeBundleKind BundleKind,
    InstallAwareConciergeArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SiblingNoteRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record InstallAwareConciergeGroupedArtifactReceipt(
    string ReceiptId,
    InstallAwareConciergeBundleKind BundleKind,
    InstallAwareConciergeArtifactRole Role,
    string Category,
    string CompanionRef,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SiblingNoteRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record InstallAwareConciergeCaptionRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<InstallAwareConciergeBundleKind> BundleKinds,
    IReadOnlyList<InstallAwareConciergeArtifactRole> Roles,
    IReadOnlyList<InstallAwareConciergeGroupedArtifactReceipt> ArtifactReceipts);

public sealed record InstallAwareConciergePreviewRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<InstallAwareConciergeBundleKind> BundleKinds,
    IReadOnlyList<InstallAwareConciergeArtifactRole> Roles,
    IReadOnlyList<InstallAwareConciergeGroupedArtifactReceipt> ArtifactReceipts);

public sealed record InstallAwareConciergeSiblingNoteReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<InstallAwareConciergeBundleKind> BundleKinds,
    IReadOnlyList<InstallAwareConciergeArtifactRole> Roles,
    IReadOnlyList<InstallAwareConciergeGroupedArtifactReceipt> ArtifactReceipts);

public sealed record InstallAwareConciergeRoleReceiptGroup(
    InstallAwareConciergeBundleKind BundleKind,
    InstallAwareConciergeArtifactRole Role,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SiblingNoteRefs,
    IReadOnlyList<InstallAwareConciergeGroupedArtifactReceipt> ArtifactReceipts);

public sealed record InstallAwareConciergeBundleReceiptGroup(
    InstallAwareConciergeBundleKind BundleKind,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SiblingNoteRefs,
    IReadOnlyList<InstallAwareConciergeArtifactRole> Roles,
    IReadOnlyList<InstallAwareConciergeGroupedArtifactReceipt> ArtifactReceipts);

public sealed record InstallAwareConciergeBundleReceipt(
    string RenderingId,
    string InstallAwarePacketId,
    string InstalledBuildReceiptId,
    string ArtifactIdentityId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    IReadOnlyList<InstallAwareConciergeArtifactReceipt> Artifacts,
    IReadOnlyList<string> ReleaseExplainerReceiptIds,
    IReadOnlyList<string> SupportClosureReceiptIds,
    IReadOnlyList<string> PublicConciergeReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<InstallAwareConciergeCompanionReadyRef> CompanionReadyRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SiblingNoteRefs,
    IReadOnlyList<InstallAwareConciergeBundleReceiptGroup> BundleReceiptGroups,
    IReadOnlyList<InstallAwareConciergeRoleReceiptGroup> RoleReceiptGroups,
    IReadOnlyList<InstallAwareConciergeCompanionRefReceipt> CompanionRefReceipts,
    IReadOnlyList<InstallAwareConciergeCaptionRefReceipt> CaptionRefReceipts,
    IReadOnlyList<InstallAwareConciergePreviewRefReceipt> PreviewRefReceipts,
    IReadOnlyList<InstallAwareConciergeSiblingNoteReceipt> SiblingNoteReceipts);

public enum StarterArtifactBundleKind
{
    StarterPrimer,
    FirstSessionBriefing,
    SupportSafeOnboarding
}

public enum StarterArtifactRole
{
    Video,
    Audio,
    PreviewCard
}

public sealed record StarterArtifactRenderRequest(
    StarterArtifactBundleKind BundleKind,
    StarterArtifactRole Role,
    string Locale,
    string Category,
    string Payload,
    string OutputFormat,
    string ArtifactRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SupportNoteRefs,
    string DeduplicationKey,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    bool AllowPersistentPinning = true);

public sealed record StarterArtifactBundleRenderRequest(
    string RenderingId,
    string ApprovedStarterSourcePackId,
    string SourcePackRevisionId,
    string StarterLaneId,
    string RequestedLocale,
    string Source,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<StarterArtifactRenderRequest> Artifacts);

public sealed record StarterArtifactReceipt(
    string ReceiptId,
    StarterArtifactBundleKind BundleKind,
    StarterArtifactRole Role,
    string Locale,
    string Category,
    string OutputFormat,
    string ArtifactRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SupportNoteRefs,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record StarterArtifactReadyRef(
    string Ref,
    StarterArtifactBundleKind BundleKind,
    StarterArtifactRole Role,
    string Locale,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SupportNoteRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record StarterArtifactGroupedReceipt(
    string ReceiptId,
    StarterArtifactBundleKind BundleKind,
    StarterArtifactRole Role,
    string Locale,
    string Category,
    string ArtifactRef,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SupportNoteRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record StarterArtifactArtifactRefReceipt(
    string Ref,
    StarterArtifactBundleKind BundleKind,
    StarterArtifactRole Role,
    string Locale,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SupportNoteRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record StarterArtifactCaptionRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<StarterArtifactBundleKind> BundleKinds,
    IReadOnlyList<StarterArtifactRole> Roles,
    IReadOnlyList<string> Locales,
    IReadOnlyList<StarterArtifactGroupedReceipt> ArtifactReceipts);

public sealed record StarterArtifactPreviewRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<StarterArtifactBundleKind> BundleKinds,
    IReadOnlyList<StarterArtifactRole> Roles,
    IReadOnlyList<string> Locales,
    IReadOnlyList<StarterArtifactGroupedReceipt> ArtifactReceipts);

public sealed record StarterArtifactSupportNoteReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<StarterArtifactBundleKind> BundleKinds,
    IReadOnlyList<StarterArtifactRole> Roles,
    IReadOnlyList<string> Locales,
    IReadOnlyList<StarterArtifactGroupedReceipt> ArtifactReceipts);

public sealed record StarterArtifactLocaleReceiptGroup(
    string Locale,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<StarterArtifactBundleKind> BundleKinds,
    IReadOnlyList<StarterArtifactRole> Roles,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SupportNoteRefs,
    IReadOnlyList<StarterArtifactGroupedReceipt> ArtifactReceipts);

public sealed record StarterArtifactBundleLocaleReceiptGroup(
    StarterArtifactBundleKind BundleKind,
    string Locale,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<StarterArtifactRole> Roles,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SupportNoteRefs,
    IReadOnlyList<StarterArtifactGroupedReceipt> ArtifactReceipts);

public sealed record StarterArtifactBundleReceipt(
    string RenderingId,
    string ApprovedStarterSourcePackId,
    string SourcePackRevisionId,
    string StarterLaneId,
    string RequestedLocale,
    IReadOnlyList<string> FallbackLocales,
    string Source,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    IReadOnlyList<StarterArtifactReceipt> Artifacts,
    IReadOnlyList<string> StarterPrimerReceiptIds,
    IReadOnlyList<string> FirstSessionBriefingReceiptIds,
    IReadOnlyList<string> SupportSafeOnboardingReceiptIds,
    IReadOnlyList<string> RequestedLocaleReceiptIds,
    IReadOnlyList<string> FallbackLocaleReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<StarterArtifactReadyRef> ReadyRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<string> SupportNoteRefs,
    IReadOnlyList<StarterArtifactLocaleReceiptGroup> LocaleReceiptGroups,
    IReadOnlyList<StarterArtifactBundleLocaleReceiptGroup> BundleLocaleReceiptGroups,
    IReadOnlyList<StarterArtifactArtifactRefReceipt> ArtifactRefReceipts,
    IReadOnlyList<StarterArtifactCaptionRefReceipt> CaptionRefReceipts,
    IReadOnlyList<StarterArtifactPreviewRefReceipt> PreviewRefReceipts,
    IReadOnlyList<StarterArtifactSupportNoteReceipt> SupportNoteReceipts);

public enum GmPrepPacketSubjectKind
{
    Opposition,
    Scene,
    PrepLibraryEntry
}

public enum GmPrepPacketArtifactRole
{
    Packet,
    Preview,
    Briefing
}

public sealed record GmPrepPacketArtifactRenderRequest(
    string Category,
    string Payload,
    string OutputFormat,
    string DeduplicationKey,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    bool AllowPersistentPinning = true);

public sealed record GmPrepPacketEntryRenderRequest(
    GmPrepPacketSubjectKind SubjectKind,
    string SourceEntryId,
    string PacketRef,
    GmPrepPacketArtifactRenderRequest Packet,
    GmPrepPacketArtifactRenderRequest Preview,
    GmPrepPacketArtifactRenderRequest? Briefing = null);

public sealed record GmPrepPacketRenderRequest(
    string RenderingId,
    string GovernedSourcePackId,
    string SourcePackRevisionId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<GmPrepPacketEntryRenderRequest> Entries);

public sealed record GmPrepPacketArtifactReceipt(
    string ReceiptId,
    GmPrepPacketSubjectKind SubjectKind,
    GmPrepPacketArtifactRole Role,
    string SourceEntryId,
    string PacketRef,
    string Category,
    string OutputFormat,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record GmPrepPacketEntryReceipt(
    string EntryReceiptId,
    GmPrepPacketSubjectKind SubjectKind,
    string SourceEntryId,
    string PacketRef,
    string PacketReceiptId,
    string PreviewReceiptId,
    string? BriefingReceiptId,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<GmPrepPacketArtifactReceipt> ArtifactReceipts);

public sealed record GmPrepPacketSubjectReceiptGroup(
    string ReceiptId,
    GmPrepPacketSubjectKind SubjectKind,
    IReadOnlyList<string> EntryReceiptIds,
    IReadOnlyList<string> PacketRefs,
    IReadOnlyList<string> PacketReceiptIds,
    IReadOnlyList<string> PreviewReceiptIds,
    IReadOnlyList<string> BriefingReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<GmPrepPacketArtifactReceipt> ArtifactReceipts);

public sealed record GmPrepPacketBundleReceipt(
    string RenderingId,
    string GovernedSourcePackId,
    string SourcePackRevisionId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    IReadOnlyList<GmPrepPacketArtifactReceipt> Artifacts,
    IReadOnlyList<GmPrepPacketEntryReceipt> EntryReceipts,
    IReadOnlyList<GmPrepPacketSubjectReceiptGroup> SubjectReceiptGroups,
    IReadOnlyList<string> PacketReceiptIds,
    IReadOnlyList<string> PreviewReceiptIds,
    IReadOnlyList<string> BriefingReceiptIds,
    IReadOnlyList<string> OppositionPacketReceiptIds,
    IReadOnlyList<string> ScenePacketReceiptIds,
    IReadOnlyList<string> PrepLibraryPacketReceiptIds,
    IReadOnlyList<string> PacketRefs,
    IReadOnlyList<string> JobIds);

public enum ReplayExchangePreviewBundleKind
{
    Recap,
    Replay,
    Exchange
}

public enum ReplayExchangePreviewArtifactRole
{
    PreviewCard,
    InspectableSibling
}

public sealed record ReplayExchangePreviewArtifactRenderRequest(
    ReplayExchangePreviewArtifactRole Role,
    string Category,
    string Payload,
    string OutputFormat,
    string ArtifactRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string DeduplicationKey,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    bool AllowPersistentPinning = true);

public sealed record ReplayExchangePreviewBundleRenderRequest(
    ReplayExchangePreviewBundleKind BundleKind,
    string BundleRef,
    string LineageRef,
    string CompatibilityReceiptId,
    string ProvenanceReceiptId,
    string BoundedLossReceiptId,
    ReplayExchangePreviewArtifactRenderRequest PreviewCard,
    ReplayExchangePreviewArtifactRenderRequest InspectableSibling);

public sealed record ReplayExchangePreviewRenderRequest(
    string RenderingId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<ReplayExchangePreviewBundleRenderRequest> Bundles);

public sealed record ReplayExchangePreviewArtifactReceipt(
    string ReceiptId,
    ReplayExchangePreviewBundleKind BundleKind,
    ReplayExchangePreviewArtifactRole Role,
    string BundleRef,
    string ArtifactRef,
    string LineageRef,
    string CompatibilityReceiptId,
    string ProvenanceReceiptId,
    string BoundedLossReceiptId,
    string Category,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record ReplayExchangePreviewBundleReceipt(
    string ReceiptId,
    ReplayExchangePreviewBundleKind BundleKind,
    string BundleRef,
    string LineageRef,
    string CompatibilityReceiptId,
    string ProvenanceReceiptId,
    string BoundedLossReceiptId,
    string PreviewCardReceiptId,
    string InspectableSiblingReceiptId,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<ReplayExchangePreviewArtifactReceipt> ArtifactReceipts);

public sealed record ReplayExchangePreviewReadyRef(
    string Ref,
    ReplayExchangePreviewBundleKind BundleKind,
    ReplayExchangePreviewArtifactRole Role,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId,
    string? AssetUrl,
    TimeSpan? CacheTtl,
    AssetApprovalState? ApprovalState,
    AssetRetentionState? RetentionState,
    AssetStorageClass? StorageClass);

public sealed record ReplayExchangePreviewArtifactRefReceipt(
    string Ref,
    ReplayExchangePreviewBundleKind BundleKind,
    ReplayExchangePreviewArtifactRole Role,
    string ReceiptId,
    string JobId,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId,
    string? AssetUrl);

public sealed record ReplayExchangePreviewCaptionRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<ReplayExchangePreviewArtifactReceipt> ArtifactReceipts);

public sealed record ReplayExchangePreviewPreviewRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<ReplayExchangePreviewArtifactReceipt> ArtifactReceipts);

public sealed record ReplayExchangePreviewKindReceiptGroup(
    string ReceiptId,
    ReplayExchangePreviewBundleKind BundleKind,
    IReadOnlyList<string> BundleRefs,
    IReadOnlyList<string> LineageRefs,
    IReadOnlyList<string> CompatibilityReceiptIds,
    IReadOnlyList<string> ProvenanceReceiptIds,
    IReadOnlyList<string> BoundedLossReceiptIds,
    IReadOnlyList<string> BundleReceiptIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<ReplayExchangePreviewArtifactReceipt> ArtifactReceipts);

public sealed record ReplayExchangePreviewRenderReceipt(
    string RenderingId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    IReadOnlyList<ReplayExchangePreviewArtifactReceipt> Artifacts,
    IReadOnlyList<ReplayExchangePreviewBundleReceipt> BundleReceipts,
    IReadOnlyList<ReplayExchangePreviewKindReceiptGroup> KindReceiptGroups,
    IReadOnlyList<string> PreviewCardReceiptIds,
    IReadOnlyList<string> InspectableSiblingReceiptIds,
    IReadOnlyList<string> BundleRefs,
    IReadOnlyList<string> LineageRefs,
    IReadOnlyList<string> CompatibilityReceiptIds,
    IReadOnlyList<string> ProvenanceReceiptIds,
    IReadOnlyList<string> BoundedLossReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<ReplayExchangePreviewReadyRef> ReadyRefs,
    IReadOnlyList<ReplayExchangePreviewArtifactRefReceipt> ArtifactRefReceipts,
    IReadOnlyList<ReplayExchangePreviewCaptionRefReceipt> CaptionRefReceipts,
    IReadOnlyList<ReplayExchangePreviewPreviewRefReceipt> PreviewRefReceipts);

public enum CreatorPromoKitArtifactRole
{
    PromoVideo,
    PromoPoster,
    PreviewCard
}

public sealed record CreatorPromoKitArtifactRenderRequest(
    CreatorPromoKitArtifactRole Role,
    string Category,
    string Payload,
    string OutputFormat,
    string ArtifactRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string DeduplicationKey,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = false,
    bool PersistOnApproval = false,
    bool AllowPersistentPinning = true);

public sealed record CreatorPromoKitRenderRequest(
    string RenderingId,
    string ApprovedManifestId,
    string ManifestRevisionId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<CreatorPromoKitArtifactRenderRequest> Artifacts);

public sealed record CreatorPromoKitArtifactReceipt(
    string ReceiptId,
    CreatorPromoKitArtifactRole Role,
    string Category,
    string OutputFormat,
    string ArtifactRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record CreatorPromoKitArtifactRefReceipt(
    string Ref,
    CreatorPromoKitArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record CreatorPromoKitReadyRef(
    string Ref,
    CreatorPromoKitArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record CreatorPromoKitGroupedArtifactReceipt(
    string ReceiptId,
    CreatorPromoKitArtifactRole Role,
    string Category,
    string ArtifactRef,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record CreatorPromoCaptionRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<CreatorPromoKitArtifactRole> Roles,
    IReadOnlyList<CreatorPromoKitGroupedArtifactReceipt> ArtifactReceipts);

public sealed record CreatorPromoPreviewRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<CreatorPromoKitArtifactRole> Roles,
    IReadOnlyList<CreatorPromoKitGroupedArtifactReceipt> ArtifactReceipts);

public sealed record CreatorPromoKitRoleReceiptGroup(
    CreatorPromoKitArtifactRole Role,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<CreatorPromoKitGroupedArtifactReceipt> ArtifactReceipts);

public sealed record CreatorPromoKitRenderReceipt(
    string RenderingId,
    string ApprovedManifestId,
    string ManifestRevisionId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    IReadOnlyList<CreatorPromoKitArtifactReceipt> Artifacts,
    IReadOnlyList<string> PromoVideoReceiptIds,
    IReadOnlyList<string> PromoPosterReceiptIds,
    IReadOnlyList<string> PreviewCardReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> ArtifactRefs,
    IReadOnlyList<CreatorPromoKitReadyRef> ReadyRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<CreatorPromoKitRoleReceiptGroup> RoleReceiptGroups,
    IReadOnlyList<CreatorPromoKitArtifactRefReceipt> ArtifactRefReceipts,
    IReadOnlyList<CreatorPromoCaptionRefReceipt> CaptionRefReceipts,
    IReadOnlyList<CreatorPromoPreviewRefReceipt> PreviewRefReceipts);

public enum ModeratedTestimonialArtifactRole
{
    Video,
    Audio,
    PreviewCard,
    TranscriptCard
}

public sealed record ModeratedTestimonialArtifactRenderRequest(
    ModeratedTestimonialArtifactRole Role,
    string Category,
    string Payload,
    string OutputFormat,
    string AssetRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string DeduplicationKey,
    TimeSpan? CacheTtl = null,
    int MaxBytes = 0,
    bool RequiresApproval = true,
    bool PersistOnApproval = true,
    bool AllowPersistentPinning = true);

public sealed record ModeratedTestimonialRenderRequest(
    string RenderingId,
    string PublicationId,
    string ModerationCaseId,
    string SourceReceiptId,
    string ConsentReceiptId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<ModeratedTestimonialArtifactRenderRequest> Artifacts);

public sealed record ModeratedTestimonialArtifactReceipt(
    string ReceiptId,
    ModeratedTestimonialArtifactRole Role,
    string Category,
    string OutputFormat,
    string AssetRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string JobId,
    MediaRenderJobState JobState,
    string ModerationState,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record ModeratedTestimonialArtifactRefReceipt(
    string Ref,
    ModeratedTestimonialArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    string ModerationState,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record ModeratedTestimonialReadyRef(
    string Ref,
    ModeratedTestimonialArtifactRole Role,
    string Category,
    string ReceiptId,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    string ModerationState,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record ModeratedTestimonialGroupedArtifactReceipt(
    string ReceiptId,
    ModeratedTestimonialArtifactRole Role,
    string Category,
    string AssetRef,
    string JobId,
    MediaRenderJobState JobState,
    string OutputFormat,
    string ModerationState,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string? AssetId = null,
    string? AssetUrl = null,
    TimeSpan? CacheTtl = null,
    AssetApprovalState? ApprovalState = null,
    AssetRetentionState? RetentionState = null,
    AssetStorageClass? StorageClass = null);

public sealed record ModeratedTestimonialCaptionRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> AssetRefs,
    IReadOnlyList<ModeratedTestimonialArtifactRole> Roles,
    IReadOnlyList<ModeratedTestimonialGroupedArtifactReceipt> ArtifactReceipts);

public sealed record ModeratedTestimonialPreviewRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> AssetRefs,
    IReadOnlyList<ModeratedTestimonialArtifactRole> Roles,
    IReadOnlyList<ModeratedTestimonialGroupedArtifactReceipt> ArtifactReceipts);

public sealed record ModeratedTestimonialRoleReceiptGroup(
    ModeratedTestimonialArtifactRole Role,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> AssetRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<ModeratedTestimonialGroupedArtifactReceipt> ArtifactReceipts);

public sealed record ModeratedTestimonialRenderReceipt(
    string RenderingId,
    string PublicationId,
    string ModerationCaseId,
    string SourceReceiptId,
    string ConsentReceiptId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    IReadOnlyList<ModeratedTestimonialArtifactReceipt> Artifacts,
    IReadOnlyList<string> VideoReceiptIds,
    IReadOnlyList<string> AudioReceiptIds,
    IReadOnlyList<string> PreviewCardReceiptIds,
    IReadOnlyList<string> TranscriptCardReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> AssetRefs,
    IReadOnlyList<ModeratedTestimonialReadyRef> ReadyRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<ModeratedTestimonialRoleReceiptGroup> RoleReceiptGroups,
    IReadOnlyList<ModeratedTestimonialArtifactRefReceipt> ArtifactRefReceipts,
    IReadOnlyList<ModeratedTestimonialCaptionRefReceipt> CaptionRefReceipts,
    IReadOnlyList<ModeratedTestimonialPreviewRefReceipt> PreviewRefReceipts);

#pragma warning restore CS1591
