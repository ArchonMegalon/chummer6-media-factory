namespace Chummer.Run.AI.Services.Assets;

public sealed record MagicFitProviderVerificationReceipt(
    string Provider,
    string AccountUserHash,
    string LicenseTier,
    int? CreditsPerMonth,
    bool TextToVideo,
    bool ImageToVideo,
    bool Mp4Download,
    bool HdVideoAudio,
    bool WatermarkFreeVerified,
    bool CommercialUseVerified,
    string Status,
    string BlockingReason);

public sealed record MagicFitDownloadedAssetReceipt(
    string Provider,
    string ProviderJobId,
    string AssetId,
    IReadOnlyList<string> DownloadedFiles,
    bool Mp4Present,
    bool CandidateAssetOnly,
    bool PublishAuthority,
    string Status,
    string BlockingReason);

public sealed record MagicFitRenderReceipt(
    string ReceiptId,
    string Provider,
    string AccountUserHash,
    string ProviderJobId,
    string ModelUsed,
    string PromptHash,
    int CreditCost,
    int DurationSeconds,
    string OutputResolution,
    IReadOnlyList<string> OutputFiles,
    string WatermarkStatus,
    string CommercialUseStatus,
    string PublicSafetyStatus,
    string SourceReceiptAssociationStatus,
    DateTimeOffset CreatedAtUtc,
    bool CandidateAssetOnly,
    bool PublishAuthority,
    string Status,
    string BlockingReason);
