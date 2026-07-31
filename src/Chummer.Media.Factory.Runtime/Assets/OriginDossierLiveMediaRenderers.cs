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
    private const int ProviderMaximumCharactersPerRequest = 2_000;
    private const int SafeMaximumCharactersPerRequest = 1_900;
    private readonly HttpClient _http;
    private readonly string _apiBaseUrl;
    private readonly IReadOnlyList<string> _apiKeys;
    private readonly IReadOnlyDictionary<string, string> _voiceMap;
    private readonly int _maximumCharactersPerRequest;
    private readonly int _maximumParallelRequests;

    public UnmixrOriginDossierAudiobookRenderer(
        HttpClient? httpClient = null,
        string? apiBaseUrl = null,
        IEnumerable<string>? apiKeys = null,
        IReadOnlyDictionary<string, string>? voiceMap = null,
        int maximumCharactersPerRequest = SafeMaximumCharactersPerRequest,
        int maximumParallelRequests = 4)
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
        _maximumCharactersPerRequest = Math.Clamp(
            maximumCharactersPerRequest,
            500,
            Math.Min(SafeMaximumCharactersPerRequest, ProviderMaximumCharactersPerRequest));
        _maximumParallelRequests = Math.Clamp(
            maximumParallelRequests,
            1,
            Math.Max(_apiKeys.Count, 1));
    }

    internal int MaximumCharactersPerRequest => _maximumCharactersPerRequest;

    internal int MaximumParallelRequests => _maximumParallelRequests;

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
        var segmentPaths = new string[chunks.Count];
        var providerRefs = new string[chunks.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, chunks.Count),
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _maximumParallelRequests
            },
            async (index, token) =>
            {
                UnmixrAudioResponse audio = await SynthesizeAsync(
                    chunks[index],
                    voiceId,
                    request.Locale,
                    index,
                    token);
                string extension = ExtensionForContentType(audio.ContentType);
                string segmentPath = Path.Combine(segmentRoot, $"segment-{index + 1:D4}{extension}");
                await File.WriteAllBytesAsync(segmentPath, audio.Bytes, token);
                segmentPaths[index] = segmentPath;
                providerRefs[index] = audio.ProviderReferenceHash;
            });

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
            segmentCount = segmentPaths.Length,
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

    internal async Task<DialogueLineRenderResult> RenderDialogueLineAsync(
        string text,
        string voiceSelectionId,
        string locale,
        string outputDirectory,
        string fileStem,
        int segmentIndex,
        CancellationToken cancellationToken)
    {
        if (_apiKeys.Count == 0)
        {
            throw new InvalidOperationException("origin_dossier_media_unmixr_credentials_missing");
        }

        string normalizedText = Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
        if (normalizedText.Length is <= 0 or > 500)
        {
            throw new InvalidOperationException("origin_dossier_media_dialogue_line_invalid");
        }

        string voiceId = ResolveProviderVoiceId(voiceSelectionId);
        UnmixrAudioResponse audio = await SynthesizeAsync(
            normalizedText,
            voiceId,
            locale,
            segmentIndex,
            cancellationToken);
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(
            outputDirectory,
            fileStem + ExtensionForContentType(audio.ContentType));
        await File.WriteAllBytesAsync(outputPath, audio.Bytes, cancellationToken);
        return new(
            OutputPath: outputPath,
            ProviderReferenceHash: audio.ProviderReferenceHash,
            OutputSha256: Sha256File(outputPath));
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

    internal sealed record DialogueLineRenderResult(
        string OutputPath,
        string ProviderReferenceHash,
        string OutputSha256);

    internal sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}

public sealed class MagicFitOriginDossierCinematicSceneRenderer : IOriginDossierCinematicSceneRenderer
{
    private const string ControlledShotAudioContract =
        "chummer.origin_dossier_controlled_shot_audio.v1";
    private const int ProviderShotSeconds = 6;
    private const int ProviderRenderAttemptLimit = 3;
    private const double ExpectedObservedShotSeconds = 5.0;
    private const double MinimumReusableShotSeconds = 3.0;
    private readonly string _scriptPath;
    private readonly string _pythonExecutable;
    private readonly UnmixrOriginDossierAudiobookRenderer _dialogueRenderer;
    private readonly IReadOnlyList<string> _dialogueVoiceAliases;

