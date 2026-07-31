using Chummer.Media.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chummer.Run.AI.Services.Assets;

public interface IOriginDossierAudiobookRenderer
{
    Task<OriginDossierMediaRenderResult> RenderAsync(
        OriginDossierMediaDispatchRequest request,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}

public interface IOriginDossierCinematicSceneRenderer
{
    Task<OriginDossierMediaRenderResult> RenderAsync(
        OriginDossierMediaDispatchRequest request,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}

public sealed record OriginDossierMediaRenderResult(
    string ProviderClass,
    string OutputPath,
    string OutputContentType,
    double? ObservedDurationSeconds,
    string ProviderExecutionRefHash,
    string RenderScope = "",
    int DialogueTurnCount = 0,
    bool AudioTrackVerified = false);

public sealed class OriginDossierMediaInboxProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly string _inboxRoot;
    private readonly string _receiptRoot;
    private readonly string _outputRoot;
    private readonly IReadOnlyList<string> _allowedSourceRoots;
    private readonly IOriginDossierAudiobookRenderer _audiobook;
    private readonly IOriginDossierCinematicSceneRenderer _cinematic;

    public OriginDossierMediaInboxProcessor(
        string inboxRoot,
        string receiptRoot,
        string outputRoot,
        IEnumerable<string> allowedSourceRoots,
        IOriginDossierAudiobookRenderer audiobook,
        IOriginDossierCinematicSceneRenderer cinematic)
    {
        _inboxRoot = RequireRoot(inboxRoot, nameof(inboxRoot));
        _receiptRoot = RequireRoot(receiptRoot, nameof(receiptRoot));
        _outputRoot = RequireRoot(outputRoot, nameof(outputRoot));
        _allowedSourceRoots = (allowedSourceRoots ?? throw new ArgumentNullException(nameof(allowedSourceRoots)))
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => Path.GetFullPath(item.Trim()).TrimEnd(Path.DirectorySeparatorChar))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (_allowedSourceRoots.Count == 0)
        {
            throw new ArgumentException("At least one allowed Origin Dossier source root is required.", nameof(allowedSourceRoots));
        }

        _audiobook = audiobook ?? throw new ArgumentNullException(nameof(audiobook));
        _cinematic = cinematic ?? throw new ArgumentNullException(nameof(cinematic));
    }

    public async Task<IReadOnlyList<OriginDossierMediaDispatchReceipt>> ProcessPendingAsync(
        int maximumRequests = 10,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_inboxRoot);
        Directory.CreateDirectory(_receiptRoot);
        Directory.CreateDirectory(_outputRoot);

        string[] requests = Directory
            .EnumerateFiles(_inboxRoot, "*.request.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Take(Math.Clamp(maximumRequests, 1, 100))
            .ToArray();
        var receipts = new List<OriginDossierMediaDispatchReceipt>(requests.Length);
        foreach (string requestPath in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OriginDossierMediaDispatchReceipt? receipt = await ProcessFileAsync(requestPath, cancellationToken);
            if (receipt is not null)
            {
                receipts.Add(receipt);
            }
        }

        return receipts;
    }

