namespace Chummer.Media.Contracts.Rendering;

/// <summary>
/// Supported render-only output families owned by media-factory.
/// </summary>
public enum MediaRenderKind
{
    /// <summary>
    /// Deterministic document rendering.
    /// </summary>
    Document = 0,

    /// <summary>
    /// Portrait image generation/rendering.
    /// </summary>
    Portrait = 1,

    /// <summary>
    /// Bounded video rendering.
    /// </summary>
    Video = 2,
}
