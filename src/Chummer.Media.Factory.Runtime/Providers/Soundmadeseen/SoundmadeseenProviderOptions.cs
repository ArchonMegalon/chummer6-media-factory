namespace Chummer.Run.AI.Services.Assets;

public sealed record SoundmadeseenProviderOptions(
    string AccountUser,
    string VoiceTier,
    int? MonthlyMinutes,
    bool AllowCandidateRendering,
    string VerificationStatus,
    bool CommercialUseVerified,
    bool DownloadVerified)
{
    public static SoundmadeseenProviderOptions FailClosedDefaults() => new(
        AccountUser: "tibor.girschele@gmail.com",
        VoiceTier: "Narration Studio",
        MonthlyMinutes: 240,
        AllowCandidateRendering: false,
        VerificationStatus: "pilot",
        CommercialUseVerified: false,
        DownloadVerified: false);
}
