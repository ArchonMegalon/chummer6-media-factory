namespace Chummer.Run.AI.Services.Assets;

public sealed record UnmixrProviderOptions(
    string AccountUser,
    string VoiceTier,
    int? MonthlyMinutes,
    bool AllowCandidateRendering,
    string VerificationStatus,
    bool CommercialUseVerified,
    bool DownloadVerified)
{
    public static UnmixrProviderOptions FailClosedDefaults() => new(
        AccountUser: "tibor.girschele@gmail.com",
        VoiceTier: "Creator",
        MonthlyMinutes: 180,
        AllowCandidateRendering: false,
        VerificationStatus: "candidate",
        CommercialUseVerified: false,
        DownloadVerified: false);
}
