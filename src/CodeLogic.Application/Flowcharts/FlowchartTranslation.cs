using System.Collections.ObjectModel;

using CodeLogic.Application.Analysis;

namespace CodeLogic.Application.Flowcharts;

public enum FlowchartTranslationStatus
{
    Success = 1,
    InvalidInput = 2,
    SyntaxError = 3,
    Unsupported = 4,
    TimedOut = 5,
}

public sealed class FlowchartTranslationResult
{
    public FlowchartTranslationResult(
        FlowchartTranslationStatus status,
        FlowGraph? graph = null,
        IEnumerable<CodeDiagnostic>? diagnostics = null)
    {
        if (status == FlowchartTranslationStatus.Success && graph is null)
        {
            throw new ArgumentException("A successful translation requires a graph.", nameof(graph));
        }

        if (status != FlowchartTranslationStatus.Success && graph is not null)
        {
            throw new ArgumentException("An unsuccessful translation cannot expose a partial graph.", nameof(graph));
        }

        Status = status;
        Graph = graph;
        Diagnostics = new ReadOnlyCollection<CodeDiagnostic>(diagnostics?.ToArray() ?? []);
    }

    public FlowchartTranslationStatus Status { get; }

    public FlowGraph? Graph { get; }

    public IReadOnlyList<CodeDiagnostic> Diagnostics { get; }

    public bool IsSuccess => Status == FlowchartTranslationStatus.Success;
}

public interface IFlowchartTranslator
{
    Task<FlowchartTranslationResult> TranslateAsync(
        string? source,
        CancellationToken cancellationToken = default);
}
