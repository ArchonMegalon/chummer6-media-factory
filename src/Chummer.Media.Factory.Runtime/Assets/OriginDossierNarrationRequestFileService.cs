using Chummer.Media.Contracts;
using System.Text.Json;

namespace Chummer.Run.AI.Services.Assets;

public interface IOriginDossierNarrationRequestFileService
{
    Task<OriginDossierNarrationRequestFileResult> RenderFromFileAsync(
        string requestPath,
        CancellationToken cancellationToken = default);
}

public sealed class OriginDossierNarrationRequestFileService : IOriginDossierNarrationRequestFileService
{
    private const string ExpectedArtifactKind = "origin_dossier_bundle_audiobook_render_request";
    private const string ExpectedOwnerRepo = "chummer6-media-factory";

    private readonly IOriginDossierNarrationRenderingService _rendering;

    public OriginDossierNarrationRequestFileService(IOriginDossierNarrationRenderingService rendering)
    {
        _rendering = rendering;
    }

    public async Task<OriginDossierNarrationRequestFileResult> RenderFromFileAsync(
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
            throw new FileNotFoundException("Origin dossier narration request file was not found.", requestPath);
        }

        var requestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(requestPath, cancellationToken));
        var renderRequest = Parse(requestDocument.RootElement);
        var renderReceipt = await _rendering.RenderAsync(renderRequest, cancellationToken);
        var receiptPath = BuildReceiptPath(requestPath);
        var persistedReceipt = new
        {
            artifactKind = ExpectedArtifactKind,
            ownerRepo = ExpectedOwnerRepo,
            requestPath,
            receiptPath,
            renderedAtUtc = renderReceipt.RenderedAtUtc,
            renderReceipt
        };

        await File.WriteAllTextAsync(
            receiptPath,
            JsonSerializer.Serialize(persistedReceipt, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        return new OriginDossierNarrationRequestFileResult(
            RequestPath: requestPath,
            ReceiptPath: receiptPath,
            Request: renderRequest,
            Receipt: renderReceipt);
    }

    private static OriginDossierNarrationRenderRequest Parse(JsonElement root)
    {
        RequireObject(root);
        string artifactKind = ReadRequiredString(root, "artifactKind");
        if (!string.Equals(artifactKind, ExpectedArtifactKind, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Origin dossier narration request artifactKind must be {ExpectedArtifactKind}.", nameof(root));
        }

        string ownerRepo = ReadRequiredString(root, "ownerRepo");
        if (!string.Equals(ownerRepo, ExpectedOwnerRepo, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Origin dossier narration request ownerRepo must be {ExpectedOwnerRepo}.", nameof(root));
        }

        string renderingId = ReadRequiredString(root, "renderRequestId");
        string approvedOriginPacketId = ReadRequiredString(root, "approvedOriginPacketId");
        string originRevisionId = ReadRequiredString(root, "originRevisionId");
        string source = ReadRequiredString(root, "source");
        DateTimeOffset requestedAtUtc = ReadRequiredDateTimeOffset(root, "requestedAtUtc");

        var artifactsElement = ReadRequiredProperty(root, "narrationArtifacts");
        if (artifactsElement.ValueKind is not JsonValueKind.Array || artifactsElement.GetArrayLength() == 0)
        {
            throw new ArgumentException("Origin dossier narration request must include at least one narration artifact.", nameof(root));
        }

        var artifacts = artifactsElement
            .EnumerateArray()
            .Select((artifact, index) => ParseArtifact(artifact, index, approvedOriginPacketId, originRevisionId))
            .ToArray();

        return new OriginDossierNarrationRenderRequest(
            RenderingId: renderingId,
            ApprovedOriginPacketId: approvedOriginPacketId,
            OriginRevisionId: originRevisionId,
            Source: source,
            RequestedAtUtc: requestedAtUtc,
            Artifacts: artifacts);
    }

    private static OriginDossierNarrationArtifactRenderRequest ParseArtifact(
        JsonElement artifact,
        int index,
        string approvedOriginPacketId,
        string originRevisionId)
    {
        RequireObject(artifact);
        string role = ReadRequiredString(artifact, "role");
        if (!string.Equals(role, "audio", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Origin dossier narration artifacts[{index}] must use role 'audio'.", nameof(artifact));
        }

        string provider = ReadRequiredString(artifact, "provider");
        string providerState = ReadRequiredString(artifact, "providerState");
        string variant = ReadRequiredString(artifact, "variant");
        string outputFormat = ReadRequiredString(artifact, "outputFormat");
        string companionRef = ReadRequiredString(artifact, "companionRef");
        string scriptPath = ReadRequiredString(artifact, "scriptPath");
        string packetPath = ReadRequiredString(artifact, "packetPath");
        var captionRefs = ReadRequiredStringArray(artifact, "captionRefs");
        var previewRefs = ReadRequiredStringArray(artifact, "previewRefs");

        var typedRole = provider switch
        {
            "Soundmadeseen" => OriginDossierNarrationArtifactRole.CanonicalAudio,
            "Unmixr AI" => OriginDossierNarrationArtifactRole.AlternateAudio,
            _ => throw new ArgumentException($"Origin dossier narration provider is unsupported: {provider}.", nameof(artifact))
        };

        string category = typedRole switch
        {
            OriginDossierNarrationArtifactRole.CanonicalAudio => "origin-dossier/narration/audio/canonical",
            OriginDossierNarrationArtifactRole.AlternateAudio => "origin-dossier/narration/audio/alternate",
            _ => throw new ArgumentOutOfRangeException(nameof(typedRole), typedRole, "Unsupported origin dossier narration artifact role.")
        };

        string payload = JsonSerializer.Serialize(
            new
            {
                approvedOriginPacketId,
                originRevisionId,
                provider,
                providerState,
                variant,
                scriptPath,
                packetPath
            });

        return new OriginDossierNarrationArtifactRenderRequest(
            Role: typedRole,
            Provider: provider,
            Category: category,
            Payload: payload,
            OutputFormat: outputFormat,
            CompanionRef: companionRef,
            CaptionRefs: captionRefs,
            PreviewRefs: previewRefs,
            DeduplicationKey: $"{provider}|{variant}|{outputFormat}|{companionRef}");
    }

    private static string BuildReceiptPath(string requestPath)
    {
        const string suffix = ".request.json";
        if (requestPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return requestPath[..^suffix.Length] + ".receipt.json";
        }

        return requestPath + ".receipt.json";
    }

    private static JsonElement ReadRequiredProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            throw new ArgumentException($"Origin dossier narration request is missing {name}.", nameof(element));
        }

        return value;
    }

    private static string ReadRequiredString(JsonElement element, string name)
    {
        var property = ReadRequiredProperty(element, name);
        if (property.ValueKind is not JsonValueKind.String)
        {
            throw new ArgumentException($"Origin dossier narration request field {name} must be a string.", nameof(element));
        }

        var value = property.GetString()?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            throw new ArgumentException($"Origin dossier narration request field {name} is required.", nameof(element));
        }

        return value;
    }

    private static DateTimeOffset ReadRequiredDateTimeOffset(JsonElement element, string name)
    {
        var value = ReadRequiredString(element, name);
        if (!DateTimeOffset.TryParse(value, out var parsed))
        {
            throw new ArgumentException($"Origin dossier narration request field {name} must be a valid date-time.", nameof(element));
        }

        return parsed;
    }

    private static IReadOnlyList<string> ReadRequiredStringArray(JsonElement element, string name)
    {
        var property = ReadRequiredProperty(element, name);
        if (property.ValueKind is not JsonValueKind.Array)
        {
            throw new ArgumentException($"Origin dossier narration request field {name} must be an array.", nameof(element));
        }

        var values = property
            .EnumerateArray()
            .Select(item =>
            {
                if (item.ValueKind is not JsonValueKind.String)
                {
                    throw new ArgumentException($"Origin dossier narration request field {name} must contain only strings.", nameof(element));
                }

                return item.GetString()?.Trim() ?? string.Empty;
            })
            .Where(static value => value.Length > 0)
            .ToArray();

        if (values.Length == 0)
        {
            throw new ArgumentException($"Origin dossier narration request field {name} must contain at least one value.", nameof(element));
        }

        return values;
    }

    private static void RequireObject(JsonElement element)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            throw new ArgumentException("Origin dossier narration request payload must be a JSON object.", nameof(element));
        }
    }
}

public sealed record OriginDossierNarrationRequestFileResult(
    string RequestPath,
    string ReceiptPath,
    OriginDossierNarrationRenderRequest Request,
    OriginDossierNarrationRenderReceipt Receipt);
