using Chummer.Media.Contracts;

namespace Chummer.Run.AI.Services.Assets;

internal static class OriginDossierScreenplayPromptBuilder
{
    private const int MaximumPromptBeatCharacters = 280;

    private static readonly string[] StoryPhases =
    [
        "establishing geography",
        "shared objective",
        "character interaction",
        "escalating physical action",
        "reversal",
        "consequence",
        "resolution"
    ];

    internal static string BuildShotPrompt(
        OriginDossierMediaDispatchRequest request,
        OriginDossierScreenplayPlan plan,
        int shotIndex,
        int shotCount)
    {
        _ = request;
        int plannedShotCount = Math.Max(shotCount, 1);
        int safeShotIndex = Math.Max(shotIndex, 0);
        bool isContinuityExtension = safeShotIndex >= plannedShotCount;
        int storyShotIndex = Math.Min(safeShotIndex, plannedShotCount - 1);
        int displayedShotCount = Math.Max(plannedShotCount, safeShotIndex + 1);
        string phase = isContinuityExtension
            ? "resolved continuity extension"
            : ResolvePhase(storyShotIndex, plannedShotCount);
        int? dialogueIndex = isContinuityExtension
            ? null
            : ResolveDialogueIndex(storyShotIndex, plannedShotCount, plan.DialogueTurns.Count);
        OriginDossierScreenplayDialogueTurn? dialogue = dialogueIndex is null
            ? null
            : plan.DialogueTurns[dialogueIndex.Value];
        OriginDossierScreenplayCharacter speakerCast = plan.Cast[0];
        OriginDossierScreenplayCharacter speaker = dialogue is null
            ? speakerCast
            : plan.Cast.FirstOrDefault(item =>
                    string.Equals(item.Name, dialogue.Speaker, StringComparison.OrdinalIgnoreCase))
                ?? speakerCast;
        OriginDossierScreenplayCharacter listener = dialogue is null
            ? plan.Cast[1]
            : plan.Cast.FirstOrDefault(item =>
                    string.Equals(item.Name, dialogue.Listener, StringComparison.OrdinalIgnoreCase))
                ?? plan.Cast[1];
        string actionDirection = phase switch
        {
            "establishing geography" =>
                $"Open on a readable master with {speaker.Name} and {listener.Name} together in the same physical space; establish exits, obstacles, and their objective.",
            "shared objective" =>
                $"{speaker.Name} attempts a concrete task while {listener.Name} helps, blocks, questions, or redirects it; both visibly affect the same prop or obstacle.",
            "character interaction" =>
                $"{speaker.Name} and {listener.Name} exchange control of the moment through movement, eyelines, interruption, and a visible reaction—not isolated talking heads.",
            "escalating physical action" =>
                $"Stage a safe but energetic action beat: {speaker.Name} moves to secure, intercept, evade, carry, open, close, or protect something while {listener.Name} actively counters or assists.",
            "reversal" =>
                $"A physical consequence changes the advantage between {speaker.Name} and {listener.Name}; show the cause, the immediate reaction, and the new positions in one continuous beat.",
            "consequence" =>
                $"{speaker.Name} and {listener.Name} deal with the visible result of the action together while their interaction changes the next choice.",
            "resolved continuity extension" =>
                ResolvePickupAction(speaker.Name, listener.Name, safeShotIndex - plannedShotCount),
            _ =>
                $"Resolve the shared objective with {speaker.Name} and {listener.Name} both present; hold on a character reaction that closes this chapter unit without a trailer-style montage."
        };
        string continuityIn = safeShotIndex == 0
            ? "Begin from the opening master positions and establish all continuity anchors clearly."
            : "Begin on the exact positions, prop ownership, eyelines, weather, lighting, and action momentum left by the previous shot; no reset or jump in blocking.";
        string continuityOut = safeShotIndex + 1 >= displayedShotCount
            ? "End on a stable shared tableau that resolves the scene."
            : "End on a visible movement, look, or prop handoff that the next shot can continue without changing direction.";
        int beatIndex = Math.Min(
            plan.RenderBeats.Count - 1,
            (int)((long)storyShotIndex * plan.RenderBeats.Count / plannedShotCount));
        string castBible = string.Join(
            "; ",
            plan.Cast.Select(character =>
                $"{character.Name} ({character.Role}): {character.VisualAnchor}"));
        string dialogueDirection = dialogue is null
            ? "No scripted dialogue in this shot. Do not invent, paraphrase, repeat, or move dialogue from another shot; use natural room tone, action sound, breath, and visible reactions."
            : string.Join(
                " ",
                $"Dialogue ownership is fixed: {speaker.Name} audibly says, verbatim, “{dialogue.Line}” to {listener.Name}; {listener.Name} hears it and gives a visible, motivated reaction.",
                dialogue.UsesSupportingCanonDialogue
                    ? "This exact line comes from approved supporting canon: deliver it naturally inside the unchanged present scene; never depict a flashback, memory insert, lighting change, or time jump."
                    : "Keep the spoken turn inside the selected chapter's present action.",
                "Complete the line at a natural pace inside this shot; never repeat it in another shot.");
        string shotGrammar = ResolveShotGrammar(
            phase,
            dialogue is not null,
            safeShotIndex - plannedShotCount);
        string chapterBeat = CompactBeat(plan.RenderBeats[beatIndex]);
        string shotLabel = isContinuityExtension
            ? $"Continuity pickup {safeShotIndex - plannedShotCount + 1} after the planned {plannedShotCount}-shot scene"
            : $"Continuity shot {safeShotIndex + 1} of {plannedShotCount}";

        return string.Join(
            " ",
            new[]
            {
                $"Origin Dossier screenplay {plan.ContractVersion}: {plan.Title}.",
                $"{shotLabel}; phase: {phase}; one continuous chapter adaptation, never a trailer or unrelated montage.",
                $"Fixed scene bible—location: {plan.PrimaryLocation}; time of day: {plan.TimeOfDay}; weather: {plan.Weather}; wardrobe: {plan.WardrobeContinuity}; screen direction: {plan.ScreenDirectionContinuity}.",
                "Do not change time of day, sun position, weather, location layout, faces, ages, hair, body shape, wardrobe, carried gear, or prop ownership between shots.",
                $"Recurring cast: {castBible}. At least {speaker.Name} and {listener.Name} must be visibly present, interacting, and reacting in this shot; do not replace either with a new person.",
                continuityIn,
                $"Exact chapter beat: {chapterBeat}",
                shotGrammar,
                actionDirection,
                dialogueDirection,
                continuityOut,
                "Natural synchronized voices, room tone, and action sound. Grounded near-future cyberpunk realism, coherent camera grammar, no captions, no logos, no watermark, no silent footage."
            });
    }

