using CodeLogic.Application.Analysis;
using CodeLogic.Application.Execution;
using CodeLogic.Application.Flowcharts;
using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Execution;
using CodeLogic.Infrastructure.Flowcharts;

namespace CodeLogic.IntegrationTests.Flowcharts;

/// <summary>Phase 1: break, continue, switch, and return inside loops.</summary>
public sealed class JumpAndSwitchTests
{
    private readonly RoslynCodeAnalysisService analysis = new();
    private readonly RoslynFlowchartTranslator translator;
    private readonly RoslynPedagogicalInterpreter interpreter;
    private readonly MermaidFlowchartSerializer serializer = new();

    public JumpAndSwitchTests()
    {
        translator = new RoslynFlowchartTranslator(analysis);
        interpreter = new RoslynPedagogicalInterpreter(translator);
    }

    [Fact]
    public async Task BreakDrawsAThickEdgeToTheStatementAfterTheLoopAndStopsTheTrace()
    {
        var source = ProgramTemplates.Classic("""
            int count = 0;
            while (count < 10)
            {
                count++;
                if (count == 3)
                {
                    break;
                }
            }

            Console.WriteLine(count);
            """);

        var graph = (await translator.TranslateAsync(source)).Graph!;
        var trace = await interpreter.TraceAsync(source);

        var breakNode = Assert.Single(graph.Nodes, node => node.Label == "break");
        var jump = Assert.Single(graph.Edges, edge => edge.Type == FlowEdgeType.Jump);
        Assert.Equal(breakNode.Id, jump.FromNodeId);
        Assert.Equal("break", jump.Label);
        Assert.Equal("Console.WriteLine(count);", Node(graph, jump.ToNodeId).Label);
        Assert.Contains(" ==>", serializer.Serialize(graph).Definition, StringComparison.Ordinal);

        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal(["3"], trace.Steps[^1].Output);
        Assert.Contains(trace.Steps, step => step.ActiveNodeId == breakNode.Id);
    }

