namespace Chummer.Run.AI.Services.Assets;

public sealed record MagicFitRenderRequest(
    string RequestId,
    string JobType,
    string Prompt,
    string NegativePrompt,
    string AspectRatio,
    int DurationSeconds,
    IReadOnlyList<string> SourceImageRefs,
    string PublicSafetyPacketId,
    string StyleProfile,
    string OutputTarget,
    IReadOnlyList<string> AllowedModels,
    string RequestedBy,
    string Source);
