using CodeLogic.Application.Flowcharts;

namespace CodeLogic.Web.Playground;

/// <summary>
/// Works out which nodes a trace step skipped, so the flowchart and the code view can dim them.
/// Uses only the generated graph: it never changes the graph or the trace.
/// </summary>
public static class TraceBranchHighlighter
{
    /// <summary>
    /// When control moves from a decision to one of its branches, the skipped nodes are those reachable
    /// only through the branch that was not taken (statements both branches reach, such as the code
    /// after an if/else, stay bright). Entering a loop body or repeating a loop skips nothing, because the
    /// code after the loop will still run. Any other step skips nothing.
    /// </summary>
    public static IReadOnlySet<string> SkippedNodeIds(FlowGraph graph, string? previousNodeId, string currentNodeId)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentNodeId);
        var none = new HashSet<string>(StringComparer.Ordinal);
        if (previousNodeId is null
            || graph.Nodes.FirstOrDefault(node => node.Id == previousNodeId) is not { Type: FlowNodeType.Decision })
        {
            return none;
        }

        var taken = graph.Edges.FirstOrDefault(edge => edge.FromNodeId == previousNodeId && edge.ToNodeId == currentNodeId);
        if (taken is null || taken.Type == FlowEdgeType.LoopBack)
        {
            return none;
        }

        var isLoopCondition = graph.Edges.Any(edge => edge.ToNodeId == previousNodeId
            && (edge.Type == FlowEdgeType.LoopBack || edge is { Type: FlowEdgeType.Jump, Label: "continue" }));
        if (isLoopCondition && taken.Type == FlowEdgeType.True)
        {
            return none;
        }

        var notTaken = graph.Edges
            .Where(edge => edge.FromNodeId == previousNodeId && edge != taken && edge.Type is FlowEdgeType.True or FlowEdgeType.False)
            .Select(edge => edge.ToNodeId);
        var reachedFromTaken = Reachable(graph, [currentNodeId]);
        var skipped = Reachable(graph, notTaken);
        skipped.ExceptWith(reachedFromTaken);
        skipped.Remove(previousNodeId);
        return skipped;
    }

    // Forward flow only: loop-backs and continue jumps point backwards, and calls lead into other methods.
    private static HashSet<string> Reachable(FlowGraph graph, IEnumerable<string> starts)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(starts);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!reached.Add(id))
            {
                continue;
            }

            foreach (var edge in graph.Edges.Where(edge => edge.FromNodeId == id))
            {
                var forward = edge.Type is FlowEdgeType.Next or FlowEdgeType.True or FlowEdgeType.False
                    || edge is { Type: FlowEdgeType.Jump, Label: "break" };
                if (forward)
                {
                    pending.Push(edge.ToNodeId);
                }
            }
        }

        return reached;
    }
}
