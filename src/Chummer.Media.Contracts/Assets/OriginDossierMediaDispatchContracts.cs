#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;

namespace Chummer.Media.Contracts;

public static class OriginDossierMediaDispatchContract
{
    public const string Version = "chummer.origin_dossier_media_dispatch.v4";
    public const int MinimumCinematicDurationSeconds = 120;
    public const int DefaultCinematicDurationSeconds = 135;
    public const int MaximumCinematicDurationSeconds = 900;
    public const int MinimumCinematicDialogueTurns = 4;
    public const string ChapterRenderScope = "book_chapter_or_equivalent";
    public const string FullBookRenderScope = "full_book";

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

public static class OriginDossierScreenplayContract
{
    public const string Version = "chummer.origin_dossier_screenplay.v2";
    public const int DefaultShotDurationSeconds = 5;
    public const int MaximumNarrativeBeatCharacters = 560;
    public const int MaximumDialogueCharacters = 120;
    public const int MaximumDialogueWordsPerShot = 12;

    public static bool IsDialogueTurnRenderable(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)
            || line.Length > MaximumDialogueCharacters)
        {
            return false;
        }

        return line.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length <= MaximumDialogueWordsPerShot;
    }

    public static string BuildFingerprint(
        OriginDossierMediaDispatchRequest request,
        OriginDossierScreenplayPlan plan)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);
        string canonical = string.Join(
            "\n",
            Version,
            request.ContractVersion,
            request.RequestId,
            request.OriginRevisionId,
            request.SelectionId,
            request.SelectionLabel,
            request.SelectionSummary,
            plan.Title,
            plan.RenderScope,
            plan.PlannedShotCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            plan.TimeOfDay,
            plan.Weather,
            plan.PrimaryLocation,
            plan.WardrobeContinuity,
            plan.ScreenDirectionContinuity,
            plan.UsesSupportingCanonDialogue ? "supporting-canon" : "selected-chapter-only",
            string.Join("|", plan.Cast.Select(item =>
                $"{item.Name}:{item.Role}:{item.VisualAnchor}")),
            string.Join("|", plan.RenderBeats),
            string.Join("|", plan.DialogueTurns.Select(item =>
                $"{item.Speaker}>{item.Listener}:{item.Line}:{(item.UsesSupportingCanonDialogue ? "supporting" : "selected")}")));
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    public static bool FingerprintMatches(
        OriginDossierMediaDispatchRequest request,
        OriginDossierScreenplayPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.FingerprintSha256)
            || plan.FingerprintSha256.Length != 64
            || !plan.FingerprintSha256.All(Uri.IsHexDigit))
        {
            return false;
        }

        byte[] expected = Convert.FromHexString(BuildFingerprint(request, plan));
        byte[] actual = Convert.FromHexString(plan.FingerprintSha256);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
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
    string? SequencePlanPath,
    int DurationTargetSeconds = OriginDossierMediaDispatchContract.DefaultCinematicDurationSeconds,
    string RenderScope = OriginDossierMediaDispatchContract.ChapterRenderScope,
    bool DialogueRequired = true,
    int MinimumDialogueTurns = OriginDossierMediaDispatchContract.MinimumCinematicDialogueTurns,
    OriginDossierScreenplayPlan? Screenplay = null);

public sealed record OriginDossierScreenplayPlan(
    string ContractVersion,
    string Title,
    string RenderScope,
    string TimeOfDay,
    string Weather,
    string PrimaryLocation,
    string WardrobeContinuity,
    string ScreenDirectionContinuity,
    IReadOnlyList<OriginDossierScreenplayCharacter> Cast,
    IReadOnlyList<string> RenderBeats,
    IReadOnlyList<OriginDossierScreenplayDialogueTurn> DialogueTurns,
    bool UsesSupportingCanonDialogue,
    int PlannedShotCount,
    string FingerprintSha256);

public sealed record OriginDossierScreenplayCharacter(
    string Name,
    string Role,
    string VisualAnchor);

public sealed record OriginDossierScreenplayDialogueTurn(
    string Speaker,
    string Listener,
    string Line,
    bool UsesSupportingCanonDialogue = false);

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
    string RenderScope = "",
    int DialogueTurnCount = 0,
    bool AudioTrackVerified = false);

#pragma warning restore CS1591