    [Fact]
    public async Task ContinueInAForLoopStillRunsTheIterator()
    {
        var source = SupportedFeatureRegistry.Get(SupportedFeatureIds.Continue).Example;

        var graph = (await translator.TranslateAsync(source)).Graph!;
        var trace = await interpreter.TraceAsync(source);

        var jump = Assert.Single(graph.Edges, edge => edge.Type == FlowEdgeType.Jump);
        Assert.Equal("continue", jump.Label);
        Assert.Equal("for iterator: number++", Node(graph, jump.ToNodeId).Label);
        Assert.Equal(["0", "1", "3", "4"], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task ContinueInAWhileLoopJumpsBackToTheCondition()
    {
        var source = ProgramTemplates.Classic("""
            int number = 0;
            while (number < 4)
            {
                number++;
                if (number == 2)
                {
                    continue;
                }

                Console.WriteLine(number);
            }
            """);

        var graph = (await translator.TranslateAsync(source)).Graph!;
        var trace = await interpreter.TraceAsync(source);

        var jump = Assert.Single(graph.Edges, edge => edge.Type == FlowEdgeType.Jump);
        Assert.Equal("number < 4", Node(graph, jump.ToNodeId).Label);
        Assert.Equal(["1", "3", "4"], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task BreakInANestedLoopLeavesOnlyTheInnerLoop()
    {
        var source = ProgramTemplates.Classic("""
            for (int row = 1; row <= 2; row++)
            {
                for (int column = 1; column <= 3; column++)
                {
                    if (column == 2)
                    {
                        break;
                    }

                    Console.WriteLine($"{row},{column}");
                }
            }
            """);

        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal(["1,1", "2,1"], trace.Steps[^1].Output);
    }

    [Theory]
    [InlineData(1, "Monday")]
    [InlineData(2, "Midweek")]
    [InlineData(3, "Midweek")]
    [InlineData(9, "Another day")]
    public async Task SwitchRunsTheMatchingCaseOrDefault(int day, string expected)
    {
        var source = SupportedFeatureRegistry.Get(SupportedFeatureIds.Switch).Example
            .Replace("int day = 2;", $"int day = {day};", StringComparison.Ordinal);

        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal([expected], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task SwitchIsDrawnAsAValueStepAndAChainOfEqualityDecisions()
    {
        var source = SupportedFeatureRegistry.Get(SupportedFeatureIds.Switch).Example;

        var graph = (await translator.TranslateAsync(source)).Graph!;
        var trace = await interpreter.TraceAsync(source);

        Assert.Contains(graph.Nodes, node => node.Type == FlowNodeType.Action && node.Label == "switch (day)");
        var decisions = graph.Nodes.Where(node => node.Type == FlowNodeType.Decision).Select(node => node.Label).ToArray();
        Assert.Equal(["day == 1", "day == 2 || day == 3"], decisions);
        Assert.DoesNotContain(graph.Nodes, node => node.Label == "break");
        Assert.Contains(trace.Steps, step => step.Explanation == "The switch value is 2.");
        Assert.Contains(trace.Steps, step => step.Explanation == "2 does not match case 1.");
        Assert.Contains(trace.Steps, step => step.Explanation == "2 matches case 2 or 3, so this case runs.");
    }

    [Fact]
    public async Task ASwitchWithoutDefaultAndNoMatchDoesNothing()
    {
        var source = ProgramTemplates.Classic("""
            int code = 5;
            switch (code)
            {
                case 1:
                    Console.WriteLine("one");
                    break;
            }

            Console.WriteLine("after");
            """);

        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(["after"], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task BreakInsideASwitchLeavesOnlyTheSwitchAndContinueReachesTheLoop()
    {
        var source = ProgramTemplates.Classic("""
            for (int number = 1; number <= 4; number++)
            {
                switch (number)
                {
                    case 2:
                        continue;
                    case 3:
                        Console.WriteLine("three");
                        break;
                    default:
                        Console.WriteLine(number);
                        break;
                }

                Console.WriteLine("end of repetition");
            }
            """);

        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal(
            ["1", "end of repetition", "three", "end of repetition", "4", "end of repetition"],
            trace.Steps[^1].Output);
    }

    [Fact]
    public async Task ReturnInsideAWhileLoopEndsTheMethodAndConnectsToItsReturnNode()
    {
        var source = SupportedFeatureRegistry.Get(SupportedFeatureIds.ReturnInLoop).Example;

        var graph = (await translator.TranslateAsync(source)).Graph!;

        var returnNode = Assert.Single(graph.Nodes, node => node.Label == "return number;");
        var end = Assert.Single(graph.Nodes, node => node.Scope == "FirstEven" && node.Type == FlowNodeType.End);
        Assert.Contains(graph.Edges, edge => edge.FromNodeId == returnNode.Id && edge.ToNodeId == end.Id);
    }

    [Theory]
    [InlineData("int day = 1;\nswitch (day)\n{\n    case 1:\n        Console.WriteLine(1);\n}", "must end with break")]
    [InlineData("int day = 1;\nswitch (day)\n{\n    case > 0:\n        break;\n}", "literal value")]
    [InlineData("int day = 1;\nint other = 2;\nswitch (day)\n{\n    case other:\n        break;\n}", "literal value")]
    public async Task UnsupportedSwitchShapesExplainWhy(string body, string expectedMessagePart)
    {
        var result = await analysis.AnalyzeAsync(ProgramTemplates.Classic(body));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains(expectedMessagePart, diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(SupportedFeatureIds.Switch, diagnostic.HelpTopicId);
    }

    [Fact]
    public async Task ASwitchCountsAsOneLevelOfNesting()
    {
        var source = ProgramTemplates.Classic("""
            int value = 1;
            while (value < 2)
            {
                switch (value)
                {
                    case 1:
                        if (value > 0)
                        {
                            for (int index = 0; index < 1; index++)
                            {
                                value++;
                            }
                        }

                        break;
                }
            }
            """);

        var result = await analysis.AnalyzeAsync(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CL1004"
            && diagnostic.Message == "This code is nested 4 levels deep; the limit is 3.");
    }

    [Fact]
    public async Task BreakCannotLeaveAMethodThatWasCalledFromALoop()
    {
        var source = ProgramTemplates.Classic(
            "for (int index = 0; index < 2; index++)\n{\n    Stop();\n}",
            "static void Stop()\n{\n    break;\n}");

        var result = await analysis.AnalyzeAsync(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CL1006");
    }

    private static FlowNode Node(FlowGraph graph, string id) => graph.Nodes.Single(node => node.Id == id);
}
