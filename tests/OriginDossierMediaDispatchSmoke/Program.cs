using Chummer.Media.Contracts;
using Chummer.Run.AI.Services.Assets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

string root = Path.Combine(
    Path.GetTempPath(),
    "chummer-origin-dossier-dispatch-" + Guid.NewGuid().ToString("N"));
string sourceRoot = Path.Combine(root, "source");
string inboxRoot = Path.Combine(root, "inbox");
string receiptRoot = Path.Combine(root, "receipts");
string outputRoot = Path.Combine(root, "outputs");
Directory.CreateDirectory(sourceRoot);
Directory.CreateDirectory(inboxRoot);
string manuscriptPath = Path.Combine(sourceRoot, "story.md");
string packetPath = Path.Combine(sourceRoot, "packet.json");
string coverPath = Path.Combine(sourceRoot, "cover.png");
string storyboardPath = Path.Combine(sourceRoot, "storyboard.json");
await File.WriteAllTextAsync(manuscriptPath, "# Chapter 1\n\nA rights-safe test story.");
await File.WriteAllTextAsync(packetPath, """{"packetId":"packet-1"}""");
await File.WriteAllBytesAsync(coverPath, [0x89, 0x50, 0x4e, 0x47]);
await File.WriteAllTextAsync(storyboardPath, """{"scenes":[]}""");

var audiobook = new FakeAudiobookRenderer();
var cinematic = new FakeCinematicRenderer();
var voiceResolver = new UnmixrOriginDossierAudiobookRenderer(
    apiKeys: ["test-key"],
    voiceMap: new Dictionary<string, string>
    {
        ["voice-noir"] = "008f05b0-d477-41c5-b8f4-1bb2815dd06a",
        ["voice-wire"] = "a5291143-a094-4ed0-8a36-ce4cdc29b7c4"
    },
    maximumCharactersPerRequest: 3_000,
    maximumParallelRequests: 8);
Assert(
    voiceResolver.MaximumCharactersPerRequest == 1_900,
    "Unmixr audiobook chunks must stay safely below the provider's 2,000-character limit.");
Assert(
    voiceResolver.MaximumParallelRequests == 1,
    "Unmixr parallelism must not exceed the number of configured provider accounts.");
Assert(
    voiceResolver.ResolveProviderVoiceId("voice-noir")
        == "008f05b0-d477-41c5-b8f4-1bb2815dd06a",
    "Provider-neutral voice aliases must resolve only inside media-factory.");
AssertThrows(
    () => voiceResolver.ResolveProviderVoiceId("11111111-1111-1111-1111-111111111111"),
    "Unknown voice aliases must fail closed before provider spend.");
OriginDossierMediaDispatchRequest dialogueRequest = Request(
    OriginDossierMediaDispatchKind.CinematicScene,
    "chapter-8");
OriginDossierScreenplayPlan screenplay = dialogueRequest.Screenplay
    ?? throw new InvalidOperationException("Cinematic smoke request is missing its approved screenplay.");
string[] shotPrompts = Enumerable.Range(0, screenplay.PlannedShotCount)
    .Select(index => OriginDossierScreenplayPromptBuilder.BuildShotPrompt(
        dialogueRequest,
        screenplay,
        index,
        screenplay.PlannedShotCount))
    .ToArray();
string openingPrompt = shotPrompts[0];
string actionPrompt = shotPrompts[14];
string closingPrompt = shotPrompts[^1];
foreach (string prompt in shotPrompts)
{
    Assert(
        prompt.Contains("continuous dusk", StringComparison.Ordinal)
        && prompt.Contains("the same continuous rain", StringComparison.Ordinal)
        && prompt.Contains("Do not change time of day", StringComparison.Ordinal),
        "Every generated shot must repeat the same daylight and weather continuity bible.");
    Assert(
        prompt.Contains("Kestrel", StringComparison.Ordinal)
        && prompt.Contains("Vela", StringComparison.Ordinal)
        && prompt.Contains("visibly present, interacting, and reacting", StringComparison.Ordinal),
        "Every generated shot must stage recurring characters interacting in the same scene.");
    Assert(
        prompt.Contains("Camera grammar:", StringComparison.Ordinal),
        "Every screenplay shot must carry deliberate cinematic camera grammar.");
}
Assert(
    actionPrompt.Contains("safe but energetic action beat", StringComparison.Ordinal),
    "The middle act must contain physical action.");
