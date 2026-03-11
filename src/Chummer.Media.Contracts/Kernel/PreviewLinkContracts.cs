#pragma warning disable CS1591
namespace Chummer.Media.Contracts.Kernel;

public enum PreviewRelationType
{
    Preview = 0,
    Thumbnail = 1,
}

/// <summary>
/// Link between a source asset and a derived preview-class asset.
/// </summary>
public sealed record PreviewLink(
    string SourceAssetId,
    string PreviewAssetId,
    PreviewRelationType RelationType,
    DateTimeOffset LinkedAtUtc);

public sealed record UpsertPreviewLinkRequest(
    string SourceAssetId,
    string PreviewAssetId,
    PreviewRelationType RelationType,
    string IdempotencyKey,
    DateTimeOffset LinkedAtUtc);

public sealed record UpsertPreviewLinkResult(
    bool Upserted,
    PreviewLink Link,
    bool ReplayedFromIdempotencyKey);

public sealed record GetPreviewChainRequest(
    string SourceAssetId);

public sealed record GetPreviewChainResult(
    string SourceAssetId,
    IReadOnlyList<PreviewLink> PreviewChain);
#pragma warning restore CS1591
