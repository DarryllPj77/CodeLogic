using System.Text;

using CodeLogic.Application.Flowcharts;

namespace CodeLogic.Infrastructure.Flowcharts;

public sealed class MermaidFlowchartSerializer : IMermaidFlowchartSerializer
{
    public MermaidFlowchartDocument Serialize(FlowGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var mermaidIds = graph.Nodes
            .Select((node, index) => new { node.Id, MermaidId = $"n{index:D3}" })
            .ToDictionary(item => item.Id, item => item.MermaidId, StringComparer.Ordinal);
        var sourceMap = graph.Nodes
            .Select(node => new FlowNodeSourceMap(
                node.Id,
                mermaidIds[node.Id],
                node.Type,
                node.Label,
                node.SourceSpan))
            .ToArray();

        var definition = new StringBuilder()
            .Append("flowchart TD\n")
            .Append("  accTitle: CodeLogic flowchart\n")
            .Append("  accDescr: Supported C sharp source shown as an ordered flowchart\n\n");

        // The entry flow is drawn first; each static method becomes a labelled subgraph.
        var methodIndex = 0;
        foreach (var scope in graph.Nodes.GroupBy(node => node.Scope, StringComparer.Ordinal))
        {
            var indent = "  ";
            if (scope.Key is not null)
            {
                definition
                    .Append("  subgraph method")
                    .Append(methodIndex++)
                    .Append("[\"")
                    .Append(EscapeLabel($"static method {scope.Key}"))
                    .Append("\"]\n    direction TB\n");
                indent = "    ";
            }

            foreach (var node in scope)
            {
                definition
                    .Append(indent)
                    .Append(mermaidIds[node.Id])
                    .Append(NodeShape(node.Type, EscapeNodeLabel(node.Label)))
                    .Append('\n');
            }

            if (scope.Key is not null)
            {
                definition.Append("  end\n");
            }
        }

        definition.Append('\n');
        foreach (var edge in graph.Edges)
        {
            definition
                .Append("  ")
                .Append(mermaidIds[edge.FromNodeId])
                .Append(edge.Type switch
                {
                    FlowEdgeType.Call => " -.->",
                    FlowEdgeType.Jump => " ==>",
                    _ => " -->",
                });
            if (edge.Label is not null)
            {
                definition
                    .Append("|\"")
                    .Append(EscapeLabel(edge.Label))
                    .Append("\"|");
            }

            definition
                .Append(' ')
                .Append(mermaidIds[edge.ToNodeId])
                .Append('\n');
        }

        return new MermaidFlowchartDocument(
            definition.ToString().TrimEnd(),
            sourceMap);
    }

    private static string NodeShape(FlowNodeType type, string label) => type switch
    {
        FlowNodeType.Start or FlowNodeType.End => $"([\"{label}\"])",
        FlowNodeType.Decision => $"{{\"{label}\"}}",
        FlowNodeType.Input or FlowNodeType.Output => $"[/\"{label}\"/]",
        FlowNodeType.Action => $"[\"{label}\"]",
        FlowNodeType.Call => $"[[\"{label}\"]]",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown flow node type."),
    };

    private static string EscapeNodeLabel(string label)
    {
        var escaped = EscapeLabel(label);
        var wrapped = new StringBuilder(escaped.Length + 8);
        for (var index = 0; index < escaped.Length; index++)
        {
            var character = escaped[index];
            wrapped.Append(character);
            if (character is '.' or '(' or ',' or ':'
                && index + 1 < escaped.Length
                && !char.IsWhiteSpace(escaped[index + 1]))
            {
                wrapped.Append('\u200B');
            }
        }

        return wrapped.ToString();
    }

    private static string EscapeLabel(string label)
    {
        var readableLabel = label
            .Replace(">=", "≥", StringComparison.Ordinal)
            .Replace("<=", "≤", StringComparison.Ordinal)
            .Replace("&&", "∧", StringComparison.Ordinal)
            .Replace("||", "∨", StringComparison.Ordinal);
        var escaped = new StringBuilder(label.Length);
        var previousWasSpace = false;
        foreach (var rune in readableLabel.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                if (!previousWasSpace)
                {
                    escaped.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            previousWasSpace = false;
            if (Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.Control)
            {
                escaped.Append('\uFFFD');
            }
            else
            {
                escaped.Append(rune.Value switch
                {
                    '"' => '\u2033',
                    '<' => '\u2039',
                    '>' => '\u203A',
                    '&' => '\uFF06',
                    '#' => '\uFF03',
                    '%' => '\uFF05',
                    '`' => '\uFF40',
                    _ => rune.ToString(),
                });
            }
        }

        return escaped.ToString().Trim();
    }
}
