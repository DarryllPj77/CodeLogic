using System.Text;

using CodeLogic.Application.Analysis;
using CodeLogic.Application.Flowcharts;
using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Flowcharts;

namespace CodeLogic.IntegrationTests.Flowcharts;

public sealed class RoslynFlowchartTranslatorTests
{
    private readonly RoslynFlowchartTranslator translator = new(
        new RoslynCodeAnalysisService());

    [Fact]
    public async Task SimpleSequenceMatchesGoldenGraph()
    {
        const string source = """
            string name = Console.ReadLine();
            string greeting = "Hello " + name;
            Console.WriteLine(greeting);
            """;

        var result = await translator.TranslateAsync(source);

        Assert.Equal(FlowchartTranslationStatus.Success, result.Status);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            """
            schema 1.1
            node node-000 Start | Start
            node node-001 Input | string name = Console.ReadLine();
            node node-002 Action | string greeting = "Hello " + name;
            node node-003 Output | Console.WriteLine(greeting);
            node node-004 End | End
            edge edge-000 node-000 -> node-001 Next
            edge edge-001 node-001 -> node-002 Next
            edge edge-002 node-002 -> node-003 Next
            edge edge-003 node-003 -> node-004 Next
            """,
            Golden(result.Graph!));

        var inputSpan = result.Graph!.Nodes[1].SourceSpan;
        Assert.NotNull(inputSpan);
        Assert.Equal(1, inputSpan.StartLine);
        Assert.Equal(1, inputSpan.StartColumn);
        Assert.Equal(source.IndexOf("string name", StringComparison.Ordinal), inputSpan.StartOffset);
        Assert.Equal("string name = Console.ReadLine();".Length, inputSpan.Length);
    }

    [Fact]
    public async Task IfElseMatchesGoldenGraphAndMergesIntoNextAction()
    {
        const string source = """
            int score = 6;
            if (score >= 5)
            {
                Console.WriteLine("Pass");
            }
            else
            {
                Console.WriteLine("Try again");
            }
            Console.WriteLine("Done");
            """;

        var result = await translator.TranslateAsync(source);

        Assert.Equal(FlowchartTranslationStatus.Success, result.Status);
        Assert.Equal(
            """
            schema 1.1
            node node-000 Start | Start
            node node-001 Action | int score = 6;
            node node-002 Decision | score >= 5
            node node-003 Output | Console.WriteLine("Pass");
            node node-004 Output | Console.WriteLine("Try again");
            node node-005 Output | Console.WriteLine("Done");
            node node-006 End | End
            edge edge-000 node-000 -> node-001 Next
            edge edge-001 node-001 -> node-002 Next
            edge edge-002 node-002 -> node-003 True | True
            edge edge-003 node-002 -> node-004 False | False
            edge edge-004 node-003 -> node-005 Next
            edge edge-005 node-004 -> node-005 Next
            edge edge-006 node-005 -> node-006 Next
            """,
            Golden(result.Graph!));
    }

    [Fact]
    public async Task SameSourceProducesSameGraphAndStableIds()
    {
        const string source = "int value = 1;\nConsole.WriteLine(value);";

        var first = await translator.TranslateAsync(source);
        var second = await translator.TranslateAsync(source);

        Assert.Equal(Golden(first.Graph!), Golden(second.Graph!));
    }

    [Fact]
    public async Task NestedBranchMatchesGoldenGraphAndMergesBothDecisions()
    {
        const string source = """
            if (score >= 5)
            {
                if (hasBonus)
                {
                    Console.WriteLine("Excellent");
                }
                else
                {
                    Console.WriteLine("Pass");
                }
            }
            else
            {
                Console.WriteLine("Try again");
            }
            Console.WriteLine("Done");
            """;

        var result = await translator.TranslateAsync(source);

        Assert.Equal(FlowchartTranslationStatus.Success, result.Status);
        Assert.Equal(
            """
            schema 1.1
            node node-000 Start | Start
            node node-001 Decision | score >= 5
            node node-002 Decision | hasBonus
            node node-003 Output | Console.WriteLine("Excellent");
            node node-004 Output | Console.WriteLine("Pass");
            node node-005 Output | Console.WriteLine("Try again");
            node node-006 Output | Console.WriteLine("Done");
            node node-007 End | End
            edge edge-000 node-000 -> node-001 Next
            edge edge-001 node-001 -> node-002 True | True
            edge edge-002 node-002 -> node-003 True | True
            edge edge-003 node-002 -> node-004 False | False
            edge edge-004 node-001 -> node-005 False | False
            edge edge-005 node-003 -> node-006 Next
            edge edge-006 node-004 -> node-006 Next
            edge edge-007 node-005 -> node-006 Next
            edge edge-008 node-006 -> node-007 Next
            """,
            Golden(result.Graph!));
    }

    [Fact]
    public async Task WhileLoopMatchesGoldenGraphWithExitAndBackEdge()
    {
        const string source = """
            int count = 0;
            while (count < 3)
            {
                Console.WriteLine(count);
                count++;
            }
            Console.WriteLine("Done");
            """;

        var result = await translator.TranslateAsync(source);

        Assert.Equal(FlowchartTranslationStatus.Success, result.Status);
        Assert.Equal(
            """
            schema 1.1
            node node-000 Start | Start
            node node-001 Action | int count = 0;
            node node-002 Decision | count < 3
            node node-003 Output | Console.WriteLine(count);
            node node-004 Action | count++;
            node node-005 Output | Console.WriteLine("Done");
            node node-006 End | End
            edge edge-000 node-000 -> node-001 Next
            edge edge-001 node-001 -> node-002 Next
            edge edge-002 node-002 -> node-003 True | True
            edge edge-003 node-003 -> node-004 Next
            edge edge-004 node-004 -> node-002 LoopBack | Repeat
            edge edge-005 node-002 -> node-005 False | False
            edge edge-006 node-005 -> node-006 Next
            """,
            Golden(result.Graph!));
    }

    [Fact]
    public async Task ForLoopMatchesGoldenGraphWithInitializerIteratorAndBackEdge()
    {
        const string source = """
            for (int index = 0; index < 3; index++)
            {
                Console.WriteLine(index);
            }
            Console.WriteLine("Done");
            """;

        var result = await translator.TranslateAsync(source);

        Assert.Equal(FlowchartTranslationStatus.Success, result.Status);
        Assert.Equal(
            """
            schema 1.1
            node node-000 Start | Start
            node node-001 Action | for init: int index = 0
            node node-002 Decision | index < 3
            node node-003 Output | Console.WriteLine(index);
            node node-004 Action | for iterator: index++
            node node-005 Output | Console.WriteLine("Done");
            node node-006 End | End
            edge edge-000 node-000 -> node-001 Next
            edge edge-001 node-001 -> node-002 Next
            edge edge-002 node-002 -> node-003 True | True
            edge edge-003 node-003 -> node-004 Next
            edge edge-004 node-004 -> node-002 LoopBack | Repeat
            edge edge-005 node-002 -> node-005 False | False
            edge edge-006 node-005 -> node-006 Next
            """,
            Golden(result.Graph!));
    }

    [Fact]
    public async Task DoWhileLoopMatchesGoldenGraphWithBodyBeforeDecision()
    {
        const string source = """
            int count = 0;
            do
            {
                Console.WriteLine(count);
                count++;
            }
            while (count < 3);
            Console.WriteLine("Done");
            """;

        var result = await translator.TranslateAsync(source);

        Assert.Equal(FlowchartTranslationStatus.Success, result.Status);
        Assert.Equal(
            """
            schema 1.1
            node node-000 Start | Start
            node node-001 Action | int count = 0;
            node node-002 Output | Console.WriteLine(count);
            node node-003 Action | count++;
            node node-004 Decision | count < 3
            node node-005 Output | Console.WriteLine("Done");
            node node-006 End | End
            edge edge-000 node-000 -> node-001 Next
            edge edge-001 node-001 -> node-002 Next
            edge edge-002 node-002 -> node-003 Next
            edge edge-003 node-003 -> node-004 Next
            edge edge-004 node-004 -> node-002 LoopBack | True · repeat
            edge edge-005 node-004 -> node-005 False | False
            edge edge-006 node-005 -> node-006 Next
            """,
            Golden(result.Graph!));
    }

    [Theory]
    [InlineData("while (true) { }", "true")]
    [InlineData("for (;;) { }", "true (implicit for condition)")]
    public async Task InfiniteLoopLookingSyntaxRetainsStructuralExitAndRepeatEdge(
        string source,
        string decisionLabel)
    {
        var result = await translator.TranslateAsync(source);

        Assert.Equal(FlowchartTranslationStatus.Success, result.Status);
        var graph = result.Graph!;
        var decision = Assert.Single(graph.Nodes, node => node.Type == FlowNodeType.Decision);
        Assert.Equal(decisionLabel, decision.Label);
        Assert.Contains(graph.Edges, edge =>
            edge.FromNodeId == decision.Id
            && edge.ToNodeId == decision.Id
            && edge.Type == FlowEdgeType.LoopBack);
        Assert.Contains(graph.Edges, edge =>
            edge.FromNodeId == decision.Id
            && edge.Type == FlowEdgeType.False
            && graph.Nodes.Single(node => node.Id == edge.ToNodeId).Type == FlowNodeType.End);
    }

    [Theory]
    [InlineData("while (true) { break; }", "CL1006")]
    [InlineData("for (;;) { continue; }", "CL1007")]
    public async Task BreakAndContinueAreExplicitlyUnsupported(string source, string expectedCode)
    {
        var result = await translator.TranslateAsync(source);

        Assert.Equal(FlowchartTranslationStatus.Unsupported, result.Status);
        Assert.Null(result.Graph);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    [Fact]
    public async Task MalformedSourceReturnsRoslynErrorsInsteadOfThrowing()
    {
        const string source = "if (true { Console.WriteLine(\"Missing parenthesis\"); }";

        var result = await translator.TranslateAsync(source);

        Assert.Equal(FlowchartTranslationStatus.SyntaxError, result.Status);
        Assert.Null(result.Graph);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.StartsWith("CS", StringComparison.Ordinal)
            && diagnostic.Line == 1
            && diagnostic.Column is > 0);
    }

    [Theory]
    [InlineData(null, FlowchartTranslationStatus.InvalidInput, "CL0001")]
    [InlineData("class Program { }", FlowchartTranslationStatus.Unsupported, "CL1001")]
    public async Task AnalysisFailuresReturnDiagnosticsWithoutGraph(
        string? source,
        FlowchartTranslationStatus expectedStatus,
        string expectedCode)
    {
        var result = await translator.TranslateAsync(source);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Graph);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    [Fact]
    public async Task MaximumLengthSourceTranslatesWithinTheAnalysisTimeBudget()
    {
        var source = "int value = 1;".PadRight(CodeAnalysisDefaults.MaximumCharacters);
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var result = await translator.TranslateAsync(source, cancellationSource.Token);

        Assert.Equal(10_000, source.Length);
        Assert.Equal(FlowchartTranslationStatus.Success, result.Status);
        Assert.NotNull(result.Graph);
    }

    private static string Golden(FlowGraph graph)
    {
        var builder = new StringBuilder();
        builder.Append("schema ").Append(graph.SchemaVersion).Append('\n');
        foreach (var node in graph.Nodes)
        {
            builder.Append("node ")
                .Append(node.Id)
                .Append(' ')
                .Append(node.Type.ToString())
                .Append(" | ")
                .Append(node.Label)
                .Append('\n');
        }

        foreach (var edge in graph.Edges)
        {
            builder.Append("edge ")
                .Append(edge.Id)
                .Append(' ')
                .Append(edge.FromNodeId)
                .Append(" -> ")
                .Append(edge.ToNodeId)
                .Append(' ')
                .Append(edge.Type.ToString());
            if (edge.Label is not null)
            {
                builder.Append(" | ").Append(edge.Label);
            }

            builder.Append('\n');
        }

        return builder.ToString().TrimEnd();
    }
}
