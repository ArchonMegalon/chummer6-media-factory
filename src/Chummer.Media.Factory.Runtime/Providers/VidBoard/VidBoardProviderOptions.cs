namespace Chummer.Run.AI.Services.Assets;

public sealed record VidBoardProviderOptions(
    string AccountUser,
    string PlanTier,
    bool CommercialUseVerified,
    bool AllowCandidateRendering,
    string VerificationStatus)
{
    public static VidBoardProviderOptions FailClosedDefaults()
        => new(
            AccountUser: "candidate@vidboard.invalid",
            PlanTier: "candidate",
            CommercialUseVerified: false,
            AllowCandidateRendering: false,
            VerificationStatus: "candidate_only");
}
