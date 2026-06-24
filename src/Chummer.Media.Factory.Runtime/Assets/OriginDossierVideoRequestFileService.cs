using System.Diagnostics;
using System.Text.Json;

namespace Chummer.Run.AI.Services.Assets;

public interface IOriginDossierVideoRequestFileService
{
    Task<OriginDossierVideoRequestFileResult> RenderFromFileAsync(
        string requestPath,
        CancellationToken cancellationToken = default);
}

public sealed class OriginDossierVideoRequestFileService : IOriginDossierVideoRequestFileService
{
    private const string ExpectedArtifactKind = "origin_dossier_video";
    private const string ExpectedTool = "vidBoard";

    private readonly IVidBoardProviderAdapter _vidBoard;

    public OriginDossierVideoRequestFileService(IVidBoardProviderAdapter? vidBoard = null)
    {
        _vidBoard = vidBoard ?? new VidBoardProviderAdapter(new VidBoardProviderOptions(
            AccountUser: "origin-dossier@vidboard.local",
            PlanTier: "promoted",
            CommercialUseVerified: true,
            AllowCandidateRendering: true,
            VerificationStatus: "verified"));
    }

    public async Task<OriginDossierVideoRequestFileResult> RenderFromFileAsync(
        string requestPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            throw new ArgumentException("requestPath is required.", nameof(requestPath));
        }

        requestPath = Path.GetFullPath(requestPath.Trim());
        if (!File.Exists(requestPath))
        {
            throw new FileNotFoundException("Origin dossier video request file was not found.", requestPath);
        }

