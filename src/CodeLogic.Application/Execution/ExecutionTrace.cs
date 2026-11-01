using System.Collections.ObjectModel;

using CodeLogic.Application.Analysis;
using CodeLogic.Application.Flowcharts;

namespace CodeLogic.Application.Execution;

public static class ExecutionTraceDefaults
{
    public const int MaximumOperations = 100;
}

public sealed record ExecutionTraceLimits
{
    public ExecutionTraceLimits(int maximumOperations)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOperations);
        MaximumOperations = maximumOperations;
    }

    public int MaximumOperations { get; }

    public static ExecutionTraceLimits Default { get; } = new(
        ExecutionTraceDefaults.MaximumOperations);
}

public enum ExecutionTraceStatus
{
    Completed = 1,
    StepLimitReached = 2,
    InvalidInput = 3,
    SyntaxError = 4,
    Unsupported = 5,
    TimedOut = 6,
}

public sealed class ExecutionTraceStep
{
    public ExecutionTraceStep(
        int programCounter,
        string activeNodeId,
        FlowNodeType activeNodeType,
        string activeNodeLabel,
        IEnumerable<KeyValuePair<string, string>> variables,
        IEnumerable<string> output,
        string explanation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(programCounter);
        ArgumentException.ThrowIfNullOrWhiteSpace(activeNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(activeNodeLabel);
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);

        ProgramCounter = programCounter;
        ActiveNodeId = activeNodeId;
        ActiveNodeType = activeNodeType;
        ActiveNodeLabel = activeNodeLabel;
        Variables = new ReadOnlyDictionary<string, string>(
            variables.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        Output = new ReadOnlyCollection<string>(output.ToArray());
        Explanation = explanation;
    }

    public int ProgramCounter { get; }

    public string ActiveNodeId { get; }

    public FlowNodeType ActiveNodeType { get; }

    public string ActiveNodeLabel { get; }

    public IReadOnlyDictionary<string, string> Variables { get; }

    public IReadOnlyList<string> Output { get; }

    public string Explanation { get; }
}

public sealed class ExecutionTraceResult
{
    public ExecutionTraceResult(
        ExecutionTraceStatus status,
        int maximumOperations,
        IEnumerable<ExecutionTraceStep>? steps = null,
        IEnumerable<CodeDiagnostic>? diagnostics = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOperations);
        Status = status;
        MaximumOperations = maximumOperations;
        Steps = new ReadOnlyCollection<ExecutionTraceStep>(steps?.ToArray() ?? []);
        Diagnostics = new ReadOnlyCollection<CodeDiagnostic>(diagnostics?.ToArray() ?? []);

        if (HasTrace != (Steps.Count > 0))
        {
            throw new ArgumentException(
                "Completed and limited results require steps; failure results cannot expose a partial trace.",
                nameof(steps));
        }
    }

    public ExecutionTraceStatus Status { get; }

    public int MaximumOperations { get; }

    public IReadOnlyList<ExecutionTraceStep> Steps { get; }

    public IReadOnlyList<CodeDiagnostic> Diagnostics { get; }

    public bool HasTrace => Status is ExecutionTraceStatus.Completed
        or ExecutionTraceStatus.StepLimitReached;
}

public interface IPedagogicalInterpreter
{
    ExecutionTraceLimits Limits { get; }

    Task<ExecutionTraceResult> TraceAsync(
        string? source,
        CancellationToken cancellationToken = default);
}