foreach (OriginDossierScreenplayDialogueTurn turn in screenplay.DialogueTurns)
{
    Assert(
        shotPrompts.Count(prompt => prompt.Contains(
            $"audibly says, verbatim, “{turn.Line}”",
            StringComparison.Ordinal)) == 1,
        "Every approved dialogue turn must be assigned exactly once across the movie.");
}
Assert(
    shotPrompts.Any(prompt => prompt.Contains("No scripted dialogue in this shot", StringComparison.Ordinal)),
    "Action and reaction shots must not repeat scripted dialogue.");
Assert(
    shotPrompts.All(prompt => !prompt.Contains("memory beat", StringComparison.OrdinalIgnoreCase)),
    "Supporting canon must never introduce a flashback or daylight continuity break.");
Assert(
    closingPrompt.Contains("End on a stable shared tableau", StringComparison.Ordinal),
    "The screenplay must resolve as a chapter scene instead of ending like a random montage.");
Assert(
    OriginDossierScreenplayPromptBuilder.ResolveDialogueIndex(3, 27, 8) == 0
    && OriginDossierScreenplayPromptBuilder.ResolveDialogueIndex(6, 27, 8) == 1
    && OriginDossierScreenplayPromptBuilder.ResolveDialogueIndex(2, 27, 8) is null,
    "Dialogue ownership must resolve deterministically to the same shot used by controlled audio.");
Assert(
    MagicFitOriginDossierCinematicSceneRenderer.ShouldRenderNextShot(
        nextShotIndex: 8,
        maximumShotCount: 46,
        plannedShotCount: 27,
        cumulativeObservedDuration: 135,
        renderTargetSeconds: 135),
    "A provider reaching the duration target early must not truncate the planned story arc.");
Assert(
    !MagicFitOriginDossierCinematicSceneRenderer.ShouldRenderNextShot(
        nextShotIndex: 27,
        maximumShotCount: 46,
        plannedShotCount: 27,
        cumulativeObservedDuration: 135,
        renderTargetSeconds: 135),
    "Rendering must stop when both the story arc and duration target are complete.");
Assert(
    MagicFitOriginDossierCinematicSceneRenderer.ShouldRenderNextShot(
        nextShotIndex: 27,
        maximumShotCount: 46,
        plannedShotCount: 27,
        cumulativeObservedDuration: 130,
        renderTargetSeconds: 135),
    "A completed story arc must receive continuity pickups when verified duration is still short.");
Assert(
    MagicFitOriginDossierCinematicSceneRenderer.ShouldRetryProviderRender(
        attemptNumber: 1,
        exitCode: 1,
        outputExists: false),
    "A transient provider failure must receive a bounded retry without discarding earlier shots.");
Assert(
    !MagicFitOriginDossierCinematicSceneRenderer.ShouldRetryProviderRender(
        attemptNumber: 3,
        exitCode: 1,
        outputExists: false),
    "Provider retries must stop at the bounded attempt limit.");
Assert(
    !MagicFitOriginDossierCinematicSceneRenderer.ShouldRetryProviderRender(
        attemptNumber: 1,
        exitCode: 0,
        outputExists: true),
    "A successful provider shot must never spend a retry credit.");
var processor = new OriginDossierMediaInboxProcessor(
    inboxRoot,
    receiptRoot,
    outputRoot,
    [sourceRoot],
    audiobook,
    cinematic);

OriginDossierMediaDispatchRequest audioRequest = Request(
    OriginDossierMediaDispatchKind.Audiobook,
    "voice-calm");
