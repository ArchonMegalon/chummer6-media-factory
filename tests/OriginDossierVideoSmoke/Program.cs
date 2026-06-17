using Chummer.Run.AI.Services.Assets;
using System.Text.Json;

var requestDirectory = Directory.CreateTempSubdirectory("origin-dossier-video-request");
string canonJsonPath = Path.Combine(requestDirectory.FullName, "origin-canon.json");
string storyboardPath = Path.Combine(requestDirectory.FullName, "origin-dossier-video.storyboard.md");
string posterPath = Path.Combine(requestDirectory.FullName, "origin-dossier-video-poster.ppm");
string requestPath = Path.Combine(requestDirectory.FullName, "vidboard-origin-dossier.packet.json");

await File.WriteAllTextAsync(canonJsonPath, JsonSerializer.Serialize(new
{
    packetId = "origin-dossier:smoke:001",
    runtimeFingerprint = "origin-canon:smoke-001"
}));
await File.WriteAllTextAsync(storyboardPath, "# Origin Dossier Video\n\n- intro\n- dossier beat\n- close");
await File.WriteAllTextAsync(posterPath, "P3\n2 2\n255\n38 46 76  123 180 255\n18 22 36  231 241 255\n");
await File.WriteAllTextAsync(
    requestPath,
    JsonSerializer.Serialize(new
    {
        tool = "vidBoard",
        artifactKind = "origin_dossier_video",
        approvedAtUtc = DateTimeOffset.UtcNow,
        source = "first_party_origin_canon",
        title = "Smoke Origin Dossier",
        durationTargetSeconds = 2,
        posterPath,
        storyboardPath,
        selectedPortraitPath = posterPath,
        selectedScenePath = posterPath,
        sourceCanon = new
        {
            canonJsonPath,
            canonMarkdownPath = "/tmp/origin.md",
            dossierPdfPath = "/tmp/origin.pdf",
            mediaFactoryNarrationReceiptPath = "/tmp/narration.receipt.json"
        }
    },
    new JsonSerializerOptions { WriteIndented = true }));

var requests = new OriginDossierVideoRequestFileService();
OriginDossierVideoRequestFileResult result = await requests.RenderFromFileAsync(requestPath);

Assert(File.Exists(result.ReceiptPath), "Origin dossier video request-file rendering should write a receipt beside the request.");
Assert(File.Exists(result.RenderedVideoPath), "Origin dossier video request-file rendering should emit a playable candidate video.");
Assert(new FileInfo(result.RenderedVideoPath).Length > 0, "Origin dossier video candidate render should not be empty.");
Assert(result.RenderReceipt.Status == "candidate_only", "Origin dossier video render should stay candidate-only.");
Assert(result.DownloadReceipt.VideoPresent, "Origin dossier video download receipt should confirm the video lane.");

using JsonDocument receiptDocument = JsonDocument.Parse(await File.ReadAllTextAsync(result.ReceiptPath));
JsonElement root = receiptDocument.RootElement;
Assert(root.TryGetProperty("renderedVideoPath", out JsonElement renderedVideoPath) && renderedVideoPath.GetString() == result.RenderedVideoPath, "Origin dossier video receipt should preserve the rendered video path.");
Assert(root.TryGetProperty("renderReceipt", out _), "Origin dossier video receipt should embed the render receipt.");
Assert(root.TryGetProperty("downloadReceipt", out _), "Origin dossier video receipt should embed the download receipt.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
