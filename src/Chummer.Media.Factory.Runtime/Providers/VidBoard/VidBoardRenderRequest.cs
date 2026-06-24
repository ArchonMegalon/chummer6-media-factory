namespace Chummer.Run.AI.Services.Assets;

public sealed record VidBoardRenderRequest(
    string RequestId,
    string ApprovedOriginPacketId,
    string OriginRevisionId,
    string StoryboardPath,
    string PosterPath,
    string? SelectedScenePath,
    string? SelectedPortraitPath,
    string OutputFormat,
    string RequestedBy,
    string Source);

public sealed record VidBoardRenderReceipt(
    string ReceiptId,
    string Provider,
    string AccountUserHash,
    string ProviderJobId,
    string PromptHash,
    int CreditCost,
    IReadOnlyList<string> OutputFiles,
    string CommercialUseStatus,
    string SourceReceiptAssociationStatus,
    DateTimeOffset CreatedAtUtc,
    bool CandidateAssetOnly,
    bool PublishAuthority,
    string Status,
    string BlockingReason);

public sealed record VidBoardDownloadedAssetReceipt(
    string Provider,
    string ProviderJobId,
    string AssetId,
    IReadOnlyList<string> DownloadedFiles,
    bool VideoPresent,
    bool CandidateAssetOnly,
    bool PublishAuthority,
    string Status,
    string BlockingReason);

public sealed record VidBoardProviderVerificationReceipt(
    string Provider,
    string AccountUserHash,
    string PlanTier,
    bool PresenterVideo,
    bool Mp4Download,
    bool CommercialUseVerified,
    string Status,
    string BlockingReason);
