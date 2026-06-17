using System.Security.Cryptography;
using System.Text;

namespace Chummer.Run.AI.Services.Assets;

public interface IVidBoardProviderAdapter
{
    Task<VidBoardProviderVerificationReceipt> VerifyAsync(CancellationToken cancellationToken = default);
    Task<VidBoardRenderReceipt> RenderAsync(VidBoardRenderRequest request, CancellationToken cancellationToken = default);
    Task<VidBoardDownloadedAssetReceipt> DownloadAsync(string providerJobId, CancellationToken cancellationToken = default);
}

public sealed class VidBoardProviderAdapter : IVidBoardProviderAdapter
{
    private readonly VidBoardProviderOptions _options;

    public VidBoardProviderAdapter(VidBoardProviderOptions? options = null)
    {
        _options = options ?? VidBoardProviderOptions.FailClosedDefaults();
    }

    public Task<VidBoardProviderVerificationReceipt> VerifyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new VidBoardProviderVerificationReceipt(
            Provider: "vidBoard",
            AccountUserHash: Hash(_options.AccountUser),
            PlanTier: _options.PlanTier,
            PresenterVideo: true,
            Mp4Download: true,
            CommercialUseVerified: _options.CommercialUseVerified,
            Status: _options.VerificationStatus,
            BlockingReason: _options.VerificationStatus == "verified"
                ? string.Empty
                : "Provider remains candidate-only until MP4 export and commercial-use proof are verified."));
    }

    public async Task<VidBoardRenderReceipt> RenderAsync(VidBoardRenderRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RequestId, nameof(request.RequestId));
        RequireText(request.ApprovedOriginPacketId, nameof(request.ApprovedOriginPacketId));
        RequireText(request.OriginRevisionId, nameof(request.OriginRevisionId));
        RequireText(request.StoryboardPath, nameof(request.StoryboardPath));
        RequireText(request.PosterPath, nameof(request.PosterPath));
        RequireText(request.OutputFormat, nameof(request.OutputFormat));

        VidBoardProviderVerificationReceipt verification = await VerifyAsync(cancellationToken);
        string promptHash = Hash($"{request.ApprovedOriginPacketId}|{request.OriginRevisionId}|{request.StoryboardPath}|{request.PosterPath}");
        string providerJobId = $"vidboard_candidate_{promptHash[..12]}";

        if (!_options.AllowCandidateRendering || verification.Status != "verified")
        {
            return new VidBoardRenderReceipt(
                ReceiptId: $"vidboard_receipt_{promptHash[..16]}",
                Provider: "vidBoard",
                AccountUserHash: Hash(_options.AccountUser),
                ProviderJobId: providerJobId,
                PromptHash: promptHash,
                CreditCost: 0,
                OutputFiles: Array.Empty<string>(),
                CommercialUseStatus: verification.CommercialUseVerified ? "verified" : "unverified",
                SourceReceiptAssociationStatus: "pending_review",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                CandidateAssetOnly: true,
                PublishAuthority: false,
                Status: "blocked",
                BlockingReason: "VidBoardProviderAdapter may_create_candidate_assets only after provider verification. may_publish_to_chummer_run: false. may_send_email: false. may_set_editorial_truth: false.");
        }

        return new VidBoardRenderReceipt(
            ReceiptId: $"vidboard_receipt_{promptHash[..16]}",
            Provider: "vidBoard",
            AccountUserHash: Hash(_options.AccountUser),
            ProviderJobId: providerJobId,
            PromptHash: promptHash,
            CreditCost: 0,
            OutputFiles: [request.OutputFormat, "poster"],
            CommercialUseStatus: "verified",
            SourceReceiptAssociationStatus: "pending_review",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            CandidateAssetOnly: true,
            PublishAuthority: false,
            Status: "candidate_only",
            BlockingReason: "Candidate dossier video created. Human review and downstream QA are still required before any publish step.");
    }

    public async Task<VidBoardDownloadedAssetReceipt> DownloadAsync(string providerJobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireText(providerJobId, nameof(providerJobId));

        VidBoardProviderVerificationReceipt verification = await VerifyAsync(cancellationToken);
        if (verification.Status != "verified")
        {
            return new VidBoardDownloadedAssetReceipt(
                Provider: "vidBoard",
                ProviderJobId: providerJobId,
                AssetId: $"vidboard_asset_{Hash(providerJobId)[..16]}",
                DownloadedFiles: Array.Empty<string>(),
                VideoPresent: false,
                CandidateAssetOnly: true,
                PublishAuthority: false,
                Status: "blocked",
                BlockingReason: "Download is blocked until provider verification and candidate video proof exist. may_send_email: false.");
        }

        return new VidBoardDownloadedAssetReceipt(
            Provider: "vidBoard",
            ProviderJobId: providerJobId,
            AssetId: $"vidboard_asset_{Hash(providerJobId)[..16]}",
            DownloadedFiles: ["mp4", "poster"],
            VideoPresent: true,
            CandidateAssetOnly: true,
            PublishAuthority: false,
            Status: "candidate_only",
            BlockingReason: "Downloaded video remains candidate-only and cannot publish directly. may_send_email: false.");
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
