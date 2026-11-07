using CodeLogic.Application.Analysis;
using CodeLogic.Application.Flowcharts;
using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Flowcharts;
using CodeLogic.Web.Playground;

namespace CodeLogic.IntegrationTests.Playground;

public sealed class TraceBranchHighlighterTests
{
    private readonly RoslynFlowchartTranslator translator = new(new RoslynCodeAnalysisService());

    [Fact]
    public async Task TakingTheTruePathDimsTheElseBranchButNotTheCodeAfterIt()
    {
        var graph = await Graph("""
            int age = 20;
            if (age >= 18)
            {
                Console.WriteLine("Adult");
            }
            else
            {
                Console.WriteLine("Minor");
                Console.WriteLine("Ask a parent");
            }

            Console.WriteLine("Done");
            """);

        var skipped = Skipped(graph, "age >= 18", "Console.WriteLine(\"Adult\");");

        Assert.Equal(["Console.WriteLine(\"Ask a parent\");", "Console.WriteLine(\"Minor\");"], skipped);
    }

    [Fact]
    public async Task EnteringALoopBodySkipsNothingAndLeavingItDimsTheBody()
    {
        var graph = await Graph("""
            int count = 0;
            while (count < 2)
            {
                count++;
            }

            Console.WriteLine(count);
            """);

        Assert.Empty(Skipped(graph, "count < 2", "count++;"));
        Assert.Equal(["count++;"], Skipped(graph, "count < 2", "Console.WriteLine(count);"));
    }

    [Fact]
    public async Task AMatchedSwitchCaseDimsTheOtherCasesAndDefault()
    {
        var graph = await Graph("""
            int day = 1;
            switch (day)
            {
                case 1:
                    Console.WriteLine("Monday");
                    break;
                case 2:
                    Console.WriteLine("Tuesday");
                    break;
                default:
                    Console.WriteLine("Other");
                    break;
            }

            Console.WriteLine("End of week check");
            """);

        var skipped = Skipped(graph, "day == 1", "Console.WriteLine(\"Monday\");");

        Assert.Equal(["Console.WriteLine(\"Other\");", "Console.WriteLine(\"Tuesday\");", "day == 2"], skipped);
    }

    [Fact]
    public async Task StepsThatDoNotLeaveADecisionSkipNothing()
    {
        var graph = await Graph("int a = 1;\nint b = 2;");

        Assert.Empty(TraceBranchHighlighter.SkippedNodeIds(graph, Id(graph, "int a = 1;"), Id(graph, "int b = 2;")));
        Assert.Empty(TraceBranchHighlighter.SkippedNodeIds(graph, null, Id(graph, "int a = 1;")));
    }

    private async Task<FlowGraph> Graph(string mainBody) =>
        (await translator.TranslateAsync(ProgramTemplates.Classic(mainBody))).Graph!;

    private static string Id(FlowGraph graph, string label) => graph.Nodes.Single(node => node.Label == label).Id;

    private static string[] Skipped(FlowGraph graph, string decision, string taken) =>
        [.. TraceBranchHighlighter.SkippedNodeIds(graph, Id(graph, decision), Id(graph, taken))
            .Select(id => graph.Nodes.Single(node => node.Id == id).Label)
            .Order(StringComparer.Ordinal)];
}
