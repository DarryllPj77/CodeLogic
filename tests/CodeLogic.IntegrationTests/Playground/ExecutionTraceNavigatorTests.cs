using CodeLogic.Application.Execution;
using CodeLogic.Application.Flowcharts;
using CodeLogic.Web.Playground;

namespace CodeLogic.IntegrationTests.Playground;

public sealed class ExecutionTraceNavigatorTests
{
    [Fact]
    public void StepAdvancesUntilEndAndResetReturnsToInitialState()
    {
        var navigator = new ExecutionTraceNavigator(CompletedTrace());

        Assert.Equal(1, navigator.StepNumber);
        Assert.Equal(FlowNodeType.Start, navigator.Current.ActiveNodeType);
        Assert.True(navigator.CanStep);

        navigator.Step();
        Assert.Equal(2, navigator.StepNumber);
        Assert.Equal("1", navigator.Current.Variables["value"]);

        navigator.Step();
        navigator.Step();
        Assert.Equal(3, navigator.StepNumber);
        Assert.False(navigator.CanStep);

        navigator.Reset();
        Assert.Equal(1, navigator.StepNumber);
        Assert.Equal(0, navigator.Current.ProgramCounter);
        Assert.Empty(navigator.Current.Variables);
        Assert.Empty(navigator.Current.Output);
    }

    [Fact]
    public void StepBackReturnsToPreviousSnapshotAndStopsAtStart()
    {
        var navigator = new ExecutionTraceNavigator(CompletedTrace());

        Assert.False(navigator.CanStepBack);
        navigator.StepBack();
        Assert.Equal(1, navigator.StepNumber);

        navigator.Step();
        navigator.Step();
        Assert.True(navigator.CanStepBack);

        navigator.StepBack();
        Assert.Equal(2, navigator.StepNumber);
        Assert.Equal("action", navigator.Current.ActiveNodeId);
    }

    [Fact]
    public void ChangedVariablesReportsNewAndUpdatedValuesOnly()
    {
        var navigator = new ExecutionTraceNavigator(new ExecutionTraceResult(
            ExecutionTraceStatus.Completed,
            maximumOperations: 100,
            steps:
            [
                Step(0, "start", FlowNodeType.Start, []),
                Step(1, "action", FlowNodeType.Action, [KeyValuePair.Create("score", "5")]),
                Step(2, "action", FlowNodeType.Action,
                [
                    KeyValuePair.Create("score", "6"),
                    KeyValuePair.Create("name", "Mina"),
                ]),
                Step(3, "end", FlowNodeType.End,
                [
                    KeyValuePair.Create("score", "6"),
                    KeyValuePair.Create("name", "Mina"),
                ]),
            ]));

        navigator.Step();
        Assert.Equal(["score"], navigator.ChangedVariables);

        navigator.Step();
        Assert.Equal(["name", "score"], navigator.ChangedVariables.Order(StringComparer.Ordinal));

        navigator.Step();
        Assert.Empty(navigator.ChangedVariables);
    }

    [Fact]
    public void CurrentNodeVisitCountTracksRepeatedLoopNode()
    {
        var navigator = new ExecutionTraceNavigator(new ExecutionTraceResult(
            ExecutionTraceStatus.Completed,
            maximumOperations: 100,
            steps:
            [
                Step(0, "loop", FlowNodeType.Decision, []),
                Step(1, "body", FlowNodeType.Action, []),
                Step(2, "loop", FlowNodeType.Decision, []),
            ]));

        Assert.Equal(1, navigator.CurrentNodeVisitCount);
        navigator.Step();
        navigator.Step();
        Assert.Equal(2, navigator.CurrentNodeVisitCount);
    }

    [Fact]
    public void FailureResultCannotCreateNavigator()
    {
        var result = new ExecutionTraceResult(
            ExecutionTraceStatus.Unsupported,
            maximumOperations: 100);

        Assert.Throws<ArgumentException>(() => new ExecutionTraceNavigator(result));
    }

    private static ExecutionTraceResult CompletedTrace() => new(
        ExecutionTraceStatus.Completed,
        maximumOperations: 100,
        steps:
        [
            Step(0, "start", FlowNodeType.Start, []),
            Step(1, "action", FlowNodeType.Action, [KeyValuePair.Create("value", "1")]),
            Step(1, "end", FlowNodeType.End, [KeyValuePair.Create("value", "1")]),
        ]);

    private static ExecutionTraceStep Step(
        int counter,
        string id,
        FlowNodeType type,
        IEnumerable<KeyValuePair<string, string>> variables) => new(
            counter,
            id,
            type,
            id,
            variables,
            output: [],
            explanation: id);
}
