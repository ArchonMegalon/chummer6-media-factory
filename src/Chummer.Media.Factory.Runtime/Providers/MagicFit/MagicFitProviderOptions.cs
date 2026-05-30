namespace Chummer.Run.AI.Services.Assets;

public sealed record MagicFitProviderOptions(
    string AccountUser,
    string LicenseTier,
    int? CreditsPerMonth,
    bool AllowCandidateRendering,
    string VerificationStatus,
    bool WatermarkFreeVerified,
    bool CommercialUseVerified)
{
    public static MagicFitProviderOptions FailClosedDefaults() => new(
        AccountUser: "tibor.girschele@gmail.com",
        LicenseTier: "License Tier 5",
        CreditsPerMonth: 6000,
        AllowCandidateRendering: false,
        VerificationStatus: "pilot",
        WatermarkFreeVerified: false,
        CommercialUseVerified: false);
}
