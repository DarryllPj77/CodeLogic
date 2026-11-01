using CodeLogic.Application.Flowcharts;
using CodeLogic.Infrastructure.Flowcharts;

namespace CodeLogic.IntegrationTests.Flowcharts;

public sealed class MermaidFlowchartSerializerTests
{
    private readonly MermaidFlowchartSerializer serializer = new();

    [Fact]
    public void BranchGraphUsesCorrectShapesAndTrueFalseArrows()
    {
        var graph = BranchGraph();

        var document = serializer.Serialize(graph);

        Assert.Equal(
            """
            flowchart TD
              accTitle: CodeLogic flowchart
              accDescr: Supported C sharp source shown as an ordered flowchart

              n000(["Start"])
              n001["int score = 6;"]
              n002{"score ≥ 5"}
              n003[/"Console.WriteLine(″Pass″);"/]
              n004[/"Console.WriteLine(″Try again″);"/]
              n005(["End"])

              n000 --> n001
              n001 --> n002
              n002 -->|"True"| n003
              n002 -->|"False"| n004
              n003 --> n005
              n004 --> n005
            """,
            document.Definition);
    }

    [Fact]
    public void SourceMapPreservesStableGraphIdsAndPositions()
    {
        var graph = BranchGraph();

        var document = serializer.Serialize(graph);

        Assert.Equal(graph.Nodes.Count, document.SourceMap.Count);
        Assert.Collection(
            document.SourceMap,
            item => AssertSourceMap(item, "node-000", "n000", FlowNodeType.Start, null),
            item => AssertSourceMap(item, "node-001", "n001", FlowNodeType.Action, 1),
            item => AssertSourceMap(item, "node-002", "n002", FlowNodeType.Decision, 2),
            item => AssertSourceMap(item, "node-003", "n003", FlowNodeType.Output, 4),
            item => AssertSourceMap(item, "node-004", "n004", FlowNodeType.Output, 8),
            item => AssertSourceMap(item, "node-005", "n005", FlowNodeType.End, null));
    }

    [Fact]
    public void LearnerLabelsAreEscapedInsteadOfBecomingMermaidSyntaxOrHtml()
    {
        const string unsafeLabel = "value\"]\nclick n000 evil <script> & %%{";
        var graph = new FlowGraph(
            [
                new FlowNode("start", FlowNodeType.Start, "Start", null),
                new FlowNode("action", FlowNodeType.Action, unsafeLabel, Span(1)),
                new FlowNode("end", FlowNodeType.End, "End", null),
            ],
            [
                new FlowEdge("edge-000", "start", "action", FlowEdgeType.Next),
                new FlowEdge("edge-001", "action", "end", FlowEdgeType.Next),
            ]);

        var document = serializer.Serialize(graph);

        Assert.Contains(
            "value″] click n000 evil ‹script› ＆ ％％{",
            document.Definition,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", document.Definition, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\nclick", document.Definition, StringComparison.Ordinal);
        Assert.DoesNotContain("%%{", document.Definition, StringComparison.Ordinal);
    }

    [Fact]
    public void SameGraphProducesSameMermaidDefinition()
    {
        var graph = BranchGraph();

        var first = serializer.Serialize(graph);
        var second = serializer.Serialize(graph);

        Assert.Equal(first.Definition, second.Definition);
        Assert.Equal(first.SourceMap, second.SourceMap);
    }

    private static FlowGraph BranchGraph() => new(
        [
            new FlowNode("node-000", FlowNodeType.Start, "Start", null),
            new FlowNode("node-001", FlowNodeType.Action, "int score = 6;", Span(1)),
            new FlowNode("node-002", FlowNodeType.Decision, "score >= 5", Span(2)),
            new FlowNode("node-003", FlowNodeType.Output, "Console.WriteLine(\"Pass\");", Span(4)),
            new FlowNode("node-004", FlowNodeType.Output, "Console.WriteLine(\"Try again\");", Span(8)),
            new FlowNode("node-005", FlowNodeType.End, "End", null),
        ],
        [
            new FlowEdge("edge-000", "node-000", "node-001", FlowEdgeType.Next),
            new FlowEdge("edge-001", "node-001", "node-002", FlowEdgeType.Next),
            new FlowEdge("edge-002", "node-002", "node-003", FlowEdgeType.True, "True"),
            new FlowEdge("edge-003", "node-002", "node-004", FlowEdgeType.False, "False"),
            new FlowEdge("edge-004", "node-003", "node-005", FlowEdgeType.Next),
            new FlowEdge("edge-005", "node-004", "node-005", FlowEdgeType.Next),
        ]);

    private static SourceSpan Span(int line) => new(
        startOffset: line * 10,
        length: 5,
        startLine: line,
        startColumn: 1,
        endLine: line,
        endColumn: 6);

    private static void AssertSourceMap(
        FlowNodeSourceMap item,
        string nodeId,
        string mermaidId,
        FlowNodeType nodeType,
        int? startLine)
    {
        Assert.Equal(nodeId, item.NodeId);
        Assert.Equal(mermaidId, item.MermaidId);
        Assert.Equal(nodeType, item.NodeType);
        Assert.Equal(startLine, item.SourceSpan?.StartLine);
    }
}
