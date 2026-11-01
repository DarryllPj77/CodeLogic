using System.Text;

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
            schema 1.0
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
            schema 1.0
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
    public async Task NestedBranchReturnsPositionedDiagnosticWithoutPartialGraph()
    {
        const string source = """
            if (true)
            {
                if (false)
                {
                    Console.WriteLine("Never");
                }
            }
            """;

        var result = await translator.TranslateAsync(source);

        Assert.Equal(FlowchartTranslationStatus.Unsupported, result.Status);
        Assert.Null(result.Graph);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("FL1001", diagnostic.Code);
        Assert.Equal(3, diagnostic.Line);
        Assert.Equal(5, diagnostic.Column);
        Assert.Contains("Nested if/else", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidButUnimplementedLoopReturnsErrorInsteadOfThrowing()
    {
        const string source = "while (true) { Console.WriteLine(\"Again\"); }";

        var result = await translator.TranslateAsync(source);

        Assert.Equal(FlowchartTranslationStatus.Unsupported, result.Status);
        Assert.Null(result.Graph);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("FL1002", diagnostic.Code);
        Assert.Equal(1, diagnostic.Line);
        Assert.Contains("WhileStatement", diagnostic.Message, StringComparison.Ordinal);
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
