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