    private static string ResolveShotGrammar(
        string phase,
        bool hasDialogue,
        int pickupIndex)
    {
        if (pickupIndex >= 0)
        {
            return (pickupIndex % 4) switch
            {
                0 => "Camera grammar: stable medium two-shot; preserve the settled geography and let one final prop movement motivate the cut.",
                1 => "Camera grammar: restrained over-the-shoulder two-shot with both faces readable; match the previous eyelines exactly.",
                2 => "Camera grammar: short lateral follow, crossing no established screen axis; keep the shared objective visible in frame.",
                _ => "Camera grammar: held closing two-shot with a clean reaction and no new story event."
            };
        }

        if (hasDialogue)
        {
            return "Camera grammar: motivated medium two-shot or over-the-shoulder composition; keep speaker and listener readable, preserve the established eyeline, and hold through the listener's reaction before cutting.";
        }

        return phase switch
        {
            "establishing geography" =>
                "Camera grammar: one calm wide master with readable screen geography; no rapid cuts, inserts, or montage fragments.",
            "shared objective" =>
                "Camera grammar: medium two-shot with a purposeful push or lateral move tied to the shared prop or obstacle.",
            "character interaction" =>
                "Camera grammar: balanced two-shot that favors faces and hands together; cut only on a motivated look or movement.",
            "escalating physical action" =>
                "Camera grammar: wider tracking composition with complete cause-and-effect action; no shaky abstraction or impossible teleporting.",
            "reversal" =>
                "Camera grammar: match on action from the cause to both characters' immediate reaction while preserving the screen axis.",
            "consequence" =>
                "Camera grammar: settle into a closer shared frame, letting the physical result and changed relationship read together.",
            _ =>
                "Camera grammar: held resolving two-shot, then a clean motivated closing image; no teaser montage or abrupt blackout."
        };
    }

    private static string ResolvePickupAction(
        string speaker,
        string listener,
        int pickupIndex)
        => (pickupIndex % 4) switch
        {
            0 => $"Continue the resolved state without restarting the action: {speaker} secures the shared prop while {listener} confirms the route ahead with a visible look.",
            1 => $"Continue the same resolved moment: {listener} finishes the last practical task while {speaker} reacts and keeps the established exit covered.",
            2 => $"Let {speaker} and {listener} move together through the already-established space, carrying forward the exact final positions and objective.",
            _ => $"Hold {speaker} and {listener} in the completed outcome for one final motivated exchange of looks; introduce no new threat, location, or plot turn."
        };

    private static string CompactBeat(string beat)
    {
        string normalized = string.Join(
            " ",
            beat.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length <= MaximumPromptBeatCharacters)
        {
            return normalized;
        }

        string window = normalized[..MaximumPromptBeatCharacters];
        int sentenceEnd = window.LastIndexOfAny(['.', '!', '?']);
        int wordEnd = window.LastIndexOf(' ');
        int cut = sentenceEnd >= MaximumPromptBeatCharacters / 2
            ? sentenceEnd + 1
            : wordEnd >= MaximumPromptBeatCharacters / 2
                ? wordEnd
                : MaximumPromptBeatCharacters;
        return window[..cut].TrimEnd() + "…";
    }

    internal static int? ResolveDialogueIndex(
        int shotIndex,
        int shotCount,
        int dialogueCount)
    {
        for (int dialogueIndex = 0; dialogueIndex < dialogueCount; dialogueIndex++)
        {
            int assignedShot = (int)Math.Round(
                (dialogueIndex + 1) * (shotCount - 1d) / (dialogueCount + 1),
                MidpointRounding.AwayFromZero);
            assignedShot = Math.Clamp(assignedShot, 1, Math.Max(shotCount - 2, 1));
            if (assignedShot == shotIndex)
            {
                return dialogueIndex;
            }
        }

        return null;
    }

    private static string ResolvePhase(int shotIndex, int shotCount)
    {
        if (shotCount <= StoryPhases.Length)
        {
            return StoryPhases[Math.Min(shotIndex, StoryPhases.Length - 1)];
        }

        double progress = shotIndex / (double)Math.Max(shotCount - 1, 1);
        return progress switch
        {
            < 0.10 => StoryPhases[0],
            < 0.24 => StoryPhases[1],
            < 0.45 => StoryPhases[2],
            < 0.67 => StoryPhases[3],
            < 0.82 => StoryPhases[4],
            < 0.94 => StoryPhases[5],
            _ => StoryPhases[6]
        };
    }
}