    public MagicFitOriginDossierCinematicSceneRenderer(
        string scriptPath,
        UnmixrOriginDossierAudiobookRenderer dialogueRenderer,
        string pythonExecutable = "python3",
        IEnumerable<string>? dialogueVoiceAliases = null)
    {
        _scriptPath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(scriptPath)
                ? throw new ArgumentException("scriptPath is required.", nameof(scriptPath))
                : scriptPath.Trim());
        _pythonExecutable = string.IsNullOrWhiteSpace(pythonExecutable)
            ? "python3"
            : pythonExecutable.Trim();
        _dialogueRenderer = dialogueRenderer
            ?? throw new ArgumentNullException(nameof(dialogueRenderer));
        _dialogueVoiceAliases = (dialogueVoiceAliases ?? LoadDialogueVoiceAliases())
            .Where(static alias => !string.IsNullOrWhiteSpace(alias))
            .Select(static alias => alias.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (_dialogueVoiceAliases.Count < 2)
        {
            throw new InvalidOperationException(
                "origin_dossier_media_dialogue_voice_aliases_invalid");
        }
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
        int renderTargetSeconds = Math.Max(
            request.DurationTargetSeconds,
            OriginDossierMediaDispatchContract.MinimumCinematicDurationSeconds + ProviderShotSeconds);
        int estimatedShotCount = (int)Math.Ceiling(renderTargetSeconds / ExpectedObservedShotSeconds);
        int maximumShotCount = (int)Math.Ceiling(renderTargetSeconds / MinimumReusableShotSeconds) + 1;
        OriginDossierScreenplayPlan plan = ValidateScreenplayPlan(
            request,
            estimatedShotCount);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "screenplay.plan.private.json"),
            JsonSerializer.Serialize(
                plan,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true })
            + Environment.NewLine,
            cancellationToken);
        string segmentRoot = Path.Combine(outputDirectory, "chapter-segments");
        Directory.CreateDirectory(segmentRoot);
        var segmentPaths = new List<string>(estimatedShotCount);
        var segmentStateHashes = new List<string>(estimatedShotCount);
        var observedSegmentDurations = new List<double>(estimatedShotCount);
        double cumulativeObservedDuration = 0;
        int nextShotIndex = 0;

        while (ShouldRenderNextShot(
                   nextShotIndex,
                   maximumShotCount,
                   plan.PlannedShotCount,
                   cumulativeObservedDuration,
                   renderTargetSeconds))
        {
            ContinuityReferenceFrame? continuityFrame = nextShotIndex == 0
                ? null
                : await MaterializeContinuityReferenceFrameAsync(
                    segmentPaths[^1],
                    segmentRoot,
                    nextShotIndex,
                    cancellationToken);
            ChapterMovieSegmentResult? reusable = await TryReuseChapterSegmentAsync(
                request,
                plan,
                segmentRoot,
                nextShotIndex,
                plan.PlannedShotCount,
                continuityFrame,
                cancellationToken);
            if (reusable is null)
            {
                break;
            }

            AddSegment(reusable);
            nextShotIndex++;
        }

        while (ShouldRenderNextShot(
                   nextShotIndex,
                   maximumShotCount,
                   plan.PlannedShotCount,
                   cumulativeObservedDuration,
                   renderTargetSeconds))
        {
            ContinuityReferenceFrame? continuityFrame = nextShotIndex == 0
                ? null
                : await MaterializeContinuityReferenceFrameAsync(
                    segmentPaths[^1],
                    segmentRoot,
                    nextShotIndex,
                    cancellationToken);
            ChapterMovieSegmentResult rendered = await RenderChapterSegmentAsync(
                request,
                plan,
                segmentRoot,
                nextShotIndex,
                plan.PlannedShotCount,
                continuityFrame,
                cancellationToken);
            AddSegment(rendered);
            nextShotIndex++;
        }

        void AddSegment(ChapterMovieSegmentResult segment)
        {
            segmentPaths.Add(segment.Path);
            observedSegmentDurations.Add(segment.ObservedDurationSeconds);
            cumulativeObservedDuration += segment.ObservedDurationSeconds;
            segmentStateHashes.Add(segment.ProviderStateHash);
        }

        if (cumulativeObservedDuration < OriginDossierMediaDispatchContract.MinimumCinematicDurationSeconds)
        {
            throw new InvalidOperationException("origin_dossier_media_cinematic_too_short");
        }

        await AssembleChapterMovieAsync(segmentPaths, outputPath, cancellationToken);
        double? observedDuration = await UnmixrOriginDossierAudiobookRenderer.ProbeDurationAsync(
            outputPath,
            cancellationToken);
        bool audioTrackVerified = await ProbeAudioTrackAsync(outputPath, cancellationToken);
        if (observedDuration is null
            || observedDuration < OriginDossierMediaDispatchContract.MinimumCinematicDurationSeconds)
        {
            throw new InvalidOperationException("origin_dossier_media_cinematic_too_short");
        }

        if (!audioTrackVerified)
        {
            throw new InvalidOperationException("origin_dossier_media_cinematic_dialogue_audio_missing");
        }

        string providerRefHash = Sha256Text(string.Join("|", segmentStateHashes));
        var privateReceipt = new
        {
            contractVersion = OriginDossierMediaDispatchContract.Version,
            requestId = request.RequestId,
            provider = "MagicFit",
            renderScope = OriginDossierMediaDispatchContract.ChapterRenderScope,
            requestedDurationSeconds = request.DurationTargetSeconds,
            observedDurationSeconds = observedDuration,
            shotCount = segmentPaths.Count,
            providerShotSecondsRequested = ProviderShotSeconds,
            observedSegmentDurationSeconds = observedSegmentDurations,
            dialogueTurnCount = plan.DialogueTurns.Count,
            dialogueAudioTrackVerified = audioTrackVerified,
            dialogueAudioMode = "controlled_tts_with_continuous_ambient_bed",
            providerOriginalShotAudioRemoved = true,
            screenplayContractVersion = plan.ContractVersion,
            screenplayPlanSha256 = plan.FingerprintSha256,
            screenplayCast = plan.Cast.Select(character => character.Name).ToArray(),
            screenplayTimeOfDay = plan.TimeOfDay,
            screenplayWeather = plan.Weather,
            screenplayLocation = plan.PrimaryLocation,
            providerSegmentStateSha256 = segmentStateHashes,
            providerExecutionRefHash = providerRefHash,
            outputSha256 = await Sha256FileAsync(outputPath, cancellationToken),
            generatedAtUtc = DateTimeOffset.UtcNow
        };
        await File.WriteAllTextAsync(
            privateStatePath,
            JsonSerializer.Serialize(
                privateReceipt,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true })
            + Environment.NewLine,
            cancellationToken);
        return new OriginDossierMediaRenderResult(
            ProviderClass: "preferred_video",
            OutputPath: outputPath,
            OutputContentType: "video/mp4",
            ObservedDurationSeconds: observedDuration,
            ProviderExecutionRefHash: providerRefHash,
            RenderScope: OriginDossierMediaDispatchContract.ChapterRenderScope,
            DialogueTurnCount: plan.DialogueTurns.Count,
            AudioTrackVerified: audioTrackVerified);
    }

    internal static bool ShouldRenderNextShot(
        int nextShotIndex,
        int maximumShotCount,
        int plannedShotCount,
        double cumulativeObservedDuration,
        int renderTargetSeconds)
        => nextShotIndex < maximumShotCount
            && (nextShotIndex < plannedShotCount
                || cumulativeObservedDuration < renderTargetSeconds);

    internal static bool ShouldRetryProviderRender(
        int attemptNumber,
        int exitCode,
        bool outputExists)
        => attemptNumber > 0
            && attemptNumber < ProviderRenderAttemptLimit
            && (exitCode != 0 || !outputExists);

    private async Task<ChapterMovieSegmentResult?> TryReuseChapterSegmentAsync(
        OriginDossierMediaDispatchRequest request,
        OriginDossierScreenplayPlan plan,
        string segmentRoot,
        int index,
        int plannedShotCount,
        ContinuityReferenceFrame? continuityFrame,
        CancellationToken cancellationToken)
    {
        string segmentPath = Path.Combine(segmentRoot, $"shot-{index + 1:D3}.mp4");
        if (!File.Exists(segmentPath))
        {
            return null;
        }

        double? segmentDuration = await UnmixrOriginDossierAudiobookRenderer.ProbeDurationAsync(
            segmentPath,
            cancellationToken);
        if (segmentDuration is null
            || segmentDuration < MinimumReusableShotSeconds
            || !await ProbeAudioTrackAsync(segmentPath, cancellationToken))
        {
            return null;
        }

        string segmentStatePath = Path.Combine(
            segmentRoot,
            $"shot-{index + 1:D3}.provider.private.json");
        string screenplayStatePath = Path.Combine(
            segmentRoot,
            $"shot-{index + 1:D3}.screenplay.private.json");
        string expectedPrompt = OriginDossierScreenplayPromptBuilder.BuildShotPrompt(
            request,
            plan,
            index,
            plannedShotCount);
        if (!TryValidateScreenplaySegmentState(
                screenplayStatePath,
                plan.FingerprintSha256,
                Sha256Text(expectedPrompt),
                continuityFrame?.Sha256)
            || !TryValidateProviderContinuityState(
                segmentStatePath,
                continuityFrame?.Sha256))
        {
            return null;
        }

        ControlledShotAudioResult controlledAudio =
            await EnsureControlledShotAudioAsync(
                request,
                plan,
                segmentRoot,
                index,
                segmentPath,
                segmentDuration.Value,
                cancellationToken);

        return new(
            Index: index,
            Path: segmentPath,
            ObservedDurationSeconds: segmentDuration.Value,
            ProviderStateHash: Sha256Text(string.Join(
                "|",
                File.Exists(segmentStatePath)
                    ? await Sha256FileAsync(segmentStatePath, cancellationToken)
                    : Sha256Text($"{request.RequestId}|{index + 1}"),
                controlledAudio.StateSha256,
                await Sha256FileAsync(screenplayStatePath, cancellationToken))));
    }

    private async Task<ChapterMovieSegmentResult> RenderChapterSegmentAsync(
        OriginDossierMediaDispatchRequest request,
        OriginDossierScreenplayPlan plan,
        string segmentRoot,
        int index,
        int plannedShotCount,
        ContinuityReferenceFrame? continuityFrame,
        CancellationToken cancellationToken)
    {
        string segmentPath = Path.Combine(segmentRoot, $"shot-{index + 1:D3}.mp4");
        string segmentStatePath = Path.Combine(
            segmentRoot,
            $"shot-{index + 1:D3}.provider.private.json");
        string screenplayStatePath = Path.Combine(
            segmentRoot,
            $"shot-{index + 1:D3}.screenplay.private.json");
        string prompt = OriginDossierScreenplayPromptBuilder.BuildShotPrompt(
            request,
            plan,
            index,
            plannedShotCount);
        var arguments = new List<string>
        {
            _scriptPath,
            "--prompt", prompt,
            "--out", segmentPath,
            "--duration", ProviderShotSeconds.ToString(CultureInfo.InvariantCulture),
            "--aspect-label", "Landscape (16:9)",
            "--state-json", segmentStatePath
        };
        if (continuityFrame is not null)
        {
            arguments.Add("--first-frame");
            arguments.Add(continuityFrame.Path);
        }

        UnmixrOriginDossierAudiobookRenderer.ProcessResult result = new(
            ExitCode: -1,
            StandardOutput: string.Empty,
            StandardError: string.Empty);
        for (int attemptNumber = 1;
             attemptNumber <= ProviderRenderAttemptLimit;
             attemptNumber++)
        {
            File.Delete(segmentPath);
            File.Delete(segmentStatePath);
            result = await UnmixrOriginDossierAudiobookRenderer.RunProcessAsync(
                _pythonExecutable,
                arguments,
                TimeSpan.FromMinutes(30),
                cancellationToken,
                throwOnFailure: false);
            bool outputExists = File.Exists(segmentPath);
            if (result.ExitCode == 0 && outputExists)
            {
                break;
            }

            string attemptFailurePath = Path.Combine(
                segmentRoot,
                $"shot-{index + 1:D3}.provider-attempt-{attemptNumber:D2}.failed.private.json");
            await File.WriteAllTextAsync(
                attemptFailurePath,
                JsonSerializer.Serialize(
                    new
                    {
                        contractName = "chummer.origin_dossier_provider_attempt_failure.v1",
                        requestId = request.RequestId,
                        shotNumber = index + 1,
                        attemptNumber,
                        attemptLimit = ProviderRenderAttemptLimit,
                        exitCode = result.ExitCode,
                        outputExists,
                        standardOutputSha256 = Sha256Text(result.StandardOutput),
                        standardErrorSha256 = Sha256Text(result.StandardError),
                        observedAtUtc = DateTimeOffset.UtcNow
                    },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true })
                + Environment.NewLine,
                cancellationToken);
            if (!ShouldRetryProviderRender(attemptNumber, result.ExitCode, outputExists))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(attemptNumber * 5), cancellationToken);
        }
        if (result.ExitCode != 0 || !File.Exists(segmentPath))
        {
            throw new InvalidOperationException("origin_dossier_media_magicfit_render_failed");
        }

        double? segmentDuration = await UnmixrOriginDossierAudiobookRenderer.ProbeDurationAsync(
            segmentPath,
            cancellationToken);
        if (segmentDuration is null || segmentDuration < MinimumReusableShotSeconds)
        {
            throw new InvalidOperationException("origin_dossier_media_magicfit_segment_too_short");
        }

        if (!await ProbeAudioTrackAsync(segmentPath, cancellationToken))
        {
            throw new InvalidOperationException("origin_dossier_media_cinematic_dialogue_audio_missing");
        }

        if (!TryValidateProviderContinuityState(
                segmentStatePath,
                continuityFrame?.Sha256))
        {
            throw new InvalidOperationException(
                "origin_dossier_media_magicfit_continuity_reference_unverified");
        }

        ControlledShotAudioResult controlledAudio =
            await EnsureControlledShotAudioAsync(
                request,
                plan,
                segmentRoot,
                index,
                segmentPath,
                segmentDuration.Value,
                cancellationToken);

        await File.WriteAllTextAsync(
            screenplayStatePath,
            JsonSerializer.Serialize(
                new
                {
                    contractVersion = OriginDossierScreenplayContract.Version,
                    requestId = request.RequestId,
                    shotIndex = index,
                    shotNumber = index + 1,
                    plannedShotCount,
                    screenplayPlanSha256 = plan.FingerprintSha256,
                    promptSha256 = Sha256Text(prompt),
                    continuityReferenceApplied = continuityFrame is not null,
                    continuityReferenceSha256 = continuityFrame?.Sha256,
                    controlledAudioApplied = true,
                    dialogueAudioRequired = controlledAudio.DialogueRequired,
                    dialogueLineSha256 = controlledAudio.DialogueLineSha256,
                    timeOfDay = plan.TimeOfDay,
                    weather = plan.Weather,
                    location = plan.PrimaryLocation,
                    cast = plan.Cast.Select(character => character.Name).ToArray()
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true })
            + Environment.NewLine,
            cancellationToken);
        string providerStateHash = File.Exists(segmentStatePath)
            ? await Sha256FileAsync(segmentStatePath, cancellationToken)
            : Sha256Text($"{request.RequestId}|{index + 1}");
        return new(
            Index: index,
            Path: segmentPath,
            ObservedDurationSeconds: segmentDuration.Value,
            ProviderStateHash: Sha256Text(string.Join(
                "|",
                providerStateHash,
                controlledAudio.StateSha256,
                await Sha256FileAsync(screenplayStatePath, cancellationToken))));
    }

    private async Task<ControlledShotAudioResult> EnsureControlledShotAudioAsync(
        OriginDossierMediaDispatchRequest request,
        OriginDossierScreenplayPlan plan,
        string segmentRoot,
        int index,
        string segmentPath,
        double segmentDurationSeconds,
        CancellationToken cancellationToken)
    {
        int? dialogueIndex = OriginDossierScreenplayPromptBuilder.ResolveDialogueIndex(
            index,
            plan.PlannedShotCount,
            plan.DialogueTurns.Count);
        OriginDossierScreenplayDialogueTurn? dialogue = dialogueIndex is null
            ? null
            : plan.DialogueTurns[dialogueIndex.Value];
        string? dialogueLineSha256 = dialogue is null
            ? null
            : Sha256Text(dialogue.Line.Trim());
        string statePath = Path.Combine(
            segmentRoot,
            $"shot-{index + 1:D3}.controlled-audio.private.json");
        ControlledShotAudioResult? reusable = await TryReuseControlledShotAudioAsync(
            statePath,
            segmentPath,
            request.RequestId,
            index,
            dialogue,
            dialogueLineSha256,
            cancellationToken);
        if (reusable is not null)
        {
            return reusable;
        }

        string? voiceAlias = null;
        UnmixrOriginDossierAudiobookRenderer.DialogueLineRenderResult? dialogueAudio = null;
        if (dialogue is not null)
        {
            voiceAlias = ResolveDialogueVoiceAlias(plan, dialogue.Speaker);
            dialogueAudio = await _dialogueRenderer.RenderDialogueLineAsync(
                dialogue.Line,
                voiceAlias,
                request.Locale,
                segmentRoot,
                $"shot-{index + 1:D3}.dialogue",
                index,
                cancellationToken);
        }

        string controlledPath = Path.Combine(
            segmentRoot,
            $"shot-{index + 1:D3}.controlled.tmp.mp4");
        var arguments = new List<string>
        {
            "-y",
            "-v", "error",
            "-i", segmentPath,
            "-f", "lavfi",
            "-i", "anoisesrc=color=pink:sample_rate=48000:amplitude=0.035"
        };
        if (dialogueAudio is not null)
        {
            arguments.AddRange(
            [
                "-i", dialogueAudio.OutputPath,
                "-filter_complex",
                "[1:a]lowpass=f=3500,volume=0.45[ambient];"
                + "[2:a]adelay=650:all=1,volume=1.30[voice];"
                + "[ambient][voice]amix=inputs=2:duration=first:dropout_transition=0[aout]"
            ]);
        }
        else
        {
            arguments.AddRange(
            [
                "-filter_complex",
                "[1:a]lowpass=f=3500,volume=0.45[aout]"
            ]);
        }

        arguments.AddRange(
        [
            "-map", "0:v:0",
            "-map", "[aout]",
            "-c:v", "copy",
            "-c:a", "aac",
            "-b:a", "192k",
            "-t", segmentDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            "-movflags", "+faststart",
            controlledPath
        ]);
        UnmixrOriginDossierAudiobookRenderer.ProcessResult ffmpegResult =
            await UnmixrOriginDossierAudiobookRenderer.RunProcessAsync(
                "ffmpeg",
                arguments,
                TimeSpan.FromMinutes(5),
                cancellationToken,
                throwOnFailure: false);
        if (ffmpegResult.ExitCode != 0
            || !File.Exists(controlledPath)
            || new FileInfo(controlledPath).Length <= 1_024
            || !await ProbeAudioTrackAsync(controlledPath, cancellationToken))
        {
            throw new InvalidOperationException(
                "origin_dossier_media_controlled_shot_audio_failed");
        }

        File.Move(controlledPath, segmentPath, overwrite: true);
        string outputSha256 = await Sha256FileAsync(segmentPath, cancellationToken);
        var state = new
        {
            contractVersion = ControlledShotAudioContract,
            requestId = request.RequestId,
            shotIndex = index,
            shotNumber = index + 1,
            controlledAudioApplied = true,
            providerOriginalAudioRemoved = true,
            ambientBed = "continuous_rain_room_tone",
            dialogueRequired = dialogue is not null,
            dialogueSpeaker = dialogue?.Speaker,
            dialogueListener = dialogue?.Listener,
            dialogueLineSha256,
            dialogueVoiceAlias = voiceAlias,
            dialogueProviderReferenceHash = dialogueAudio?.ProviderReferenceHash,
            dialogueAudioSha256 = dialogueAudio?.OutputSha256,
            outputSha256,
            generatedAtUtc = DateTimeOffset.UtcNow
        };
        await File.WriteAllTextAsync(
            statePath,
            JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true })
            + Environment.NewLine,
            cancellationToken);
        return new(
            DialogueRequired: dialogue is not null,
            DialogueLineSha256: dialogueLineSha256,
            StateSha256: await Sha256FileAsync(statePath, cancellationToken));
    }

    private static async Task<ControlledShotAudioResult?> TryReuseControlledShotAudioAsync(
        string statePath,
        string segmentPath,
        string requestId,
        int index,
        OriginDossierScreenplayDialogueTurn? dialogue,
        string? dialogueLineSha256,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(statePath)
            || !File.Exists(segmentPath)
            || !await ProbeAudioTrackAsync(segmentPath, cancellationToken))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                await File.ReadAllTextAsync(statePath, cancellationToken));
            JsonElement root = document.RootElement;
            string outputSha256 = await Sha256FileAsync(segmentPath, cancellationToken);
            bool expectedDialogue = dialogue is not null;
            bool matches = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("contractVersion", out JsonElement contractVersion)
                && string.Equals(
                    contractVersion.GetString(),
                    ControlledShotAudioContract,
                    StringComparison.Ordinal)
                && root.TryGetProperty("requestId", out JsonElement stateRequestId)
                && string.Equals(stateRequestId.GetString(), requestId, StringComparison.Ordinal)
                && root.TryGetProperty("shotIndex", out JsonElement shotIndex)
                && shotIndex.TryGetInt32(out int stateIndex)
                && stateIndex == index
                && root.TryGetProperty("controlledAudioApplied", out JsonElement applied)
                && applied.ValueKind == JsonValueKind.True
                && root.TryGetProperty("providerOriginalAudioRemoved", out JsonElement removed)
                && removed.ValueKind == JsonValueKind.True
                && root.TryGetProperty("dialogueRequired", out JsonElement required)
                && required.ValueKind is JsonValueKind.True or JsonValueKind.False
                && required.GetBoolean() == expectedDialogue
                && root.TryGetProperty("outputSha256", out JsonElement stateOutputSha256)
                && string.Equals(
                    stateOutputSha256.GetString(),
                    outputSha256,
                    StringComparison.OrdinalIgnoreCase);
            if (!matches)
            {
                return null;
            }

            if (expectedDialogue
                && (!root.TryGetProperty(
                        "dialogueLineSha256",
                        out JsonElement stateDialogueLineSha256)
                    || !string.Equals(
                        stateDialogueLineSha256.GetString(),
                        dialogueLineSha256,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            return new(
                DialogueRequired: expectedDialogue,
                DialogueLineSha256: dialogueLineSha256,
                StateSha256: await Sha256FileAsync(statePath, cancellationToken));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private string ResolveDialogueVoiceAlias(
        OriginDossierScreenplayPlan plan,
        string speaker)
    {
        int castIndex = plan.Cast
            .Select((character, index) => new { character.Name, Index = index })
            .Where(item => string.Equals(
                item.Name,
                speaker,
                StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Index)
            .DefaultIfEmpty(-1)
            .First();
        if (castIndex < 0)
        {
            throw new InvalidOperationException(
                "origin_dossier_media_dialogue_speaker_unknown");
        }

        return _dialogueVoiceAliases[castIndex % _dialogueVoiceAliases.Count];
    }

    private static IReadOnlyList<string> LoadDialogueVoiceAliases()
    {
        string configured = Environment.GetEnvironmentVariable(
            "CHUMMER_MEDIA_FACTORY_ORIGIN_DIALOGUE_VOICE_ALIASES")
            ?? "voice-noir,voice-wire";
        return configured.Split(
            [',', ';', ' ', '\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TryValidateScreenplaySegmentState(
        string statePath,
        string expectedPlanSha256,
        string expectedPromptSha256,
        string? expectedContinuityReferenceSha256)
    {
        if (!File.Exists(statePath))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(statePath));
            JsonElement root = document.RootElement;
            bool baseStateMatches = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("contractVersion", out JsonElement contractVersion)
                && string.Equals(
                    contractVersion.GetString(),
                    OriginDossierScreenplayContract.Version,
                    StringComparison.Ordinal)
                && root.TryGetProperty("screenplayPlanSha256", out JsonElement planSha256)
                && string.Equals(
                    planSha256.GetString(),
                    expectedPlanSha256,
                    StringComparison.OrdinalIgnoreCase)
                && root.TryGetProperty("promptSha256", out JsonElement promptSha256)
                && string.Equals(
                    promptSha256.GetString(),
                    expectedPromptSha256,
                    StringComparison.OrdinalIgnoreCase);
            if (!baseStateMatches)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(expectedContinuityReferenceSha256))
            {
                return !root.TryGetProperty(
                        "continuityReferenceSha256",
                        out JsonElement unusedReference)
                    || unusedReference.ValueKind == JsonValueKind.Null
                    || string.IsNullOrWhiteSpace(unusedReference.GetString());
            }

            return root.TryGetProperty(
                    "continuityReferenceSha256",
                    out JsonElement continuityReferenceSha256)
                && string.Equals(
                    continuityReferenceSha256.GetString(),
                    expectedContinuityReferenceSha256,
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryValidateProviderContinuityState(
        string statePath,
        string? expectedContinuityReferenceSha256)
    {
        if (!File.Exists(statePath))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(statePath));
            JsonElement root = document.RootElement;
            bool expectedApplied = !string.IsNullOrWhiteSpace(
                expectedContinuityReferenceSha256);
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("firstFrameApplied", out JsonElement applied)
                || applied.ValueKind is not JsonValueKind.True and not JsonValueKind.False
                || applied.GetBoolean() != expectedApplied)
            {
                return false;
            }

            if (!expectedApplied)
            {
                return !root.TryGetProperty("firstFrameSha256", out JsonElement unusedHash)
                    || unusedHash.ValueKind == JsonValueKind.Null
                    || string.IsNullOrWhiteSpace(unusedHash.GetString());
            }

            return root.TryGetProperty("firstFrameSha256", out JsonElement firstFrameSha256)
                && string.Equals(
                    firstFrameSha256.GetString(),
                    expectedContinuityReferenceSha256,
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task<ContinuityReferenceFrame> MaterializeContinuityReferenceFrameAsync(
        string previousSegmentPath,
        string segmentRoot,
        int nextShotIndex,
        CancellationToken cancellationToken)
    {
        string framePath = Path.Combine(
            segmentRoot,
            $"shot-{nextShotIndex + 1:D3}.continuity-first-frame.png");
        UnmixrOriginDossierAudiobookRenderer.ProcessResult result =
            await UnmixrOriginDossierAudiobookRenderer.RunProcessAsync(
                "ffmpeg",
                [
                    "-y",
                    "-v", "error",
                    "-sseof", "-0.15",
                    "-i", previousSegmentPath,
                    "-frames:v", "1",
                    "-vf", "format=rgb24",
                    framePath
                ],
                TimeSpan.FromMinutes(2),
                cancellationToken,
                throwOnFailure: false);
        if (result.ExitCode != 0
            || !File.Exists(framePath)
            || new FileInfo(framePath).Length <= 1_024)
        {
            throw new InvalidOperationException(
                "origin_dossier_media_continuity_reference_frame_failed");
        }

        return new(
            framePath,
            await Sha256FileAsync(framePath, cancellationToken));
    }

    private static OriginDossierScreenplayPlan ValidateScreenplayPlan(
        OriginDossierMediaDispatchRequest request,
        int plannedShotCount)
    {
        if (!string.Equals(
                request.RenderScope,
                OriginDossierMediaDispatchContract.ChapterRenderScope,
                StringComparison.Ordinal)
            || !request.DialogueRequired
            || request.MinimumDialogueTurns < OriginDossierMediaDispatchContract.MinimumCinematicDialogueTurns)
        {
            throw new InvalidOperationException("origin_dossier_media_chapter_dialogue_contract_invalid");
        }

        OriginDossierScreenplayPlan plan = request.Screenplay
            ?? throw new InvalidOperationException("origin_dossier_media_screenplay_missing");
        bool valid = string.Equals(
                request.ContractVersion,
                OriginDossierMediaDispatchContract.Version,
                StringComparison.Ordinal)
            && string.Equals(
                plan.ContractVersion,
                OriginDossierScreenplayContract.Version,
                StringComparison.Ordinal)
            && string.Equals(
                plan.RenderScope,
                OriginDossierMediaDispatchContract.ChapterRenderScope,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(plan.Title)
            && !string.IsNullOrWhiteSpace(plan.TimeOfDay)
            && !string.IsNullOrWhiteSpace(plan.Weather)
            && !string.IsNullOrWhiteSpace(plan.PrimaryLocation)
            && !string.IsNullOrWhiteSpace(plan.WardrobeContinuity)
            && !string.IsNullOrWhiteSpace(plan.ScreenDirectionContinuity)
            && plan.Cast is { Count: >= 2 and <= 8 }
            && plan.Cast.All(character =>
                !string.IsNullOrWhiteSpace(character.Name)
                && !string.IsNullOrWhiteSpace(character.Role)
                && !string.IsNullOrWhiteSpace(character.VisualAnchor))
            && plan.Cast.Select(character => character.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == plan.Cast.Count
            && plan.RenderBeats is { Count: >= 1 and <= 128 }
            && plan.RenderBeats.All(beat =>
                !string.IsNullOrWhiteSpace(beat)
                && beat.Length <= OriginDossierScreenplayContract.MaximumNarrativeBeatCharacters + 1)
            && plan.DialogueTurns.Count >= request.MinimumDialogueTurns
            && plan.DialogueTurns.Count <= Math.Max(plan.PlannedShotCount - 2, 0)
            && plan.DialogueTurns.All(turn =>
                !string.IsNullOrWhiteSpace(turn.Speaker)
                && !string.IsNullOrWhiteSpace(turn.Listener)
                && !string.IsNullOrWhiteSpace(turn.Line)
                && OriginDossierScreenplayContract.IsDialogueTurnRenderable(turn.Line)
                && plan.Cast.Any(character =>
                    string.Equals(character.Name, turn.Speaker, StringComparison.OrdinalIgnoreCase))
                && plan.Cast.Any(character =>
                    string.Equals(character.Name, turn.Listener, StringComparison.OrdinalIgnoreCase))
                && !string.Equals(turn.Speaker, turn.Listener, StringComparison.OrdinalIgnoreCase))
            && plan.UsesSupportingCanonDialogue
                == plan.DialogueTurns.Any(turn => turn.UsesSupportingCanonDialogue)
            && plan.PlannedShotCount == plannedShotCount
            && OriginDossierScreenplayContract.FingerprintMatches(request, plan);
        if (!valid)
        {
            throw new InvalidOperationException("origin_dossier_media_screenplay_invalid");
        }

        return plan;
    }

    private static async Task AssembleChapterMovieAsync(
        IReadOnlyList<string> segmentPaths,
        string outputPath,
        CancellationToken cancellationToken)
    {
        string concatPath = Path.Combine(Path.GetDirectoryName(outputPath)!, "chapter-segments.concat.txt");
        await File.WriteAllLinesAsync(
            concatPath,
            segmentPaths.Select(path => $"file '{path.Replace("'", "'\\''", StringComparison.Ordinal)}'"),
            cancellationToken);
        await UnmixrOriginDossierAudiobookRenderer.RunProcessAsync(
            "ffmpeg",
            [
                "-y",
                "-v", "error",
                "-f", "concat",
                "-safe", "0",
                "-i", concatPath,
                "-c:v", "libx264",
                "-preset", "medium",
                "-crf", "18",
                "-c:a", "aac",
                "-b:a", "192k",
                "-movflags", "+faststart",
                outputPath
            ],
            TimeSpan.FromMinutes(60),
            cancellationToken);
        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length <= 1_024)
        {
            throw new InvalidOperationException("origin_dossier_media_chapter_movie_assembly_failed");
        }
    }

    private static async Task<bool> ProbeAudioTrackAsync(
        string outputPath,
        CancellationToken cancellationToken)
    {
        UnmixrOriginDossierAudiobookRenderer.ProcessResult result =
            await UnmixrOriginDossierAudiobookRenderer.RunProcessAsync(
                "ffprobe",
                [
                    "-v", "error",
                    "-select_streams", "a:0",
                    "-show_entries", "stream=codec_type",
                    "-of", "default=noprint_wrappers=1:nokey=1",
                    outputPath
                ],
                TimeSpan.FromSeconds(30),
                cancellationToken,
                throwOnFailure: false);
        return result.ExitCode == 0
            && result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(value => string.Equals(value, "audio", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> Sha256FileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Sha256Text(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record ContinuityReferenceFrame(
        string Path,
        string Sha256);

    private sealed record ControlledShotAudioResult(
        bool DialogueRequired,
        string? DialogueLineSha256,
        string StateSha256);

    private sealed record ChapterMovieSegmentResult(
        int Index,
        string Path,
        double ObservedDurationSeconds,
        string ProviderStateHash);
}
