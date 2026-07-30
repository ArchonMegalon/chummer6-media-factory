#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;

namespace Chummer.Media.Contracts;

public static class OriginDossierMediaDispatchContract
{
    public const string Version = "chummer.origin_dossier_media_dispatch.v1";

    public static string BuildRequestId(
        OriginDossierMediaDispatchKind kind,
        string projectId,
        string ownerRefHash,
        string selectionId,
        string originRevisionId)
    {
        string fingerprint = string.Join(
            "|",
            projectId,
            ownerRefHash,
            kind,
            selectionId,
            originRevisionId);
        string hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)))
            .ToLowerInvariant();
        return $"origin-media-{hash[..24]}";
    }
}

public enum OriginDossierMediaDispatchKind
{
    Audiobook,
    CinematicScene
}

public sealed record OriginDossierMediaDispatchRequest(
    string ContractVersion,
    string RequestId,
    OriginDossierMediaDispatchKind Kind,
    string ProjectId,
    string OwnerRefHash,
    string ApprovedOriginPacketId,
    string OriginRevisionId,
    string Source,
    DateTimeOffset RequestedAtUtc,
    string Locale,
    string SelectionId,
    string SelectionLabel,
    string SelectionSummary,
    string ManuscriptPath,
    string SourcePacketPath,
    string? CoverPath,
    string? StoryboardPath,
    int DurationTargetSeconds = 10);

public sealed record OriginDossierMediaDispatchReceipt(
    string ContractVersion,
    string RequestId,
    OriginDossierMediaDispatchKind Kind,
    string ProjectId,
    string OwnerRefHash,
    string OriginRevisionId,
    string SelectionId,
    string Status,
    string ProviderClass,
    string OutputPath,
    string OutputContentType,
    string OutputSha256,
    long OutputBytes,
    double? ObservedDurationSeconds,
    string RequestSha256,
    string ProviderExecutionRefHash,
    DateTimeOffset CompletedAtUtc,
    string ErrorCode = "");

#pragma warning restore CS1591
