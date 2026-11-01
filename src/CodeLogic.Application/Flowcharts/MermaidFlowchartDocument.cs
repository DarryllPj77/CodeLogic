using System.Collections.ObjectModel;

namespace CodeLogic.Application.Flowcharts;

public sealed record FlowNodeSourceMap(
    string NodeId,
    string MermaidId,
    FlowNodeType NodeType,
    string Label,
    SourceSpan? SourceSpan);

public sealed class MermaidFlowchartDocument
{
    public MermaidFlowchartDocument(
        string definition,
        IEnumerable<FlowNodeSourceMap> sourceMap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition);
        ArgumentNullException.ThrowIfNull(sourceMap);

        Definition = definition;
        SourceMap = new ReadOnlyCollection<FlowNodeSourceMap>(sourceMap.ToArray());
    }

    public string Definition { get; }

    public IReadOnlyList<FlowNodeSourceMap> SourceMap { get; }
}

public interface IMermaidFlowchartSerializer
{
    MermaidFlowchartDocument Serialize(FlowGraph graph);
}
