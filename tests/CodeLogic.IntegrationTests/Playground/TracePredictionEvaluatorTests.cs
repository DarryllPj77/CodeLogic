using CodeLogic.Application.Execution;
using CodeLogic.Application.Flowcharts;
using CodeLogic.Web.Playground;

namespace CodeLogic.IntegrationTests.Playground;

public sealed class TracePredictionEvaluatorTests
{
    [Fact]
    public void CreatesBranchPredictionFromTakenGraphEdge()
    {
        var graph = new FlowGraph(
            [Node("start", FlowNodeType.Start), Node("decision", FlowNodeType.Decision), Node("pass", FlowNodeType.Output), Node("retry", FlowNodeType.Output), Node("end", FlowNodeType.End)],
            [
                new FlowEdge("start-edge", "start", "decision", FlowEdgeType.Next),
                new FlowEdge("true-edge", "decision", "pass", FlowEdgeType.True),
                new FlowEdge("false-edge", "decision", "retry", FlowEdgeType.False),
                new FlowEdge("pass-end", "pass", "end", FlowEdgeType.Next),
                new FlowEdge("retry-end", "retry", "end", FlowEdgeType.Next),
            ]);

        var prediction = TracePredictionEvaluator.Create(
            Step("decision", FlowNodeType.Decision, "score >= 5"),
            Step("pass", FlowNodeType.Output, "Pass"),
            graph);

        Assert.NotNull(prediction);
        Assert.Equal(TracePredictionKind.Branch, prediction.Kind);
        Assert.Equal("True", prediction.ExpectedAnswer);
    }

    [Fact]
    public void CreatesVariablePredictionForChangedValue()
    {
        var prediction = TracePredictionEvaluator.Create(
            Step("before", FlowNodeType.Action, "score++", ("score", "5")),
            Step("after", FlowNodeType.Action, "next", ("score", "6")),
            graph: null);

        Assert.NotNull(prediction);
        Assert.Equal(TracePredictionKind.VariableValue, prediction.Kind);
        Assert.Equal("6", prediction.ExpectedAnswer);
    }

    [Theory]
    [InlineData(" true ")]
    [InlineData("TRUE")]
    public void AcceptsNormalizedExactPrediction(string answer)
    {
        var prediction = new TracePrediction(TracePredictionKind.Branch, "Branch?", "True", "Taken.");

        var result = TracePredictionEvaluator.Evaluate(prediction, answer);

        Assert.True(result.IsCorrect);
        Assert.Contains("Correct", result.Feedback, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsIncorrectPredictionWithExpectedAnswer()
    {
        var prediction = new TracePrediction(TracePredictionKind.VariableValue, "Value?", "6", "Updated.");

        var result = TracePredictionEvaluator.Evaluate(prediction, "5");

        Assert.False(result.IsCorrect);
        Assert.Contains("expected answer was 6", result.Feedback, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReturnsNoPredictionWhenNextStepHasNoBranchOrVariableChange()
    {
        var prediction = TracePredictionEvaluator.Create(
            Step("first", FlowNodeType.Action, "work", ("score", "5")),
            Step("second", FlowNodeType.Output, "print", ("score", "5")),
            graph: null);

        Assert.Null(prediction);
    }

    private static FlowNode Node(string id, FlowNodeType type) => new(id, type, id, sourceSpan: null);

    private static ExecutionTraceStep Step(
        string id,
        FlowNodeType type,
        string label,
        params (string Name, string Value)[] variables) => new(
            programCounter: 0,
            id,
            type,
            label,
            variables.Select(variable => KeyValuePair.Create(variable.Name, variable.Value)),
            output: [],
            explanation: "Test step.");
}