await WriteRequestAsync(audioRequest);
IReadOnlyList<OriginDossierMediaDispatchReceipt> audioReceipts =
    await processor.ProcessPendingAsync();
Assert(audioReceipts.Count == 1, "Audiobook request should produce one receipt.");
OriginDossierMediaDispatchReceipt audioReceipt = audioReceipts[0];
Assert(audioReceipt.Status == "succeeded", "Audiobook request should succeed.");
Assert(audioReceipt.ProviderClass == "approved_tts", "Audiobook provider must stay redacted.");
Assert(audioReceipt.OutputContentType == "audio/mp4", "Audiobook output must be an M4B-compatible media type.");
Assert(File.Exists(audioReceipt.OutputPath), "Audiobook output must exist.");
Assert(audioReceipt.OutputSha256 == Sha256File(audioReceipt.OutputPath), "Audiobook output digest must bind the file.");
Assert(audiobook.CallCount == 1, "Audiobook renderer should run exactly once.");

await WriteRequestAsync(audioRequest);
IReadOnlyList<OriginDossierMediaDispatchReceipt> replayReceipts =
    await processor.ProcessPendingAsync();
Assert(replayReceipts.Count == 1, "Replay should return the durable receipt.");
Assert(audiobook.CallCount == 1, "Replay must not spend provider credits again.");
Assert(
    replayReceipts[0].OutputSha256 == audioReceipt.OutputSha256,
    "Replay must preserve the original output digest.");

OriginDossierMediaDispatchRequest videoRequest = Request(
    OriginDossierMediaDispatchKind.CinematicScene,
    "scene-lantern");
await WriteRequestAsync(videoRequest);
IReadOnlyList<OriginDossierMediaDispatchReceipt> videoReceipts =
    await processor.ProcessPendingAsync();
Assert(videoReceipts.Count == 1, "Cinematic request should produce one receipt.");
OriginDossierMediaDispatchReceipt videoReceipt = videoReceipts[0];
Assert(videoReceipt.Status == "succeeded", "Cinematic request should succeed.");
Assert(videoReceipt.ProviderClass == "preferred_video", "Video provider must stay redacted.");
Assert(videoReceipt.OutputContentType == "video/mp4", "Cinematic output must be MP4.");
Assert(
    videoReceipt.ObservedDurationSeconds == 125.085,
    "Chapter movie receipt must use the verified two-minute-plus duration.");
Assert(
    videoReceipt.RenderScope == OriginDossierMediaDispatchContract.ChapterRenderScope,
    "Chapter movie receipt must preserve chapter-scale scope.");
Assert(
    videoReceipt.DialogueTurnCount >= OriginDossierMediaDispatchContract.MinimumCinematicDialogueTurns,
    "Chapter movie receipt must prove multiple dialogue turns.");
Assert(videoReceipt.AudioTrackVerified, "Chapter movie receipt must prove an audio track.");
Assert(cinematic.CallCount == 1, "Cinematic renderer should run exactly once.");

string tamperedInboxRoot = Path.Combine(root, "tampered-inbox");
string tamperedReceiptRoot = Path.Combine(root, "tampered-receipts");
string tamperedOutputRoot = Path.Combine(root, "tampered-outputs");
Directory.CreateDirectory(tamperedInboxRoot);
var tamperedProcessor = new OriginDossierMediaInboxProcessor(
    tamperedInboxRoot,
    tamperedReceiptRoot,
    tamperedOutputRoot,
    [sourceRoot],
    audiobook,
    cinematic);
OriginDossierMediaDispatchRequest tamperedRequest = Request(
    OriginDossierMediaDispatchKind.CinematicScene,
    "scene-tampered");
tamperedRequest = tamperedRequest with
{
    Screenplay = tamperedRequest.Screenplay! with { Title = "Unbound replacement title" }
};
await WriteRequestAsync(tamperedRequest, tamperedInboxRoot);
OriginDossierMediaDispatchReceipt tamperedReceipt = AssertSingle(
    await tamperedProcessor.ProcessPendingAsync(),
    "A fingerprint-tampered screenplay should produce one failed receipt.");
