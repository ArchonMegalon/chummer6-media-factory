#pragma warning disable CS1591
namespace Chummer.Media.Contracts.Lineage;

public enum LineageRelationType
{
    Parent = 0,
    Derived = 1,
    SupersededBy = 2,
    PreviewOf = 3,
}

public sealed record AssetLineageNode(
    string AssetId,
    string RenderJobId,
    string? ParentAssetId,
    string? PreviewAssetId,
    IReadOnlyList<string> DerivedAssetIds,
    DateTimeOffset CreatedAtUtc);

public sealed record AssetLineageEdge(
    string FromAssetId,
    string ToAssetId,
    LineageRelationType RelationType);

public sealed record AssetLineageQuery(
    string RootAssetId,
    bool IncludeParents,
    bool IncludeDerived,
    bool IncludeSupersessions,
    bool IncludePreviews,
    int MaxDepth);

public sealed record AssetLineageResult(
    string RootAssetId,
    IReadOnlyList<AssetLineageNode> Nodes,
    IReadOnlyList<AssetLineageEdge> Edges);
#pragma warning restore CS1591
