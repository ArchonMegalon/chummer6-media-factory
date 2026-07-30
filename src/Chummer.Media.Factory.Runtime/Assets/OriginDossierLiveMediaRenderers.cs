using Chummer.Media.Contracts;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chummer.Run.AI.Services.Assets;

public sealed class UnmixrOriginDossierAudiobookRenderer : IOriginDossierAudiobookRenderer
{
    private const string DefaultApiBaseUrl = "https://unmixr.com/api/v1";
    private readonly HttpClient _http;
    private readonly string _apiBaseUrl;
    private readonly IReadOnlyList<string> _apiKeys;
    private readonly IReadOnlyDictionary<string, string> _voiceMap;
    private readonly int _maximumCharactersPerRequest;

    public UnmixrOriginDossierAudiobookRenderer(
        HttpClient? httpClient = null,
        string? apiBaseUrl = null,
        IEnumerable<string>? apiKeys = null,
        IReadOnlyDictionary<string, string>? voiceMap = null,
        int maximumCharactersPerRequest = 3_000)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        _apiBaseUrl = (apiBaseUrl ?? Environment.GetEnvironmentVariable("CHUMMER_MEDIA_FACTORY_UNMIXR_API_BASE_URL")
            ?? DefaultApiBaseUrl).Trim().TrimEnd('/');
        _apiKeys = (apiKeys ?? LoadApiKeys())
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _voiceMap = voiceMap ?? LoadVoiceMap();
        _maximumCharactersPerRequest = Math.Clamp(maximumCharactersPerRequest, 500, 5_000);
    }

    public async Task<OriginDossierMediaRenderResult> RenderAsync(
        OriginDossierMediaDispatchRequest request,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_apiKeys.Count == 0)
        {
            throw new InvalidOperationException("origin_dossier_media_unmixr_credentials_missing");
        }

        FileInfo manuscript = new(request.ManuscriptPath);
        if (!manuscript.Exists || manuscript.Length is <= 0 or > 8 * 1024 * 1024)
        {
            throw new InvalidOperationException("origin_dossier_media_manuscript_invalid");
        }

        string source = await File.ReadAllTextAsync(manuscript.FullName, cancellationToken);
        string narration = NormalizeManuscript(source);
        IReadOnlyList<string> chunks = SplitNarration(narration, _maximumCharactersPerRequest);
        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("origin_dossier_media_manuscript_empty");
        }

        Directory.CreateDirectory(outputDirectory);
        string voiceId = ResolveProviderVoiceId(request.SelectionId);
        string segmentRoot = Path.Combine(outputDirectory, "segments");
        Directory.CreateDirectory(segmentRoot);
        var segmentPaths = new List<string>(chunks.Count);
        var providerRefs = new List<string>(chunks.Count);
        for (int index = 0; index < chunks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UnmixrAudioResponse audio = await SynthesizeAsync(
                chunks[index],
                voiceId,
                request.Locale,
                index,
                cancellationToken);
            string extension = ExtensionForContentType(audio.ContentType);
            string segmentPath = Path.Combine(segmentRoot, $"segment-{index + 1:D4}{extension}");
            await File.WriteAllBytesAsync(segmentPath, audio.Bytes, cancellationToken);
            segmentPaths.Add(segmentPath);
            providerRefs.Add(audio.ProviderReferenceHash);
        }

        string outputPath = Path.Combine(outputDirectory, "origin-dossier-audiobook.m4b");
        await AssembleM4bAsync(segmentPaths, outputPath, cancellationToken);
        double? observedDuration = await ProbeDurationAsync(outputPath, cancellationToken);
        string executionHash = Sha256Text(string.Join("|", providerRefs));
        var privateReceipt = new
        {
            contractVersion = OriginDossierMediaDispatchContract.Version,
            requestId = request.RequestId,
            provider = "Unmixr AI",
            providerAccountKeyCount = _apiKeys.Count,
            segmentCount = segmentPaths.Count,
            segmentSha256 = segmentPaths.Select(Sha256File).ToArray(),
            providerExecutionRefHash = executionHash,
            outputSha256 = Sha256File(outputPath),
            observedDurationSeconds = observedDuration,
            generatedAtUtc = DateTimeOffset.UtcNow
        };
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "audiobook.provider.private.json"),
            JsonSerializer.Serialize(privateReceipt, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            }) + Environment.NewLine,
            cancellationToken);

        return new OriginDossierMediaRenderResult(
            ProviderClass: "approved_tts",
            OutputPath: outputPath,
            OutputContentType: "audio/mp4",
            ObservedDurationSeconds: observedDuration,
            ProviderExecutionRefHash: executionHash);
    }

    private async Task<UnmixrAudioResponse> SynthesizeAsync(
        string text,
        string voiceId,
        string locale,
        int segmentIndex,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (int attempt = 0; attempt < _apiKeys.Count; attempt++)
        {
            string apiKey = _apiKeys[(segmentIndex + attempt) % _apiKeys.Count];
            try
            {
                using HttpRequestMessage message = new(
                    HttpMethod.Post,
                    $"{_apiBaseUrl}/short-tts/");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                message.Content = JsonContent.Create(new
                {
                    text,
                    voice_id = voiceId,
                    language = NormalizeLocale(locale),
                    response_type = "url",
                    speaking_rate = ResolveSpeechSetting(
                        "CHUMMER_MEDIA_FACTORY_UNMIXR_SPEAKING_RATE",
                        "UNMIXR_SPEAKING_RATE",
                        "medium"),
                    speaking_pitch = ResolveSpeechSetting(
                        "CHUMMER_MEDIA_FACTORY_UNMIXR_SPEAKING_PITCH",
                        "UNMIXR_SPEAKING_PITCH",
                        "low"),
                    speaking_volume = ResolveSpeechSetting(
                        "CHUMMER_MEDIA_FACTORY_UNMIXR_SPEAKING_VOLUME",
                        "UNMIXR_SPEAKING_VOLUME",
                        "medium")
                });
                using HttpResponseMessage response = await _http.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    lastError = new InvalidOperationException(
                        $"origin_dossier_media_unmixr_http_{(int)response.StatusCode}");
                    if (response.StatusCode is HttpStatusCode.Unauthorized
                        or HttpStatusCode.Forbidden
                        or HttpStatusCode.PaymentRequired
                        or HttpStatusCode.TooManyRequests)
                    {
                        continue;
                    }

                    throw lastError;
                }

                using JsonDocument payload = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken));
                string audioUrl = payload.RootElement.TryGetProperty("audio_url", out JsonElement value)
                    ? value.GetString()?.Trim() ?? string.Empty
                    : string.Empty;
                if (!IsSafeProviderAudioUrl(audioUrl))
                {
                    throw new InvalidOperationException("origin_dossier_media_unmixr_audio_url_invalid");
                }

                using HttpResponseMessage audioResponse = await _http.GetAsync(
                    audioUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                audioResponse.EnsureSuccessStatusCode();
                byte[] bytes = await audioResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                if (bytes.Length is <= 128 or > 32 * 1024 * 1024)
                {
                    throw new InvalidOperationException("origin_dossier_media_unmixr_audio_invalid");
                }

                string contentType = audioResponse.Content.Headers.ContentType?.MediaType ?? "audio/mpeg";
                return new UnmixrAudioResponse(
                    Bytes: bytes,
                    ContentType: contentType,
                    ProviderReferenceHash: Sha256Text(audioUrl));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastError = exception;
            }
        }

        throw new InvalidOperationException(
            "origin_dossier_media_unmixr_accounts_unavailable",
            lastError);
    }

    private static IReadOnlyList<string> LoadApiKeys()
    {
        var values = new List<string>();
        Add(Environment.GetEnvironmentVariable("CHUMMER_MEDIA_FACTORY_UNMIXR_API_KEY"));
        Add(Environment.GetEnvironmentVariable("UNMIXR_API_KEY"));
        foreach (System.Collections.DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            string name = variable.Key?.ToString() ?? string.Empty;
            if (name.StartsWith("UNMIXR_API_KEY_FALLBACK_", StringComparison.Ordinal)
                || name.StartsWith("CHUMMER_MEDIA_FACTORY_UNMIXR_API_KEY_FALLBACK_", StringComparison.Ordinal))
            {
                Add(variable.Value?.ToString());
            }
        }

        foreach (string value in (Environment.GetEnvironmentVariable("UNMIXR_API_KEYS") ?? string.Empty)
                     .Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            Add(value);
        }

        return values;

        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value.Trim(), StringComparer.Ordinal))
            {
                values.Add(value.Trim());
            }
        }
    }

    private static IReadOnlyDictionary<string, string> LoadVoiceMap()
    {
        string value = Environment.GetEnvironmentVariable(
            "CHUMMER_MEDIA_FACTORY_UNMIXR_VOICE_MAP_JSON") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            Dictionary<string, string>? parsed =
                JsonSerializer.Deserialize<Dictionary<string, string>>(value);
            return (parsed ?? new Dictionary<string, string>())
                .Where(static item => !string.IsNullOrWhiteSpace(item.Key)
                    && Guid.TryParse(item.Value, out _))
                .ToDictionary(
                    static item => item.Key.Trim(),
                    static item => item.Value.Trim(),
                    StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "origin_dossier_media_unmixr_voice_map_invalid");
        }
    }

    internal string ResolveProviderVoiceId(string selectionId)
    {
        string normalized = selectionId.Trim();
        if (!_voiceMap.TryGetValue(normalized, out string? voiceId)
            || !Guid.TryParse(voiceId, out _))
        {
            throw new InvalidOperationException(
                "origin_dossier_media_unmixr_voice_selection_unmapped");
        }

        return voiceId;
    }

    private static string NormalizeManuscript(string value)
    {
        string withoutFrontMatter = Regex.Replace(
            value,
            @"\A\s*---\s*\r?\n.*?\r?\n---\s*\r?\n",
            string.Empty,
            RegexOptions.Singleline);
        string withoutCodeFences = Regex.Replace(
            withoutFrontMatter,
            @"```.*?```",
            string.Empty,
            RegexOptions.Singleline);
        string withoutImages = Regex.Replace(withoutCodeFences, @"!\[[^\]]*\]\([^)]+\)", string.Empty);
        string linksAsLabels = Regex.Replace(withoutImages, @"\[([^\]]+)\]\([^)]+\)", "$1");
        string withoutMarkup = Regex.Replace(linksAsLabels, @"(?m)^\s{0,3}#{1,6}\s*", string.Empty);
        withoutMarkup = Regex.Replace(withoutMarkup, @"[*_`>]", string.Empty);
        withoutMarkup = withoutMarkup.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Regex.Replace(withoutMarkup, @"[ \t]+", " ").Trim();
    }

    private static IReadOnlyList<string> SplitNarration(string text, int maximumCharacters)
    {
        var chunks = new List<string>();
        var current = new StringBuilder();
        string[] paragraphs = Regex.Split(text, @"\n\s*\n");
        foreach (string paragraphSource in paragraphs)
        {
            string paragraph = Regex.Replace(paragraphSource, @"\s+", " ").Trim();
            if (paragraph.Length == 0)
            {
                continue;
            }

            foreach (string part in SplitOversizedParagraph(paragraph, maximumCharacters))
            {
                if (current.Length > 0 && current.Length + part.Length + 2 > maximumCharacters)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append("\n\n");
                }

                current.Append(part);
            }
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }

    private static IEnumerable<string> SplitOversizedParagraph(string paragraph, int maximumCharacters)
    {
        if (paragraph.Length <= maximumCharacters)
        {
            yield return paragraph;
            yield break;
        }

        string[] sentences = Regex.Split(paragraph, @"(?<=[.!?])\s+");
        var current = new StringBuilder();
        foreach (string sentence in sentences)
        {
            if (sentence.Length > maximumCharacters)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                for (int offset = 0; offset < sentence.Length; offset += maximumCharacters)
                {
                    yield return sentence.Substring(offset, Math.Min(maximumCharacters, sentence.Length - offset));
                }

                continue;
            }

            if (current.Length > 0 && current.Length + sentence.Length + 1 > maximumCharacters)
            {
                yield return current.ToString();
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(sentence);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static async Task AssembleM4bAsync(
        IReadOnlyList<string> segmentPaths,
        string outputPath,
        CancellationToken cancellationToken)
    {
        string concatPath = Path.Combine(Path.GetDirectoryName(outputPath)!, "segments.concat.txt");
        await File.WriteAllLinesAsync(
            concatPath,
            segmentPaths.Select(path => $"file '{path.Replace("'", "'\\''", StringComparison.Ordinal)}'"),
            cancellationToken);
        await RunProcessAsync(
            "ffmpeg",
            [
                "-y",
                "-v", "error",
                "-f", "concat",
                "-safe", "0",
                "-i", concatPath,
                "-vn",
                "-c:a", "aac",
                "-b:a", "128k",
                "-movflags", "+faststart",
                outputPath
            ],
            TimeSpan.FromMinutes(30),
            cancellationToken);
        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length <= 1_024)
        {
            throw new InvalidOperationException("origin_dossier_media_m4b_assembly_failed");
        }
    }

    internal static async Task<double?> ProbeDurationAsync(
        string outputPath,
        CancellationToken cancellationToken)
    {
        ProcessResult result = await RunProcessAsync(
            "ffprobe",
            [
                "-v", "error",
                "-show_entries", "format=duration",
                "-of", "default=noprint_wrappers=1:nokey=1",
                outputPath
            ],
            TimeSpan.FromSeconds(30),
            cancellationToken,
            throwOnFailure: false);
        return result.ExitCode == 0
               && double.TryParse(
                   result.StandardOutput.Trim(),
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out double duration)
               && duration > 0
            ? Math.Round(duration, 3)
            : null;
    }

    internal static async Task<ProcessResult> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool throwOnFailure = true)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"origin_dossier_media_{executable}_start_failed");
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        Task<string> standardError = process.StandardError.ReadToEndAsync(timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"origin_dossier_media_{executable}_timeout");
        }

        ProcessResult result = new(
            process.ExitCode,
            await standardOutput,
            await standardError);
        if (throwOnFailure && result.ExitCode != 0)
        {
            throw new InvalidOperationException($"origin_dossier_media_{executable}_failed");
        }

        return result;
    }

    private static string NormalizeLocale(string locale)
    {
        string normalized = string.IsNullOrWhiteSpace(locale)
            ? "en"
            : locale.Trim().Replace('_', '-');
        return normalized.Split('-', 2)[0].ToLowerInvariant();
    }

    private static string ResolveSpeechSetting(
        string primaryEnvironmentName,
        string compatibilityEnvironmentName,
        string fallback)
    {
        string? configured = Environment.GetEnvironmentVariable(primaryEnvironmentName)
            ?? Environment.GetEnvironmentVariable(compatibilityEnvironmentName);
        return string.IsNullOrWhiteSpace(configured)
            ? fallback
            : configured.Trim();
    }

    private static bool IsSafeProviderAudioUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host)
            || uri.IsLoopback)
        {
            return false;
        }

        return !IPAddress.TryParse(uri.Host, out IPAddress? address)
            || !IsPrivateAddress(address);
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || bytes[0] == 127
                || bytes[0] == 169 && bytes[1] == 254
                || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
                || bytes[0] == 192 && bytes[1] == 168;
        }

        return address.Equals(IPAddress.IPv6Loopback)
            || address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal;
    }

    private static string ExtensionForContentType(string contentType)
        => contentType.Trim().ToLowerInvariant() switch
        {
            "audio/wav" or "audio/x-wav" => ".wav",
            "audio/ogg" => ".ogg",
            "audio/flac" => ".flac",
            "audio/mp4" or "audio/aac" => ".m4a",
            _ => ".mp3"
        };

    private static string Sha256Text(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Sha256File(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private sealed record UnmixrAudioResponse(
        byte[] Bytes,
        string ContentType,
        string ProviderReferenceHash);

    internal sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}

public sealed class MagicFitOriginDossierCinematicSceneRenderer : IOriginDossierCinematicSceneRenderer
{
    private readonly string _scriptPath;
    private readonly string _pythonExecutable;

    public MagicFitOriginDossierCinematicSceneRenderer(
        string scriptPath,
        string pythonExecutable = "python3")
    {
        _scriptPath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(scriptPath)
                ? throw new ArgumentException("scriptPath is required.", nameof(scriptPath))
                : scriptPath.Trim());
        _pythonExecutable = string.IsNullOrWhiteSpace(pythonExecutable)
            ? "python3"
            : pythonExecutable.Trim();
    }

    public async Task<OriginDossierMediaRenderResult> RenderAsync(
        OriginDossierMediaDispatchRequest request,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_scriptPath))
        {
            throw new InvalidOperationException("origin_dossier_media_magicfit_adapter_missing");
        }

        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "origin-dossier-selected-scene.mp4");
        string privateStatePath = Path.Combine(outputDirectory, "cinematic.provider.private.json");
        string prompt = BuildPrompt(request);
        UnmixrOriginDossierAudiobookRenderer.ProcessResult result =
            await UnmixrOriginDossierAudiobookRenderer.RunProcessAsync(
                _pythonExecutable,
                [
                    _scriptPath,
                    "--prompt", prompt,
                    "--out", outputPath,
                    "--duration", request.DurationTargetSeconds.ToString(CultureInfo.InvariantCulture),
                    "--aspect-label", "Landscape (16:9)",
                    "--state-json", privateStatePath
                ],
                TimeSpan.FromMinutes(25),
                cancellationToken,
                throwOnFailure: false);
        if (result.ExitCode != 0 || !File.Exists(outputPath))
        {
            throw new InvalidOperationException("origin_dossier_media_magicfit_render_failed");
        }

        double? observedDuration = await UnmixrOriginDossierAudiobookRenderer.ProbeDurationAsync(
            outputPath,
            cancellationToken);
        string providerRefHash = File.Exists(privateStatePath)
            ? Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(privateStatePath, cancellationToken)))
                .ToLowerInvariant()
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RequestId)))
                .ToLowerInvariant();
        return new OriginDossierMediaRenderResult(
            ProviderClass: "preferred_video",
            OutputPath: outputPath,
            OutputContentType: "video/mp4",
            ObservedDurationSeconds: observedDuration,
            ProviderExecutionRefHash: providerRefHash);
    }

    private static string BuildPrompt(OriginDossierMediaDispatchRequest request)
        => string.Join(
            " ",
            new[]
            {
                $"Selected Origin Dossier scene: {request.SelectionLabel}.",
                request.SelectionSummary,
                "Create one coherent photorealistic cinematic moment from this exact selected scene.",
                "Keep the same adult runner visually consistent throughout.",
                "Near-future cyberpunk realism, grounded dramatic lighting, natural camera motion.",
                "No captions, no logos, no watermark, no character redesign, no unrelated montage."
            });
}
