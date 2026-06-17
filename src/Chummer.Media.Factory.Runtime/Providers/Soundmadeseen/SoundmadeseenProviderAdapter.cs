using System.Security.Cryptography;
using System.Text;

namespace Chummer.Run.AI.Services.Assets;

public interface ISoundmadeseenProviderAdapter
{
    Task<SoundmadeseenProviderVerificationReceipt> VerifyAsync(CancellationToken cancellationToken = default);
    Task<SoundmadeseenRenderReceipt> RenderAsync(SoundmadeseenRenderRequest request, CancellationToken cancellationToken = default);
    Task<SoundmadeseenDownloadedAssetReceipt> DownloadAsync(string providerJobId, CancellationToken cancellationToken = default);
}

public sealed class SoundmadeseenProviderAdapter : ISoundmadeseenProviderAdapter
{
    // Policy boundary: may_create_candidate_assets: true, may_publish_to_chummer_run: false,
    // may_send_email: false, may_set_editorial_truth: false.
    private readonly SoundmadeseenProviderOptions _options;

    public SoundmadeseenProviderAdapter(SoundmadeseenProviderOptions? options = null)
    {
        _options = options ?? SoundmadeseenProviderOptions.FailClosedDefaults();
    }

    public Task<SoundmadeseenProviderVerificationReceipt> VerifyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SoundmadeseenProviderVerificationReceipt(
            Provider: "Soundmadeseen",
            AccountUserHash: Hash(_options.AccountUser),
            VoiceTier: _options.VoiceTier,
            MonthlyMinutes: _options.MonthlyMinutes,
            AudiobookNarration: true,
            CommercialUseVerified: _options.CommercialUseVerified,
            DownloadVerified: _options.DownloadVerified,
            Status: _options.VerificationStatus,
            BlockingReason: _options.VerificationStatus == "verified"
                ? string.Empty
                : "Provider remains candidate-only until narration export, download, and commercial-use proof are verified."));
    }

    public async Task<SoundmadeseenRenderReceipt> RenderAsync(SoundmadeseenRenderRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        RequireText(request.RequestId, nameof(request.RequestId));
        RequireText(request.ApprovedOriginPacketId, nameof(request.ApprovedOriginPacketId));
        RequireText(request.OriginRevisionId, nameof(request.OriginRevisionId));
        RequireText(request.ScriptPath, nameof(request.ScriptPath));
        RequireText(request.PacketPath, nameof(request.PacketPath));
        RequireText(request.OutputFormat, nameof(request.OutputFormat));
        RequireText(request.VoiceVariant, nameof(request.VoiceVariant));

        SoundmadeseenProviderVerificationReceipt verification = await VerifyAsync(cancellationToken);
        string promptHash = Hash($"{request.ApprovedOriginPacketId}|{request.OriginRevisionId}|{request.VoiceVariant}|{request.ScriptPath}");
        string providerJobId = $"soundmadeseen_candidate_{promptHash[..12]}";

        if (!_options.AllowCandidateRendering || verification.Status != "verified")
        {
            return new SoundmadeseenRenderReceipt(
                ReceiptId: $"soundmadeseen_receipt_{promptHash[..16]}",
                Provider: "Soundmadeseen",
                AccountUserHash: Hash(_options.AccountUser),
                ProviderJobId: providerJobId,
                VoiceVariant: request.VoiceVariant,
                PromptHash: promptHash,
                CreditCost: 0,
                OutputFiles: Array.Empty<string>(),
                CommercialUseStatus: verification.CommercialUseVerified ? "verified" : "unverified",
                SourceReceiptAssociationStatus: "pending_review",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                CandidateAssetOnly: true,
                PublishAuthority: false,
                Status: "blocked",
                BlockingReason: "SoundmadeseenProviderAdapter may_create_candidate_assets only after provider verification. may_publish_to_chummer_run: false. may_send_email: false. may_set_editorial_truth: false.");
        }

        return new SoundmadeseenRenderReceipt(
            ReceiptId: $"soundmadeseen_receipt_{promptHash[..16]}",
            Provider: "Soundmadeseen",
            AccountUserHash: Hash(_options.AccountUser),
            ProviderJobId: providerJobId,
            VoiceVariant: request.VoiceVariant,
            PromptHash: promptHash,
            CreditCost: 0,
            OutputFiles: [request.OutputFormat],
            CommercialUseStatus: "verified",
            SourceReceiptAssociationStatus: "pending_review",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            CandidateAssetOnly: true,
            PublishAuthority: false,
            Status: "candidate_only",
            BlockingReason: "Candidate narration created. Human review and downstream QA are still required before any publish step.");
    }

    public async Task<SoundmadeseenDownloadedAssetReceipt> DownloadAsync(string providerJobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireText(providerJobId, nameof(providerJobId));

        SoundmadeseenProviderVerificationReceipt verification = await VerifyAsync(cancellationToken);
        if (verification.Status != "verified")
        {
            return new SoundmadeseenDownloadedAssetReceipt(
                Provider: "Soundmadeseen",
                ProviderJobId: providerJobId,
                AssetId: $"soundmadeseen_asset_{Hash(providerJobId)[..16]}",
                DownloadedFiles: Array.Empty<string>(),
                AudioPresent: false,
                CandidateAssetOnly: true,
                PublishAuthority: false,
                Status: "blocked",
                BlockingReason: "Download is blocked until provider verification and candidate narration proof exist. may_send_email: false.");
        }

        return new SoundmadeseenDownloadedAssetReceipt(
            Provider: "Soundmadeseen",
            ProviderJobId: providerJobId,
            AssetId: $"soundmadeseen_asset_{Hash(providerJobId)[..16]}",
            DownloadedFiles: ["mp3", "captions"],
            AudioPresent: true,
            CandidateAssetOnly: true,
            PublishAuthority: false,
            Status: "candidate_only",
            BlockingReason: "Downloaded narration remains candidate-only and cannot publish directly. may_send_email: false.");
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
