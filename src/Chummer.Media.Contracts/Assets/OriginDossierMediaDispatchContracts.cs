#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;

namespace Chummer.Media.Contracts;

public static class OriginDossierMediaDispatchContract
{
    public const string Version = "chummer.origin_dossier_media_dispatch.v2";
    public const int MinimumCinematicDurationSeconds = 120;
    public const int DefaultCinematicDurationSeconds = 135;
    public const int MaximumCinematicDurationSeconds = 900;
    public const int MinimumCinematicDialogueTurns = 4;
    public const string ChapterNarrativeScope = "book_chapter_or_equivalent";
    public const string FullBookNarrativeScope = "full_book";

    public static string BuildRequestId(
        OriginDossierMediaDispatchKind kind,
        string projectId,
        string ownerRefHash,
        string selectionId,
        string originRevisionId)
    {
        string fingerprint = string.Join(
            "|",
            Version,
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
    int DurationTargetSeconds = OriginDossierMediaDispatchContract.DefaultCinematicDurationSeconds,
    string NarrativeScope = OriginDossierMediaDispatchContract.ChapterNarrativeScope,
    bool DialogueRequired = true,
    int MinimumDialogueTurns = OriginDossierMediaDispatchContract.MinimumCinematicDialogueTurns);

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
    string ErrorCode = "",
    string NarrativeScope = "",
    int DialogueTurnCount = 0,
    bool AudioTrackVerified = false);

#pragma warning restore CS1591
