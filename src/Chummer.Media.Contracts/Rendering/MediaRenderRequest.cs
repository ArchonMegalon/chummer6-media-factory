namespace Chummer.Media.Contracts.Rendering;

/// <summary>
/// Deterministic render input passed into media-factory after upstream orchestration is complete.
/// </summary>
public sealed record MediaRenderRequest(
    string RenderRequestId,
    MediaRenderKind RenderKind,
    string TemplateId,
    string TemplateVersion,
    string OutputFormat,
    string ContentHash,
    string RequestedBy,
    IReadOnlyDictionary<string, string> Inputs,
    DateTimeOffset RequestedAtUtc);
