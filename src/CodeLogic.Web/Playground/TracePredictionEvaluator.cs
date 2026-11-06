using CodeLogic.Application.Execution;
using CodeLogic.Application.Flowcharts;

namespace CodeLogic.Web.Playground;

public enum TracePredictionKind
{
    VariableValue = 1,
    Branch = 2,
}

public sealed record TracePrediction(
    TracePredictionKind Kind,
    string Prompt,
    string ExpectedAnswer,
    string Explanation);

public sealed record TracePredictionResult(bool IsCorrect, string Feedback);

public static class TracePredictionEvaluator
{
    public static TracePrediction? Create(
        ExecutionTraceStep current,
        ExecutionTraceStep next,
        FlowGraph? graph)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        if (current.ActiveNodeType == FlowNodeType.Decision && graph is not null)
        {
            var takenEdge = graph.Edges.FirstOrDefault(edge =>
                edge.FromNodeId == current.ActiveNodeId && edge.ToNodeId == next.ActiveNodeId);
            if (takenEdge?.Type is FlowEdgeType.True or FlowEdgeType.False)
            {
                var answer = takenEdge.Type == FlowEdgeType.True ? "True" : "False";
                return new TracePrediction(
                    TracePredictionKind.Branch,
                    $"Which branch will '{current.ActiveNodeLabel}' take: True or False?",
                    answer,
                    $"The bounded trace follows the {answer} edge to '{next.ActiveNodeLabel}'.");
            }
        }

        var changedVariable = next.Variables
            .OrderBy(variable => variable.Key, StringComparer.Ordinal)
            .FirstOrDefault(variable => !current.Variables.TryGetValue(variable.Key, out var previousValue)
                || !string.Equals(previousValue, variable.Value, StringComparison.Ordinal));
        if (!string.IsNullOrEmpty(changedVariable.Key))
        {
            return new TracePrediction(
                TracePredictionKind.VariableValue,
                $"What will the value of {changedVariable.Key} be after the next step?",
                changedVariable.Value,
                $"The next bounded trace snapshot records {changedVariable.Key} = {changedVariable.Value}.");
        }

        return null;
    }

    public static TracePredictionResult Evaluate(TracePrediction prediction, string? learnerAnswer)
    {
        ArgumentNullException.ThrowIfNull(prediction);

        var actual = Normalize(learnerAnswer);
        var expected = Normalize(prediction.ExpectedAnswer);
        if (actual.Length == 0)
        {
            return new TracePredictionResult(false, "Enter a prediction before advancing the trace.");
        }

        var isCorrect = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        return new TracePredictionResult(
            isCorrect,
            isCorrect
                ? $"Correct. {prediction.Explanation}"
                : $"Not this time. The expected answer was {prediction.ExpectedAnswer}. {prediction.Explanation}");
    }

    private static string Normalize(string? answer) => answer?.Trim() ?? string.Empty;
}
