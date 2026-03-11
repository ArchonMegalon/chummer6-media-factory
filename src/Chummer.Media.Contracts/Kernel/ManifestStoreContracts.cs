#pragma warning disable CS1591
using Chummer.Media.Contracts.Assets;

namespace Chummer.Media.Contracts.Kernel;

/// <summary>
/// Contract for creating a new immutable manifest with initial lifecycle state.
/// </summary>
public sealed record CreateManifestRequest(
    string IdempotencyKey,
    MediaAssetManifest Manifest);

public sealed record CreateManifestResult(
    bool Created,
    MediaAssetManifest Manifest,
    int Version);

/// <summary>
/// Contract for fetching a manifest by identity.
/// </summary>
public sealed record GetManifestRequest(
    string AssetId);

public sealed record GetManifestResult(
    bool Found,
    MediaAssetManifest? Manifest,
    int? Version);

/// <summary>
/// Contract for mutating lifecycle metadata without changing immutable byte identity fields.
/// </summary>
public sealed record UpdateManifestLifecycleRequest(
    string AssetId,
    int ExpectedVersion,
    MediaAssetLifecycleState Lifecycle,
    string MutationReason);

public sealed record UpdateManifestLifecycleResult(
    bool Updated,
    MediaAssetManifest? Manifest,
    int? Version,
    string? RejectionReason);
#pragma warning restore CS1591