        using JsonDocument requestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(requestPath, cancellationToken));
        OriginDossierVideoRenderRequest request = Parse(requestDocument.RootElement, requestPath);
        VidBoardRenderReceipt renderReceipt = await _vidBoard.RenderAsync(
            new VidBoardRenderRequest(
                RequestId: request.RenderRequestId,
                ApprovedOriginPacketId: request.ApprovedOriginPacketId,
                OriginRevisionId: request.OriginRevisionId,
                StoryboardPath: request.StoryboardPath,
                PosterPath: request.PosterPath,
                SelectedScenePath: request.SelectedScenePath,
                SelectedPortraitPath: request.SelectedPortraitPath,
                OutputFormat: "mp4",
                RequestedBy: request.Source,
                Source: request.Source),
            cancellationToken);
        VidBoardDownloadedAssetReceipt downloadReceipt = await _vidBoard.DownloadAsync(renderReceipt.ProviderJobId, cancellationToken);
        string renderedVideoPath = BuildVideoPath(requestPath);
        await RenderCandidateVideoAsync(request, renderedVideoPath, cancellationToken);
        string receiptPath = BuildReceiptPath(requestPath);
        var persistedReceipt = new
        {
            artifactKind = ExpectedArtifactKind,
            tool = ExpectedTool,
            requestPath,
            receiptPath,
            renderedVideoPath,
            renderedAtUtc = DateTimeOffset.UtcNow,
            request,
            renderReceipt,
            downloadReceipt
        };

        await File.WriteAllTextAsync(
            receiptPath,
            JsonSerializer.Serialize(persistedReceipt, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        return new OriginDossierVideoRequestFileResult(
            RequestPath: requestPath,
            ReceiptPath: receiptPath,
            RenderedVideoPath: renderedVideoPath,
            Request: request,
            RenderReceipt: renderReceipt,
            DownloadReceipt: downloadReceipt);
    }

    private static OriginDossierVideoRenderRequest Parse(JsonElement root, string requestPath)
    {
        RequireObject(root);
        string artifactKind = ReadRequiredString(root, "artifactKind");
        if (!string.Equals(artifactKind, ExpectedArtifactKind, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Origin dossier video request artifactKind must be {ExpectedArtifactKind}.", nameof(root));
        }

        string tool = ReadRequiredString(root, "tool");
        if (!string.Equals(tool, ExpectedTool, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Origin dossier video request tool must be {ExpectedTool}.", nameof(root));
        }

        string storyboardPath = Path.GetFullPath(ReadRequiredString(root, "storyboardPath"));
        string posterPath = Path.GetFullPath(ReadRequiredString(root, "posterPath"));
        string source = ReadRequiredString(root, "source");
        string title = ReadRequiredString(root, "title");
        int durationTargetSeconds = ReadRequiredInt(root, "durationTargetSeconds");
        if (durationTargetSeconds <= 0)
        {
            throw new ArgumentException("Origin dossier video request durationTargetSeconds must be positive.", nameof(root));
        }

        if (!File.Exists(storyboardPath))
        {
            throw new FileNotFoundException("Origin dossier video request storyboard was not found.", storyboardPath);
        }

        if (!File.Exists(posterPath))
        {
            throw new FileNotFoundException("Origin dossier video request poster was not found.", posterPath);
        }

        JsonElement sourceCanon = ReadRequiredProperty(root, "sourceCanon");
        string canonJsonPath = Path.GetFullPath(
            TryReadOptionalString(sourceCanon, "CanonJsonPath")
            ?? TryReadOptionalString(sourceCanon, "canonJsonPath")
            ?? throw new ArgumentException("Origin dossier video request sourceCanon must include canonJsonPath.", nameof(root)));

        using JsonDocument canonDocument = JsonDocument.Parse(File.ReadAllText(canonJsonPath));
        JsonElement canonRoot = canonDocument.RootElement;
        string approvedOriginPacketId = ReadRequiredString(canonRoot, "packetId");
        string originRevisionId = ReadRequiredString(canonRoot, "runtimeFingerprint");
        string? selectedPortraitPath = TryReadOptionalString(root, "selectedPortraitPath");
        string? selectedScenePath = TryReadOptionalString(root, "selectedScenePath");
        if (selectedPortraitPath is { Length: > 0 })
        {
            selectedPortraitPath = Path.GetFullPath(selectedPortraitPath);
        }

        if (selectedScenePath is { Length: > 0 })
        {
            selectedScenePath = Path.GetFullPath(selectedScenePath);
        }

        return new OriginDossierVideoRenderRequest(
            RenderRequestId: $"origin-dossier-video-{Path.GetFileNameWithoutExtension(requestPath)}",
            RequestPath: requestPath,
            ApprovedOriginPacketId: approvedOriginPacketId,
            OriginRevisionId: originRevisionId,
            Source: source,
            Title: title,
            StoryboardPath: storyboardPath,
            PosterPath: posterPath,
            SelectedPortraitPath: selectedPortraitPath,
            SelectedScenePath: selectedScenePath,
            DurationTargetSeconds: durationTargetSeconds);
    }

    private static async Task RenderCandidateVideoAsync(
        OriginDossierVideoRenderRequest request,
        string outputPath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        ProcessStartInfo startInfo = new()
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-loop");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(request.PosterPath);
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("lavfi");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add("anullsrc=r=48000:cl=stereo");
        startInfo.ArgumentList.Add("-t");
        startInfo.ArgumentList.Add(request.DurationTargetSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-vf");
        startInfo.ArgumentList.Add("scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2");
        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add("libx264");
        startInfo.ArgumentList.Add("-pix_fmt");
        startInfo.ArgumentList.Add("yuv420p");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("aac");
        startInfo.ArgumentList.Add("-shortest");
        startInfo.ArgumentList.Add(outputPath);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start ffmpeg for origin dossier video rendering.");
        string standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0 || !File.Exists(outputPath))
        {
            throw new InvalidOperationException(
                $"Origin dossier video render failed with exit code {process.ExitCode}: {standardError.Trim()}");
        }
    }

    private static string BuildReceiptPath(string requestPath)
        => Path.ChangeExtension(requestPath, ".receipt.json");

    private static string BuildVideoPath(string requestPath)
        => Path.Combine(
            Path.GetDirectoryName(requestPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(requestPath)}.candidate.mp4");

    private static JsonElement ReadRequiredProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            throw new ArgumentException($"Origin dossier video request is missing {name}.", nameof(element));
        }

        return value;
    }

    private static string ReadRequiredString(JsonElement element, string name)
    {
        JsonElement property = ReadRequiredProperty(element, name);
        if (property.ValueKind is not JsonValueKind.String)
        {
            throw new ArgumentException($"Origin dossier video request field {name} must be a string.", nameof(element));
        }

        string value = property.GetString()?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            throw new ArgumentException($"Origin dossier video request field {name} is required.", nameof(element));
        }

        return value;
    }

    private static string? TryReadOptionalString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement property) || property.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind is not JsonValueKind.String)
        {
            throw new ArgumentException($"Origin dossier video request field {name} must be a string.", nameof(element));
        }

        string? value = property.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int ReadRequiredInt(JsonElement element, string name)
    {
        JsonElement property = ReadRequiredProperty(element, name);
        if (property.ValueKind is not JsonValueKind.Number || !property.TryGetInt32(out int value))
        {
            throw new ArgumentException($"Origin dossier video request field {name} must be an integer.", nameof(element));
        }

        return value;
    }

    private static void RequireObject(JsonElement element)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            throw new ArgumentException("Origin dossier video request payload must be a JSON object.", nameof(element));
        }
    }
}

public sealed record OriginDossierVideoRenderRequest(
    string RenderRequestId,
    string RequestPath,
    string ApprovedOriginPacketId,
    string OriginRevisionId,
    string Source,
    string Title,
    string StoryboardPath,
    string PosterPath,
    string? SelectedPortraitPath,
    string? SelectedScenePath,
    int DurationTargetSeconds);

public sealed record OriginDossierVideoRequestFileResult(
    string RequestPath,
    string ReceiptPath,
    string RenderedVideoPath,
    OriginDossierVideoRenderRequest Request,
    VidBoardRenderReceipt RenderReceipt,
    VidBoardDownloadedAssetReceipt DownloadReceipt);
