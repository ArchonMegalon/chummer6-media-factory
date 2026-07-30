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
Assert(videoReceipt.ObservedDurationSeconds == 5.085, "Receipt must use observed duration.");
Assert(cinematic.CallCount == 1, "Cinematic renderer should run exactly once.");

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
        StoryboardPath: storyboardPath,
        DurationTargetSeconds: 10);
    return request with
    {
        RequestId = OriginDossierMediaDispatchContract.BuildRequestId(
            request.Kind,
            request.ProjectId,
            request.OwnerRefHash,
            request.SelectionId,
            request.OriginRevisionId)
    };
}

async Task WriteRequestAsync(OriginDossierMediaDispatchRequest request)
{
    string path = Path.Combine(inboxRoot, request.RequestId + ".request.json");
    await File.WriteAllTextAsync(
        path,
        JsonSerializer.Serialize(
            request,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
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
            5.085,
            new string('d', 64));
    }
}
