using System.Collections.ObjectModel;

namespace CodeLogic.Application.Flowcharts;

public enum FlowNodeType
{
    Start = 1,
    End = 2,
    Action = 3,
    Decision = 4,
    Input = 5,
    Output = 6,
}

public enum FlowEdgeType
{
    Next = 1,
    True = 2,
    False = 3,
    LoopBack = 4,
}

public sealed record SourceSpan
{
    public SourceSpan(
        int startOffset,
        int length,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startLine);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startColumn);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(endLine);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(endColumn);
        if (endLine < startLine || (endLine == startLine && endColumn < startColumn))
        {
            throw new ArgumentException("The source span end must not precede its start.");
        }

        StartOffset = startOffset;
        Length = length;
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    public int StartOffset { get; }

    public int Length { get; }

    public int StartLine { get; }

    public int StartColumn { get; }

    public int EndLine { get; }

    public int EndColumn { get; }
}

public sealed record FlowNode
{
    public FlowNode(string id, FlowNodeType type, string label, SourceSpan? sourceSpan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown flow node type.");
        }

        Id = id;
        Type = type;
        Label = label;
        SourceSpan = sourceSpan;
    }

    public string Id { get; }

    public FlowNodeType Type { get; }

    public string Label { get; }

    public SourceSpan? SourceSpan { get; }
}

public sealed record FlowEdge
{
    public FlowEdge(
        string id,
        string fromNodeId,
        string toNodeId,
        FlowEdgeType type,
        string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toNodeId);
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown flow edge type.");
        }

        Id = id;
        FromNodeId = fromNodeId;
        ToNodeId = toNodeId;
        Type = type;
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    }

    public string Id { get; }

    public string FromNodeId { get; }

    public string ToNodeId { get; }

    public FlowEdgeType Type { get; }

    public string? Label { get; }
}

public sealed class FlowGraph
{
    public const string CurrentSchemaVersion = "1.1";

    public FlowGraph(IEnumerable<FlowNode> nodes, IEnumerable<FlowEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var nodeArray = nodes.ToArray();
        var edgeArray = edges.ToArray();
        Validate(nodeArray, edgeArray);

        SchemaVersion = CurrentSchemaVersion;
        Nodes = new ReadOnlyCollection<FlowNode>(nodeArray);
        Edges = new ReadOnlyCollection<FlowEdge>(edgeArray);
    }

    public string SchemaVersion { get; }

    public IReadOnlyList<FlowNode> Nodes { get; }

    public IReadOnlyList<FlowEdge> Edges { get; }

    private static void Validate(FlowNode[] nodes, FlowEdge[] edges)
    {
        if (nodes.Count(node => node.Type == FlowNodeType.Start) != 1)
        {
            throw new ArgumentException("A flow graph must contain exactly one start node.", nameof(nodes));
        }

        if (nodes.Count(node => node.Type == FlowNodeType.End) != 1)
        {
            throw new ArgumentException("A flow graph must contain exactly one end node.", nameof(nodes));
        }

        var nodeIds = nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        if (nodeIds.Count != nodes.Length)
        {
            throw new ArgumentException("Flow node IDs must be unique.", nameof(nodes));
        }

        if (edges.Select(edge => edge.Id).Distinct(StringComparer.Ordinal).Count() != edges.Length)
        {
            throw new ArgumentException("Flow edge IDs must be unique.", nameof(edges));
        }

        if (edges.Any(edge => !nodeIds.Contains(edge.FromNodeId) || !nodeIds.Contains(edge.ToNodeId)))
        {
            throw new ArgumentException("Every flow edge must reference existing nodes.", nameof(edges));
        }
    }
}
