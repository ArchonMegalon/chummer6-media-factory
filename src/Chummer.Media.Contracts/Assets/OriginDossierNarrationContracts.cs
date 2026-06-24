#pragma warning disable CS1591

namespace Chummer.Media.Contracts;

public enum OriginDossierNarrationArtifactRole
{
    CanonicalAudio,
    AlternateAudio
}

public sealed record OriginDossierNarrationArtifactRenderRequest(
    OriginDossierNarrationArtifactRole Role,
    string Provider,
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

public sealed record OriginDossierNarrationRenderRequest(
    string RenderingId,
    string ApprovedOriginPacketId,
    string OriginRevisionId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<OriginDossierNarrationArtifactRenderRequest> Artifacts);

public sealed record OriginDossierNarrationArtifactReceipt(
    string ReceiptId,
    OriginDossierNarrationArtifactRole Role,
    string Provider,
    string Category,
    string OutputFormat,
    string CompanionRef,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    string JobId,
    MediaRenderJobState JobState,
    string? AssetId,
    string? AssetUrl,
    TimeSpan? CacheTtl,
    AssetApprovalState? ApprovalState,
    AssetRetentionState? RetentionState,
    AssetStorageClass? StorageClass);

public sealed record OriginDossierNarrationGroupedArtifactReceipt(
    string ReceiptId,
    OriginDossierNarrationArtifactRole Role,
    string Provider,
    string Category,
    string CompanionRef,
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

public sealed record OriginDossierNarrationReadyRef(
    string Ref,
    OriginDossierNarrationArtifactRole Role,
    string Provider,
    string Category,
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

public sealed record OriginDossierNarrationCompanionRefReceipt(
    string Ref,
    OriginDossierNarrationArtifactRole Role,
    string Provider,
    string Category,
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

public sealed record OriginDossierNarrationRoleReceiptGroup(
    OriginDossierNarrationArtifactRole Role,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<OriginDossierNarrationGroupedArtifactReceipt> ArtifactReceipts);

public sealed record OriginDossierNarrationCaptionRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<OriginDossierNarrationArtifactRole> Roles,
    IReadOnlyList<string> Providers,
    IReadOnlyList<OriginDossierNarrationGroupedArtifactReceipt> ArtifactReceipts);

public sealed record OriginDossierNarrationPreviewRefReceipt(
    string Ref,
    IReadOnlyList<string> ReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<OriginDossierNarrationArtifactRole> Roles,
    IReadOnlyList<string> Providers,
    IReadOnlyList<OriginDossierNarrationGroupedArtifactReceipt> ArtifactReceipts);

public sealed record OriginDossierNarrationRenderReceipt(
    string RenderingId,
    string ApprovedOriginPacketId,
    string OriginRevisionId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset RenderedAtUtc,
    IReadOnlyList<OriginDossierNarrationArtifactReceipt> Artifacts,
    IReadOnlyList<string> PrimaryAudioReceiptIds,
    IReadOnlyList<string> AlternateAudioReceiptIds,
    IReadOnlyList<string> JobIds,
    IReadOnlyList<string> CompanionRefs,
    IReadOnlyList<OriginDossierNarrationReadyRef> CompanionReadyRefs,
    IReadOnlyList<string> CaptionRefs,
    IReadOnlyList<string> PreviewRefs,
    IReadOnlyList<OriginDossierNarrationRoleReceiptGroup> RoleReceiptGroups,
    IReadOnlyList<OriginDossierNarrationCompanionRefReceipt> CompanionRefReceipts,
    IReadOnlyList<OriginDossierNarrationCaptionRefReceipt> CaptionRefReceipts,
    IReadOnlyList<OriginDossierNarrationPreviewRefReceipt> PreviewRefReceipts);

#pragma warning restore CS1591
