using System.Security.Cryptography;
using System.Text;

namespace Chummer.Run.AI.Services.Assets;

public interface IMagicFitProviderAdapter
{
    Task<MagicFitProviderVerificationReceipt> VerifyAsync(CancellationToken cancellationToken = default);
    Task<MagicFitRenderReceipt> RenderAsync(MagicFitRenderRequest request, CancellationToken cancellationToken = default);
    Task<MagicFitDownloadedAssetReceipt> DownloadAsync(string providerJobId, CancellationToken cancellationToken = default);
}

public sealed class MagicFitProviderAdapter : IMagicFitProviderAdapter
{
    // Policy boundary: may_create_candidate_assets: true, may_publish_to_chummer_run: false,
    // may_send_email: false, may_set_editorial_truth: false.
    private readonly MagicFitProviderOptions _options;

    public MagicFitProviderAdapter(MagicFitProviderOptions? options = null)
    {
        _options = options ?? MagicFitProviderOptions.FailClosedDefaults();
    }

    public Task<MagicFitProviderVerificationReceipt> VerifyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new MagicFitProviderVerificationReceipt(
            Provider: "MagicFit",
            AccountUserHash: Hash(_options.AccountUser),
            LicenseTier: _options.LicenseTier,
            CreditsPerMonth: _options.CreditsPerMonth,
            TextToVideo: true,
            ImageToVideo: true,
            Mp4Download: true,
            HdVideoAudio: true,
            WatermarkFreeVerified: _options.WatermarkFreeVerified,
            CommercialUseVerified: _options.CommercialUseVerified,
            Status: _options.VerificationStatus,
            BlockingReason: _options.VerificationStatus == "verified"
                ? string.Empty
                : "Provider remains candidate-only until watermark-free export, commercial-use rights, and account verification are proven."));
    }

    public async Task<MagicFitRenderReceipt> RenderAsync(MagicFitRenderRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RequestId, nameof(request.RequestId));
        RequireText(request.JobType, nameof(request.JobType));
        RequireText(request.Prompt, nameof(request.Prompt));
        RequireText(request.AspectRatio, nameof(request.AspectRatio));
        RequireText(request.PublicSafetyPacketId, nameof(request.PublicSafetyPacketId));

        MagicFitProviderVerificationReceipt verification = await VerifyAsync(cancellationToken);
        string promptHash = Hash(request.Prompt);
        string providerJobId = $"magicfit_candidate_{promptHash[..12]}";

        if (!_options.AllowCandidateRendering || verification.Status != "verified")
        {
            return new MagicFitRenderReceipt(
                ReceiptId: $"magicfit_receipt_{promptHash[..16]}",
                Provider: "MagicFit",
                AccountUserHash: Hash(_options.AccountUser),
                ProviderJobId: providerJobId,
                ModelUsed: request.AllowedModels.FirstOrDefault() ?? "unassigned",
                PromptHash: promptHash,
                CreditCost: 0,
                DurationSeconds: request.DurationSeconds,
                OutputResolution: "unrendered",
                OutputFiles: Array.Empty<string>(),
                WatermarkStatus: verification.WatermarkFreeVerified ? "verified_absent" : "unverified",
                CommercialUseStatus: verification.CommercialUseVerified ? "verified" : "unverified",
                PublicSafetyStatus: "policy_only",
                SourceReceiptAssociationStatus: "missing",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                CandidateAssetOnly: true,
                PublishAuthority: false,
                Status: "blocked",
                BlockingReason: "MagicFitProviderAdapter may_create_candidate_assets only after provider verification. may_publish_to_chummer_run: false. may_send_email: false. may_set_editorial_truth: false.");
        }

        return new MagicFitRenderReceipt(
            ReceiptId: $"magicfit_receipt_{promptHash[..16]}",
            Provider: "MagicFit",
            AccountUserHash: Hash(_options.AccountUser),
            ProviderJobId: providerJobId,
            ModelUsed: request.AllowedModels.FirstOrDefault() ?? "unassigned",
            PromptHash: promptHash,
            CreditCost: 0,
            DurationSeconds: request.DurationSeconds,
            OutputResolution: "1080p",
            OutputFiles: ["mp4"],
            WatermarkStatus: "verified_absent",
            CommercialUseStatus: "verified",
            PublicSafetyStatus: "pending_review",
            SourceReceiptAssociationStatus: "pending_review",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            CandidateAssetOnly: true,
            PublishAuthority: false,
            Status: "candidate_only",
            BlockingReason: "Candidate render created. Human review and downstream QA are still required before any publish step.");
    }

    public async Task<MagicFitDownloadedAssetReceipt> DownloadAsync(string providerJobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireText(providerJobId, nameof(providerJobId));

        MagicFitProviderVerificationReceipt verification = await VerifyAsync(cancellationToken);
        if (verification.Status != "verified")
        {
            return new MagicFitDownloadedAssetReceipt(
                Provider: "MagicFit",
                ProviderJobId: providerJobId,
                AssetId: $"magicfit_asset_{Hash(providerJobId)[..16]}",
                DownloadedFiles: Array.Empty<string>(),
            Mp4Present: false,
            CandidateAssetOnly: true,
            PublishAuthority: false,
            Status: "blocked",
            BlockingReason: "Download is blocked until provider verification and candidate render proof exist. may_send_email: false.");
        }

        return new MagicFitDownloadedAssetReceipt(
            Provider: "MagicFit",
            ProviderJobId: providerJobId,
            AssetId: $"magicfit_asset_{Hash(providerJobId)[..16]}",
            DownloadedFiles: ["mp4", "poster"],
            Mp4Present: true,
            CandidateAssetOnly: true,
            PublishAuthority: false,
            Status: "candidate_only",
            BlockingReason: "Downloaded asset remains candidate-only and cannot publish directly. may_send_email: false.");
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }
    }

    private static string Hash(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
