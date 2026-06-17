namespace Chummer.Run.AI.Services.Assets;

public sealed record SoundmadeseenRenderRequest(
    string RequestId,
    string ApprovedOriginPacketId,
    string OriginRevisionId,
    string ScriptPath,
    string PacketPath,
    string OutputFormat,
    string VoiceVariant,
    string RequestedBy,
    string Source);

public sealed record SoundmadeseenProviderVerificationReceipt(
    string Provider,
    string AccountUserHash,
    string VoiceTier,
    int? MonthlyMinutes,
    bool AudiobookNarration,
    bool CommercialUseVerified,
    bool DownloadVerified,
    string Status,
    string BlockingReason);

public sealed record SoundmadeseenDownloadedAssetReceipt(
    string Provider,
    string ProviderJobId,
    string AssetId,
    IReadOnlyList<string> DownloadedFiles,
    bool AudioPresent,
    bool CandidateAssetOnly,
    bool PublishAuthority,
    string Status,
    string BlockingReason);

public sealed record SoundmadeseenRenderReceipt(
    string ReceiptId,
    string Provider,
    string AccountUserHash,
    string ProviderJobId,
    string VoiceVariant,
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
