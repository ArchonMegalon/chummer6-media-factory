namespace Chummer.Media.Contracts.Assets;

/// <summary>
/// Catalog-facing asset metadata used to index manifests without taking ownership of delivery policy.
/// </summary>
public sealed record MediaAssetCatalogEntry(
    string AssetId,
    string CatalogKey,
    string OwnerId,
    string RenderJobId,
    string RenderRequestId,
    string DisplayName,
    string? PreviewAssetId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc);
