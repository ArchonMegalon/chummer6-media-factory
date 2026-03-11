namespace Chummer.Media.Contracts.Jobs;

/// <summary>
/// Defines the ownership boundary for dedupe decisions within the factory job queue.
/// </summary>
public enum RenderJobDedupeScope
{
    /// <summary>
    /// Dedupe by the exact render request payload.
    /// </summary>
    Request = 0,

    /// <summary>
    /// Dedupe within a template/version lineage.
    /// </summary>
    TemplateVersion = 1,

    /// <summary>
    /// Dedupe by intended output asset identity.
    /// </summary>
    OutputAsset = 2,
}
