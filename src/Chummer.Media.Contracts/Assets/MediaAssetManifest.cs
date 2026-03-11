using Chummer.Media.Contracts.Rendering;

namespace Chummer.Media.Contracts.Assets;

/// <summary>
/// Canonical manifest for a rendered asset, including storage, previews, retention, and lineage metadata.
/// </summary>
public sealed record MediaAssetManifest(
    string AssetId,
    string CatalogKey,
    string RenderJobId,
    MediaRenderKind RenderKind,
    string StorageBucket,
    string StorageObjectKey,
    string ContentType,
    long ContentLengthBytes,
    string ContentHash,
    string? PreviewAssetId,
    string? ParentAssetId,
    MediaAssetLifecycleState Lifecycle,
    IReadOnlyList<string> DerivedAssetIds);
