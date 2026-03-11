#pragma warning disable CS1591
namespace Chummer.Media.Contracts.Storage;

/// <summary>
/// Provider-neutral locator for immutable binary bytes.
/// </summary>
public sealed record BinaryLocator(
    string Store,
    string Container,
    string ObjectKey,
    string LocatorHash);

public sealed record BinaryWriteRequest(
    string AssetId,
    BinaryLocator Locator,
    long ContentLengthBytes,
    string ContentHash,
    string ContentType);

public sealed record BinaryWriteResult(
    bool Accepted,
    BinaryLocator Locator,
    bool HashVerified,
    string? RejectionReason);

public sealed record BinaryReadRequest(
    string AssetId,
    BinaryLocator Locator,
    long ExpectedLengthBytes,
    string ExpectedHash);

public sealed record BinaryReadResult(
    bool Found,
    BinaryLocator Locator,
    long ContentLengthBytes,
    string ContentHash,
    bool HashMatches,
    string? RejectionReason);

public sealed record BinaryDeleteRequest(
    string AssetId,
    BinaryLocator Locator,
    string Reason);

public sealed record BinaryDeleteResult(
    bool Deleted,
    BinaryLocator Locator,
    string? RejectionReason);
#pragma warning restore CS1591