Assert(tamperedReceipt.Status == "failed", "A tampered screenplay must fail closed.");
Assert(
    tamperedReceipt.ErrorCode == "origin_dossier_media_chapter_dialogue_contract_invalid",
    "Fingerprint tampering must fail before provider execution with a machine-readable error.");
Assert(cinematic.CallCount == 1, "Fingerprint tampering must not spend provider credits.");

string shortInboxRoot = Path.Combine(root, "short-inbox");
string shortReceiptRoot = Path.Combine(root, "short-receipts");
string shortOutputRoot = Path.Combine(root, "short-outputs");
Directory.CreateDirectory(shortInboxRoot);
var shortProcessor = new OriginDossierMediaInboxProcessor(
    shortInboxRoot,
    shortReceiptRoot,
    shortOutputRoot,
    [sourceRoot],
    audiobook,
    new FakeCinematicRenderer(observedDurationSeconds: 15));
OriginDossierMediaDispatchRequest shortVideoRequest = Request(
    OriginDossierMediaDispatchKind.CinematicScene,
    "scene-too-short");
await WriteRequestAsync(shortVideoRequest, shortInboxRoot);
OriginDossierMediaDispatchReceipt shortVideoReceipt = AssertSingle(
    await shortProcessor.ProcessPendingAsync(),
    "A short chapter movie request should produce one failed receipt.");
Assert(shortVideoReceipt.Status == "failed", "A chapter movie below two minutes must fail closed.");
Assert(
    shortVideoReceipt.ErrorCode == "origin_dossier_media_cinematic_too_short",
    "The short chapter movie failure must stay machine-readable.");

Directory.Delete(root, recursive: true);
Console.WriteLine("Origin Dossier media dispatch smoke passed.");

return;

OriginDossierMediaDispatchRequest Request(
    OriginDossierMediaDispatchKind kind,
    string selectionId)
{
    var request = new OriginDossierMediaDispatchRequest(
        ContractVersion: OriginDossierMediaDispatchContract.Version,
        RequestId: string.Empty,
        Kind: kind,
        ProjectId: "origin-smoke",
        OwnerRefHash: new string('a', 64),
        ApprovedOriginPacketId: "packet-1",
        OriginRevisionId: new string('b', 64),
        Source: "chummer6-hub",
        RequestedAtUtc: DateTimeOffset.UtcNow,
        Locale: "en-US",
        SelectionId: selectionId,
        SelectionLabel: kind == OriginDossierMediaDispatchKind.Audiobook
            ? "Calm voice"
            : "The Lantern Test",
        SelectionSummary: "A rights-safe selected scene summary.",
        ManuscriptPath: manuscriptPath,
        SourcePacketPath: packetPath,
        CoverPath: coverPath,
        SequencePlanPath: storyboardPath,
        DurationTargetSeconds: kind == OriginDossierMediaDispatchKind.CinematicScene
            ? OriginDossierMediaDispatchContract.DefaultCinematicDurationSeconds
            : 1,
        RenderScope: kind == OriginDossierMediaDispatchKind.CinematicScene
            ? OriginDossierMediaDispatchContract.ChapterRenderScope
            : OriginDossierMediaDispatchContract.FullBookRenderScope,
        DialogueRequired: kind == OriginDossierMediaDispatchKind.CinematicScene,
        MinimumDialogueTurns: kind == OriginDossierMediaDispatchKind.CinematicScene
            ? OriginDossierMediaDispatchContract.MinimumCinematicDialogueTurns
            : 0);
    request = request with
    {
        RequestId = OriginDossierMediaDispatchContract.BuildRequestId(
            request.Kind,
            request.ProjectId,
            request.OwnerRefHash,
            request.SelectionId,
            request.OriginRevisionId)
    };
    return request.Kind == OriginDossierMediaDispatchKind.CinematicScene
        ? request with { Screenplay = ApprovedScreenplay(request) }
        : request;
}