    public async Task<OriginDossierMediaDispatchReceipt?> ProcessFileAsync(
        string requestPath,
        CancellationToken cancellationToken = default)
    {
        requestPath = Path.GetFullPath(requestPath);
        if (!IsUnderRoot(requestPath, _inboxRoot))
        {
            throw new InvalidOperationException("Origin Dossier request path must stay inside the configured inbox.");
        }

        string processingRoot = Path.Combine(_inboxRoot, "processing");
        Directory.CreateDirectory(processingRoot);
        string processingPath = Path.Combine(processingRoot, Path.GetFileName(requestPath));
        try
        {
            File.Move(requestPath, processingPath);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (IOException) when (File.Exists(processingPath))
        {
            return null;
        }

        byte[] requestBytes = await File.ReadAllBytesAsync(processingPath, cancellationToken);
        string requestSha256 = Sha256(requestBytes);
        OriginDossierMediaDispatchRequest? request = null;
        OriginDossierMediaDispatchReceipt receipt;
        try
        {
            request = JsonSerializer.Deserialize<OriginDossierMediaDispatchRequest>(requestBytes, JsonOptions)
                ?? throw new InvalidOperationException("origin_dossier_media_request_missing");
            Validate(request);
            string receiptPath = ReceiptPath(request.RequestId);
            if (File.Exists(receiptPath))
            {
                receipt = JsonSerializer.Deserialize<OriginDossierMediaDispatchReceipt>(
                    await File.ReadAllTextAsync(receiptPath, cancellationToken),
                    JsonOptions)
                    ?? throw new InvalidOperationException("origin_dossier_media_receipt_invalid");
                await ValidateExistingReceiptAsync(
                    receipt,
                    request,
                    cancellationToken);
                MoveProcessed(processingPath, request.RequestId, "completed");
                return receipt;
            }

            string outputDirectory = Path.Combine(
                _outputRoot,
                SafeToken(request.ProjectId),
                SafeToken(request.RequestId));
            Directory.CreateDirectory(outputDirectory);
            OriginDossierMediaRenderResult rendered = request.Kind switch
            {
                OriginDossierMediaDispatchKind.Audiobook => await _audiobook.RenderAsync(
                    request,
                    outputDirectory,
                    cancellationToken),
                OriginDossierMediaDispatchKind.CinematicScene => await _cinematic.RenderAsync(
                    request,
                    outputDirectory,
                    cancellationToken),
                _ => throw new InvalidOperationException("origin_dossier_media_kind_unsupported")
            };
            string outputPath = Path.GetFullPath(rendered.OutputPath);
            if (!IsUnderRoot(outputPath, _outputRoot) || !File.Exists(outputPath))
            {
                throw new InvalidOperationException("origin_dossier_media_output_missing");
            }
            ValidateRenderedResult(request, rendered);

            FileInfo output = new(outputPath);
            receipt = new OriginDossierMediaDispatchReceipt(
                ContractVersion: OriginDossierMediaDispatchContract.Version,
                RequestId: request.RequestId,
                Kind: request.Kind,
                ProjectId: request.ProjectId,
                OwnerRefHash: request.OwnerRefHash,
                OriginRevisionId: request.OriginRevisionId,
                SelectionId: request.SelectionId,
                Status: "succeeded",
                ProviderClass: rendered.ProviderClass,
                OutputPath: outputPath,
                OutputContentType: rendered.OutputContentType,
                OutputSha256: await Sha256FileAsync(outputPath, cancellationToken),
                OutputBytes: output.Length,
                ObservedDurationSeconds: rendered.ObservedDurationSeconds,
                RequestSha256: requestSha256,
                ProviderExecutionRefHash: rendered.ProviderExecutionRefHash,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                RenderScope: rendered.RenderScope,
                DialogueTurnCount: rendered.DialogueTurnCount,
                AudioTrackVerified: rendered.AudioTrackVerified);
            await WriteReceiptAsync(receipt, cancellationToken);
            MoveProcessed(processingPath, request.RequestId, "completed");
            return receipt;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            string requestId = SafeToken(request?.RequestId ?? Path.GetFileNameWithoutExtension(processingPath));
            receipt = new OriginDossierMediaDispatchReceipt(
                ContractVersion: OriginDossierMediaDispatchContract.Version,
                RequestId: requestId,
                Kind: request?.Kind ?? OriginDossierMediaDispatchKind.Audiobook,
                ProjectId: request?.ProjectId ?? string.Empty,
                OwnerRefHash: request?.OwnerRefHash ?? string.Empty,
                OriginRevisionId: request?.OriginRevisionId ?? string.Empty,
                SelectionId: request?.SelectionId ?? string.Empty,
                Status: "failed",
                ProviderClass: string.Empty,
                OutputPath: string.Empty,
                OutputContentType: string.Empty,
                OutputSha256: string.Empty,
                OutputBytes: 0,
                ObservedDurationSeconds: null,
                RequestSha256: requestSha256,
                ProviderExecutionRefHash: string.Empty,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                ErrorCode: SafeErrorCode(exception));
            await WriteReceiptAsync(receipt, cancellationToken);
            MoveProcessed(processingPath, requestId, "failed");
            return receipt;
        }
    }

    private async Task ValidateExistingReceiptAsync(
        OriginDossierMediaDispatchReceipt receipt,
        OriginDossierMediaDispatchRequest request,
        CancellationToken cancellationToken)
    {
        bool requestMatches =
            string.Equals(receipt.ContractVersion, OriginDossierMediaDispatchContract.Version, StringComparison.Ordinal)
            && string.Equals(receipt.RequestId, request.RequestId, StringComparison.Ordinal)
            && receipt.Kind == request.Kind
            && string.Equals(receipt.ProjectId, request.ProjectId, StringComparison.Ordinal)
            && string.Equals(receipt.OwnerRefHash, request.OwnerRefHash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(receipt.OriginRevisionId, request.OriginRevisionId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(receipt.SelectionId, request.SelectionId, StringComparison.Ordinal)
            && receipt.RequestSha256.Length == 64
            && receipt.RequestSha256.All(static character => Uri.IsHexDigit(character));
        if (!requestMatches)
        {
            throw new InvalidOperationException("origin_dossier_media_receipt_request_mismatch");
        }

        if (string.Equals(receipt.Status, "failed", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(receipt.ErrorCode)
                || !receipt.ErrorCode.StartsWith("origin_dossier_media_", StringComparison.Ordinal)
                || receipt.OutputBytes != 0
                || !string.IsNullOrEmpty(receipt.OutputPath)
                || !string.IsNullOrEmpty(receipt.OutputSha256))
            {
                throw new InvalidOperationException("origin_dossier_media_failed_receipt_invalid");
            }

            return;
        }

        string expectedProviderClass = request.Kind == OriginDossierMediaDispatchKind.Audiobook
            ? "approved_tts"
            : "preferred_video";
        string expectedContentType = request.Kind == OriginDossierMediaDispatchKind.Audiobook
            ? "audio/mp4"
            : "video/mp4";
        string outputPath = string.IsNullOrWhiteSpace(receipt.OutputPath)
            ? string.Empty
            : Path.GetFullPath(receipt.OutputPath);
        bool durationValid = receipt.ObservedDurationSeconds is > 0
            && (request.Kind != OriginDossierMediaDispatchKind.CinematicScene
                || receipt.ObservedDurationSeconds >= OriginDossierMediaDispatchContract.MinimumCinematicDurationSeconds);
        bool cinematicEvidenceValid = request.Kind != OriginDossierMediaDispatchKind.CinematicScene
            || string.Equals(
                receipt.RenderScope,
                OriginDossierMediaDispatchContract.ChapterRenderScope,
                StringComparison.Ordinal)
            && receipt.DialogueTurnCount >= request.MinimumDialogueTurns
            && receipt.AudioTrackVerified;
        if (!string.Equals(receipt.Status, "succeeded", StringComparison.Ordinal)
            || !string.Equals(receipt.ProviderClass, expectedProviderClass, StringComparison.Ordinal)
            || !string.Equals(receipt.OutputContentType, expectedContentType, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(outputPath)
            || !IsUnderRoot(outputPath, _outputRoot)
            || !File.Exists(outputPath)
            || receipt.OutputBytes != new FileInfo(outputPath).Length
            || !durationValid
            || !cinematicEvidenceValid
            || string.IsNullOrWhiteSpace(receipt.ProviderExecutionRefHash)
            || !string.IsNullOrEmpty(receipt.ErrorCode))
        {
            throw new InvalidOperationException("origin_dossier_media_succeeded_receipt_invalid");
        }

        string outputSha256 = await Sha256FileAsync(outputPath, cancellationToken);
        if (!string.Equals(receipt.OutputSha256, outputSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("origin_dossier_media_receipt_output_mismatch");
        }
    }

    private void Validate(OriginDossierMediaDispatchRequest request)
    {
        if (!string.Equals(
                request.ContractVersion,
                OriginDossierMediaDispatchContract.Version,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("origin_dossier_media_contract_unsupported");
        }

        RequireText(request.RequestId, nameof(request.RequestId));
        RequireText(request.ProjectId, nameof(request.ProjectId));
        RequireHash(request.OwnerRefHash, nameof(request.OwnerRefHash));
        RequireText(request.ApprovedOriginPacketId, nameof(request.ApprovedOriginPacketId));
        RequireHash(request.OriginRevisionId, nameof(request.OriginRevisionId));
        RequireText(request.Source, nameof(request.Source));
        RequireText(request.SelectionId, nameof(request.SelectionId));
        RequireText(request.SelectionLabel, nameof(request.SelectionLabel));
        RequireText(request.SelectionSummary, nameof(request.SelectionSummary));
        if (!string.Equals(
                request.RequestId,
                OriginDossierMediaDispatchContract.BuildRequestId(
                    request.Kind,
                    request.ProjectId,
                    request.OwnerRefHash,
                    request.SelectionId,
                    request.OriginRevisionId),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("origin_dossier_media_request_id_invalid");
        }
        ValidateSourcePath(request.ManuscriptPath, nameof(request.ManuscriptPath), required: true);
        ValidateSourcePath(request.SourcePacketPath, nameof(request.SourcePacketPath), required: true);
        ValidateSourcePath(request.CoverPath, nameof(request.CoverPath), required: false);
        ValidateSourcePath(request.SequencePlanPath, nameof(request.SequencePlanPath), required: false);
        if (request.Kind == OriginDossierMediaDispatchKind.CinematicScene)
        {
            if (request.DurationTargetSeconds is < OriginDossierMediaDispatchContract.MinimumCinematicDurationSeconds
                or > OriginDossierMediaDispatchContract.MaximumCinematicDurationSeconds)
            {
                throw new InvalidOperationException("origin_dossier_media_duration_invalid");
            }

            if (!string.Equals(
                    request.RenderScope,
                    OriginDossierMediaDispatchContract.ChapterRenderScope,
                    StringComparison.Ordinal)
                || !request.DialogueRequired
                || request.MinimumDialogueTurns < OriginDossierMediaDispatchContract.MinimumCinematicDialogueTurns
                || request.Screenplay is null
                || !string.Equals(
                    request.Screenplay.ContractVersion,
                    OriginDossierScreenplayContract.Version,
                    StringComparison.Ordinal)
                || request.Screenplay.Cast.Count < 2
                || request.Screenplay.DialogueTurns.Count < request.MinimumDialogueTurns
                || request.Screenplay.DialogueTurns.Count
                    > Math.Max(request.Screenplay.PlannedShotCount - 2, 0)
                || request.Screenplay.DialogueTurns.Any(turn =>
                    !OriginDossierScreenplayContract.IsDialogueTurnRenderable(turn.Line))
                || request.Screenplay.RenderBeats.Count == 0
                || request.Screenplay.RenderBeats.Any(beat =>
                    string.IsNullOrWhiteSpace(beat)
                    || beat.Length > OriginDossierScreenplayContract.MaximumNarrativeBeatCharacters + 1)
                || request.Screenplay.UsesSupportingCanonDialogue
                    != request.Screenplay.DialogueTurns.Any(turn => turn.UsesSupportingCanonDialogue)
                || !OriginDossierScreenplayContract.FingerprintMatches(
                    request,
                    request.Screenplay))
            {
                throw new InvalidOperationException("origin_dossier_media_chapter_dialogue_contract_invalid");
            }
        }
        else if (request.DurationTargetSeconds is < 1 or > OriginDossierMediaDispatchContract.MaximumCinematicDurationSeconds)
        {
            throw new InvalidOperationException("origin_dossier_media_duration_invalid");
        }
    }

    private static void ValidateRenderedResult(
        OriginDossierMediaDispatchRequest request,
        OriginDossierMediaRenderResult rendered)
    {
        if (rendered.ObservedDurationSeconds is null or <= 0)
        {
            throw new InvalidOperationException("origin_dossier_media_duration_unverified");
        }

        if (request.Kind != OriginDossierMediaDispatchKind.CinematicScene)
        {
            return;
        }

        if (rendered.ObservedDurationSeconds < OriginDossierMediaDispatchContract.MinimumCinematicDurationSeconds)
        {
            throw new InvalidOperationException("origin_dossier_media_cinematic_too_short");
        }

        if (!string.Equals(
                rendered.RenderScope,
                OriginDossierMediaDispatchContract.ChapterRenderScope,
                StringComparison.Ordinal)
            || rendered.DialogueTurnCount < request.MinimumDialogueTurns
            || !rendered.AudioTrackVerified)
        {
            throw new InvalidOperationException("origin_dossier_media_cinematic_dialogue_unverified");
        }
    }

    private void ValidateSourcePath(string? path, string name, bool required)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (required)
            {
                throw new InvalidOperationException($"origin_dossier_media_{name}_missing");
            }

            return;
        }

        string fullPath = Path.GetFullPath(path.Trim());
        if (!_allowedSourceRoots.Any(root => IsUnderRoot(fullPath, root)) || !File.Exists(fullPath))
        {
            throw new InvalidOperationException($"origin_dossier_media_{name}_unavailable");
        }
    }

    private async Task WriteReceiptAsync(
        OriginDossierMediaDispatchReceipt receipt,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_receiptRoot);
        string receiptPath = ReceiptPath(receipt.RequestId);
        string temporaryPath = receiptPath + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(receipt, JsonOptions) + Environment.NewLine,
            Utf8NoBom,
            cancellationToken);
        File.Move(temporaryPath, receiptPath, true);
    }

    private string ReceiptPath(string requestId)
        => Path.Combine(_receiptRoot, SafeToken(requestId) + ".receipt.json");

    private void MoveProcessed(string processingPath, string requestId, string state)
    {
        string destinationRoot = Path.Combine(_inboxRoot, state);
        Directory.CreateDirectory(destinationRoot);
        string destination = Path.Combine(destinationRoot, SafeToken(requestId) + ".request.json");
        File.Move(processingPath, destination, true);
    }

    private static string SafeErrorCode(Exception exception)
    {
        string value = exception.Message.Trim().ToLowerInvariant();
        if (value.StartsWith("origin_dossier_media_", StringComparison.Ordinal))
        {
            return value.Split(':', 2)[0];
        }

        return $"origin_dossier_media_{exception.GetType().Name.ToLowerInvariant()}";
    }

    private static string RequireRoot(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        return Path.GetFullPath(value.Trim());
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"origin_dossier_media_{name}_missing");
        }
    }

    private static void RequireHash(string value, string name)
    {
        if (value.Length != 64 || value.Any(static character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException($"origin_dossier_media_{name}_invalid");
        }
    }

    private static bool IsUnderRoot(string path, string root)
    {
        string normalizedPath = Path.GetFullPath(path);
        string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.Ordinal);
    }

    private static string SafeToken(string value)
    {
        string normalized = new(
            value
                .Select(static character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'
                    ? character
                    : '-')
                .ToArray());
        normalized = normalized.Trim('-', '_', '.');
        return string.IsNullOrWhiteSpace(normalized) ? "origin-dossier-media" : normalized;
    }

    private static string Sha256(byte[] value)
        => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static async Task<string> Sha256FileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
