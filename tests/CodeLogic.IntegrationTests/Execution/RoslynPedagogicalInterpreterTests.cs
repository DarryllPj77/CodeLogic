using CodeLogic.Application.Execution;
using CodeLogic.Application.Flowcharts;
using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Execution;
using CodeLogic.Infrastructure.Flowcharts;

namespace CodeLogic.IntegrationTests.Execution;

public sealed class RoslynPedagogicalInterpreterTests
{
    [Fact]
    public async Task SequenceProducesDeterministicProgramCountersStateAndOutput()
    {
        const string source = """
            int score = 5;
            string learnerName = "Mina";
            score = score + 1;
            Console.WriteLine($"{learnerName}: {score}");
            """;
        var interpreter = CreateInterpreter();

        var first = await interpreter.TraceAsync(source);
        var second = await interpreter.TraceAsync(source);

        Assert.True(
            first.HasTrace,
            string.Join(" | ", first.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(ExecutionTraceStatus.Completed, first.Status);
        Assert.Equal(6, first.Steps.Count);
        Assert.Equal(
            first.Steps.Select(Snapshot),
            second.Steps.Select(Snapshot));

        var start = first.Steps[0];
        Assert.Equal(0, start.ProgramCounter);
        Assert.Equal(FlowNodeType.Start, start.ActiveNodeType);
        Assert.Empty(start.Variables);
        Assert.Empty(start.Output);

        var assignment = first.Steps[3];
        Assert.Equal(3, assignment.ProgramCounter);
        Assert.Equal("6", assignment.Variables["score"]);
        Assert.Equal("Mina", assignment.Variables["learnerName"]);

        var output = first.Steps[4];
        Assert.Equal(FlowNodeType.Output, output.ActiveNodeType);
        Assert.Equal(["Mina: 6"], output.Output);
        Assert.Equal(4, first.Steps[^1].ProgramCounter);
        Assert.Equal(FlowNodeType.End, first.Steps[^1].ActiveNodeType);
    }

    [Theory]
    [InlineData(8, "Pass", "score >= 5")]
    [InlineData(2, "Try again", "score >= 5")]
    public async Task IfElseSelectsOnlyTheCorrectBranch(
        int score,
        string expectedOutput,
        string decisionLabel)
    {
        var source = $$"""
            int score = {{score}};
            if (score >= 5)
            {
                Console.WriteLine("Pass");
            }
            else
            {
                Console.WriteLine("Try again");
            }
            """;

        var result = await CreateInterpreter().TraceAsync(source);

        Assert.Equal(ExecutionTraceStatus.Completed, result.Status);
        var decision = Assert.Single(result.Steps, step => step.ActiveNodeType == FlowNodeType.Decision);
        Assert.Equal(decisionLabel, decision.ActiveNodeLabel);
        Assert.Contains(score >= 5 ? "true" : "false", decision.Explanation, StringComparison.Ordinal);
        Assert.Equal([expectedOutput], result.Steps[^1].Output);
        Assert.DoesNotContain(
            result.Steps,
            step => step.ActiveNodeType == FlowNodeType.Output
                && step.ActiveNodeLabel.Contains(
                    expectedOutput == "Pass" ? "Try again" : "Pass",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task ForLoopTracesConditionBodyAndIteratorInOrder()
    {
        const string source = """
            for (int count = 1; count <= 3; count++)
            {
                Console.WriteLine(count);
            }
            """;

        var result = await CreateInterpreter().TraceAsync(source);

        Assert.Equal(ExecutionTraceStatus.Completed, result.Status);
        Assert.Equal(["1", "2", "3"], result.Steps[^1].Output);
        Assert.Equal("4", result.Steps[^1].Variables["count"]);
        Assert.Equal(
            ["true", "true", "true", "false"],
            result.Steps
                .Where(step => step.ActiveNodeType == FlowNodeType.Decision)
                .Select(step => step.Explanation.Contains("true", StringComparison.Ordinal) ? "true" : "false"));
    }

    [Fact]
    public async Task DeclaredDoubleUsesDoubleArithmeticAndLoopFormsRemainDeterministic()
    {
        const string source = """
            double half = 5 / 2.0;
            int count = 0;
            do
            {
                count++;
            }
            while (count < 2);
            while (count < 3)
            {
                count++;
            }
            Console.WriteLine(half);
            """;

        var result = await CreateInterpreter().TraceAsync(source);

        Assert.Equal(ExecutionTraceStatus.Completed, result.Status);
        Assert.Equal("2.5", result.Steps[^1].Variables["half"]);
        Assert.Equal("3", result.Steps[^1].Variables["count"]);
        Assert.Equal(["2.5"], result.Steps[^1].Output);
        Assert.Equal(
            4,
            result.Steps.Count(step => step.ActiveNodeType == FlowNodeType.Decision));
    }

    [Fact]
    public async Task InfiniteLookingLoopStopsAtOperationLimit()
    {
        var interpreter = CreateInterpreter(maximumOperations: 5);

        var result = await interpreter.TraceAsync("while (true) { }");

        Assert.Equal(ExecutionTraceStatus.StepLimitReached, result.Status);
        Assert.Equal(6, result.Steps.Count);
        Assert.Equal(5, result.Steps[^1].ProgramCounter);
        Assert.All(result.Steps.Skip(1), step => Assert.Equal(FlowNodeType.Decision, step.ActiveNodeType));
        Assert.Equal("ET0002", Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("string value = Console.ReadLine();", "ET1001")]
    [InlineData("int value;", "ET1001")]
    public async Task TraceSpecificUnsupportedInputReturnsNoPartialTrace(
        string source,
        string expectedCode)
    {
        var result = await CreateInterpreter().TraceAsync(source);

        Assert.Equal(ExecutionTraceStatus.Unsupported, result.Status);
        Assert.False(result.HasTrace);
        Assert.Empty(result.Steps);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
    }

    private static RoslynPedagogicalInterpreter CreateInterpreter(int maximumOperations = 100)
    {
        var analyzer = new RoslynCodeAnalysisService();
        var translator = new RoslynFlowchartTranslator(analyzer);
        return new RoslynPedagogicalInterpreter(
            translator,
            new ExecutionTraceLimits(maximumOperations));
    }

    private static string Snapshot(ExecutionTraceStep step) => string.Join(
        "|",
        step.ProgramCounter,
        step.ActiveNodeId,
        step.ActiveNodeType,
        step.ActiveNodeLabel,
        string.Join(",", step.Variables.Select(pair => $"{pair.Key}={pair.Value}")),
        string.Join(",", step.Output),
        step.Explanation);
}