static OriginDossierScreenplayPlan ApprovedScreenplay(
    OriginDossierMediaDispatchRequest request)
{
    var plan = new OriginDossierScreenplayPlan(
        ContractVersion: OriginDossierScreenplayContract.Version,
        Title: request.SelectionLabel,
        RenderScope: OriginDossierMediaDispatchContract.ChapterRenderScope,
        TimeOfDay: "continuous dusk",
        Weather: "the same continuous rain",
        PrimaryLocation: "Vela's connected street clinic treatment room",
        WardrobeContinuity: "the exact wardrobe and carried gear established in shot 1",
        ScreenDirectionContinuity: "Kestrel remains screen-left of Vela until a visible crossing",
        Cast:
        [
            new(
                "Kestrel",
                "scene protagonist",
                "the same adult Kestrel with unchanged face, hair, build, wardrobe, and evidence case"),
            new(
                "Vela",
                "scene counterpart",
                "the same adult Vela with unchanged face, hair, build, clinic coat, and carried gear")
        ],
        RenderBeats:
        [
            "Kestrel carries the evidence case into Vela's clinic while a patrol closes in.",
            "Vela blocks the rear door and redirects Kestrel toward the service corridor.",
            "A drone arm reaches for the case, forcing both characters to act.",
            "Vela catches the case and returns it so they can escape together."
        ],
        DialogueTurns:
        [
            new("Vela", "Kestrel", "You're tracking mud inside."),
            new("Kestrel", "Vela", "I can wipe my boots."),
            new("Vela", "Kestrel", "Then move before they see the case."),
            new("Kestrel", "Vela", "Cover the corridor and stay with me.")
        ],
        UsesSupportingCanonDialogue: false,
        PlannedShotCount: 27,
        FingerprintSha256: string.Empty);
    return plan with
    {
        FingerprintSha256 = OriginDossierScreenplayContract.BuildFingerprint(request, plan)
    };
}

async Task WriteRequestAsync(
    OriginDossierMediaDispatchRequest request,
    string? targetInboxRoot = null)
{
    string path = Path.Combine(targetInboxRoot ?? inboxRoot, request.RequestId + ".request.json");
    await File.WriteAllTextAsync(
        path,
        JsonSerializer.Serialize(
            request,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
}

static OriginDossierMediaDispatchReceipt AssertSingle(
    IReadOnlyList<OriginDossierMediaDispatchReceipt> receipts,
    string message)
{
    Assert(receipts.Count == 1, message);
    return receipts[0];
}

static string Sha256File(string path)
    => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows(Action action, string message)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

sealed class FakeAudiobookRenderer : IOriginDossierAudiobookRenderer
{
    public int CallCount { get; private set; }

    public async Task<OriginDossierMediaRenderResult> RenderAsync(
        OriginDossierMediaDispatchRequest request,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "story.m4b");
        await File.WriteAllBytesAsync(
            outputPath,
            Encoding.UTF8.GetBytes("rights-safe-m4b-smoke"),
            cancellationToken);
        return new(
            "approved_tts",
            outputPath,
            "audio/mp4",
            12.5,
            new string('c', 64));
    }
}

sealed class FakeCinematicRenderer : IOriginDossierCinematicSceneRenderer
{
    private readonly double _observedDurationSeconds;

    public FakeCinematicRenderer(double observedDurationSeconds = 125.085)
    {
        _observedDurationSeconds = observedDurationSeconds;
    }

    public int CallCount { get; private set; }

    public async Task<OriginDossierMediaRenderResult> RenderAsync(
        OriginDossierMediaDispatchRequest request,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "scene.mp4");
        await File.WriteAllBytesAsync(
            outputPath,
            Encoding.UTF8.GetBytes("rights-safe-mp4-smoke"),
            cancellationToken);
        return new(
            "preferred_video",
            outputPath,
            "video/mp4",
            _observedDurationSeconds,
            new string('d', 64),
            OriginDossierMediaDispatchContract.ChapterRenderScope,
            OriginDossierMediaDispatchContract.MinimumCinematicDialogueTurns,
            true);
    }
}
