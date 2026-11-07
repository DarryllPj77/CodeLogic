using CodeLogic.Application.Analysis;
using CodeLogic.Application.Execution;
using CodeLogic.Application.Flowcharts;
using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Execution;
using CodeLogic.Infrastructure.Flowcharts;
using CodeLogic.Web.Playground;

namespace CodeLogic.IntegrationTests.Analysis;

public sealed class ClassicProgramTests
{
    private readonly RoslynCodeAnalysisService analysis = new();
    private readonly RoslynFlowchartTranslator translator;
    private readonly RoslynPedagogicalInterpreter interpreter;
    private readonly RoslynProgramStructureService structure = new();

    public ClassicProgramTests()
    {
        translator = new RoslynFlowchartTranslator(analysis);
        interpreter = new RoslynPedagogicalInterpreter(translator);
    }

    [Fact]
    public async Task ClassicHelloWorldPassesSyntaxAndProducesAFlowchartFromMain()
    {
        var result = await analysis.AnalyzeAsync(ProgramTemplates.ClassicHelloWorld);
        var translation = await translator.TranslateAsync(ProgramTemplates.ClassicHelloWorld);
        var trace = await interpreter.TraceAsync(ProgramTemplates.ClassicHelloWorld);

        Assert.Equal(CodeAnalysisStatus.Valid, result.Status);
        Assert.Empty(result.Diagnostics);
        Assert.True(translation.IsSuccess);
        Assert.Equal(
            [FlowNodeType.Start, FlowNodeType.Output, FlowNodeType.End],
            translation.Graph!.Nodes.Select(node => node.Type));
        Assert.Equal("Console.WriteLine(\"Hello World!\");", translation.Graph.Nodes[1].Label);
        Assert.Equal("1.2", translation.Graph.SchemaVersion);
        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal(["Hello World!"], trace.Steps[^1].Output);
    }

    [Theory]
    [InlineData("using System;\nnamespace MyProgram\n{\n    class Program\n    {\n        static void Main(string[] args)\n        {\n            Console.WriteLine(1);\n        }\n    }\n}")]
    [InlineData("using System;\nnamespace MyProgram;\nclass Program\n{\n    static void Main(string[] args)\n    {\n        Console.WriteLine(1);\n    }\n}")]
    [InlineData("class Program\n{\n    static void Main()\n    {\n        Console.WriteLine(1);\n    }\n}")]
    [InlineData("using System;\nclass Program\n{\n    static int Main(string[] args)\n    {\n        Console.WriteLine(1);\n        return 0;\n    }\n}")]
    [InlineData("using System;\nusing System.Text;\npublic static class Program\n{\n    public static void Main(string[] args)\n    {\n        Console.WriteLine(1);\n    }\n}")]
    public async Task UsingNamespaceAndClassAreAcceptedAroundASupportedMain(string source)
    {
        var result = await analysis.AnalyzeAsync(source);
        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(CodeAnalysisStatus.Valid, result.Status);
        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal(["1"], trace.Steps[^1].Output);
        Assert.Equal(ProgramForm.Classic, structure.DetectForm(source));
    }

    [Fact]
    public async Task IntMainReturnValueEndsTheProgram()
    {
        const string source = "class Program\n{\n    static int Main(string[] args)\n    {\n        return 0;\n    }\n}";

        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Contains("Main returned 0", trace.Steps[^1].Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AClassWithoutMainExplainsWhereTheProgramShouldStart()
    {
        const string source = "using System;\nnamespace MyProgram\n{\n    class Program\n    {\n        static void Run()\n        {\n        }\n    }\n}";

        var result = await analysis.AnalyzeAsync(source);

        var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == "CL1008");
        Assert.Equal("No Main method was found.", diagnostic.Message);
        Assert.Contains("static void Main(string[] args)", diagnostic.Suggestion, StringComparison.Ordinal);
        Assert.Equal(SupportedFeatureIds.Main, diagnostic.HelpTopicId);
    }

    [Fact]
    public async Task TwoMainMethodsReportOneEntryPointRule()
    {
        const string source = "class Program\n{\n    static void Main(string[] args)\n    {\n    }\n\n    static void Main()\n    {\n    }\n}";

        var result = await analysis.AnalyzeAsync(source);

        Assert.Equal(CodeAnalysisStatus.Unsupported, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == "CL1009");
        Assert.Equal("There are 2 Main methods.", diagnostic.Message);
        Assert.Equal(7, diagnostic.Line);
    }

    [Fact]
    public async Task MixingTopLevelStatementsAndMainIsRejected()
    {
        const string source = "Console.WriteLine(1);\nclass Program\n{\n    static void Main()\n    {\n    }\n}";

        var result = await analysis.AnalyzeAsync(source);

        Assert.Equal(ProgramForm.Mixed, structure.DetectForm(source));
        Assert.Contains(result.Diagnostics, item => item.Code == "CL1010" && item.Line == 1);
    }

    [Fact]
    public async Task SwitchDiagnosticSaysWhatWhyAndWhatToDoInstead()
    {
        var source = ProgramTemplates.Classic("int day = 1;\nswitch (day)\n{\n    default:\n        break;\n}");

        var result = await analysis.AnalyzeAsync(source);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CL1001", diagnostic.Code);
        Assert.Equal("switch is not supported yet.", diagnostic.Message);
        Assert.Equal("Use if/else if/else or a small method instead.", diagnostic.Suggestion);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Explanation));
        Assert.Equal(SupportedFeatureIds.Switch, diagnostic.HelpTopicId);
    }

    [Fact]
    public async Task LocalFunctionDiagnosticOffersTheStaticMethodRewrite()
    {
        var source = SupportedFeatureRegistry.Get(SupportedFeatureIds.LocalFunctions).Example;

        var result = await analysis.AnalyzeAsync(source);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("FL1002", diagnostic.Code);
        Assert.Equal("Local functions are not supported yet.", diagnostic.Message);
        Assert.Contains("static method", diagnostic.Suggestion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DepthDiagnosticShowsTheActualDepthAndSuggestsExtractingAMethod()
    {
        var source = ProgramTemplates.Classic("""
            int a = 1;
            if (a > 0)
            {
                while (a < 3)
                {
                    for (int b = 0; b < 1; b++)
                    {
                        if (b == 0)
                        {
                            a++;
                        }
                    }
                }
            }
            """);

        var result = await analysis.AnalyzeAsync(source);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CL1004", diagnostic.Code);
        Assert.Equal("This code is nested 4 levels deep; the limit is 3.", diagnostic.Message);
        Assert.Contains("static method", diagnostic.Suggestion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnElseIfChainDoesNotCountAsDeeperNesting()
    {
        var source = ProgramTemplates.Classic("""
            int score = 70;
            if (score > 90) { Console.WriteLine("A"); }
            else if (score > 80) { Console.WriteLine("B"); }
            else if (score > 70) { Console.WriteLine("C"); }
            else if (score > 60) { Console.WriteLine("D"); }
            else { Console.WriteLine("F"); }
            """);

        var result = await analysis.AnalyzeAsync(source);

        Assert.Equal(CodeAnalysisStatus.Valid, result.Status);
    }

    [Fact]
    public async Task StaticMethodsGetScopedSubFlowchartsCallEdgesAndTracedFrames()
    {
        var source = SupportedFeatureRegistry.Get(SupportedFeatureIds.StaticMethods).Example;

        var translation = await translator.TranslateAsync(source);
        var trace = await interpreter.TraceAsync(source);

        Assert.True(translation.IsSuccess);
        var graph = translation.Graph!;
        Assert.Equal(["Area", "Greet"], graph.Nodes.Select(node => node.Scope).OfType<string>().Distinct());
        Assert.Equal(2, graph.Edges.Count(edge => edge.Type == FlowEdgeType.Call));
        Assert.Contains(graph.Nodes, node => node.Type == FlowNodeType.Start && node.Label == "Start: Area(int width, int height)");
        Assert.Equal(2, graph.Nodes.Count(node => node.Type == FlowNodeType.Call));

        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal(["Area: 12", "Hello, Ada!"], trace.Steps[^1].Output);
        var entered = Assert.Single(trace.Steps, step => step.Explanation.StartsWith("Called Area with", StringComparison.Ordinal));
        Assert.Contains("width = 3, height = 4", entered.Explanation, StringComparison.Ordinal);
        Assert.Equal(["height", "width"], entered.Variables.Select(variable => variable.Key));
        Assert.Contains(trace.Steps, step => step.Explanation == "Area returned 12 to the caller.");
    }

    [Fact]
    public async Task ReturnInsideAnIfLeavesTheMethodWhileReturnInsideALoopIsRejected()
    {
        var allowed = ProgramTemplates.Classic(
            "Console.WriteLine(Classify(20));",
            "static string Classify(int age)\n{\n    if (age >= 18)\n    {\n        return \"Adult\";\n    }\n\n    return \"Minor\";\n}");
        var rejected = SupportedFeatureRegistry.Get(SupportedFeatureIds.ReturnInLoop).Example;

        var trace = await interpreter.TraceAsync(allowed);
        var result = await analysis.AnalyzeAsync(rejected);

        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal(["Adult"], trace.Steps[^1].Output);
        Assert.Contains(result.Diagnostics, item => item.Code == "CL1012");
    }

    [Fact]
    public async Task RecursionIsRejectedWithALoopSuggestion()
    {
        var source = ProgramTemplates.Classic(
            "Console.WriteLine(Count(3));",
            "static int Count(int n)\n{\n    if (n == 0)\n    {\n        return 0;\n    }\n\n    return Count(n - 1);\n}");

        var result = await analysis.AnalyzeAsync(source);

        var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == "CL1013");
        Assert.Contains("loop", diagnostic.Suggestion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsoleWriteContinuesTheCurrentOutputLine()
    {
        var source = ProgramTemplates.Classic("Console.Write(\"a\");\nConsole.Write(\"b\");\nConsole.WriteLine(\"c\");\nConsole.WriteLine(\"d\");");

        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(["abc", "d"], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task ArgsIsReadOnlyAndEmptyInTheSimulation()
    {
        var source = ProgramTemplates.Classic("string first = args;\nConsole.WriteLine(first);");

        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(ExecutionTraceStatus.Unsupported, trace.Status);
        Assert.Contains("args is read-only and empty", Assert.Single(trace.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TopLevelStatementsStillWorkAsTheSecondaryMode()
    {
        const string source = "int count = 2;\nConsole.WriteLine(count);";

        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(ProgramForm.TopLevel, structure.DetectForm(source));
        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal(["2"], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task RewriteMovesALocalFunctionIntoTheClassAsAStaticMethod()
    {
        var source = SupportedFeatureRegistry.Get(SupportedFeatureIds.LocalFunctions).Example;
        var diagnostic = Assert.Single((await analysis.AnalyzeAsync(source)).Diagnostics);

        var rewrite = structure.RewriteLocalFunctionAsStaticMethod(source, diagnostic.Line!.Value, diagnostic.Column!.Value);

        Assert.True(rewrite.Succeeded, rewrite.Message);
        Assert.Contains("static int Double(int value)", rewrite.Source, StringComparison.Ordinal);
        Assert.Equal(CodeAnalysisStatus.Valid, (await analysis.AnalyzeAsync(rewrite.Source)).Status);
        var trace = await interpreter.TraceAsync(rewrite.Source);
        Assert.Equal(["8"], trace.Steps[^1].Output);
    }

    [Fact]
    public void RewriteRefusesWhenTheFunctionUsesMainsVariables()
    {
        var source = ProgramTemplates.Classic("int factor = 3;\nint Scale(int value)\n{\n    return value * factor;\n}\n\nConsole.WriteLine(Scale(2));");
        var line = source.Split('\n').ToList().FindIndex(text => text.Contains("int Scale", StringComparison.Ordinal)) + 1;

        var rewrite = structure.RewriteLocalFunctionAsStaticMethod(source, line, 13);

        Assert.False(rewrite.Succeeded);
        Assert.Null(rewrite.Source);
        Assert.Contains("factor", rewrite.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultEditorContentAndResetTargetAreTheClassicTemplate()
    {
        var state = new PlaygroundEditorState(PlaygroundSnippets.All, CodeAnalysisDefaults.MaximumCharacters);

        Assert.Equal(ProgramTemplates.ClassicHelloWorld, state.CurrentCode);
        Assert.Equal(PlaygroundSnippets.DefaultSnippetId, state.ActiveSnippet.Id);

        state.LoadSnippet("while");
        state.TryUpdateCode("changed");
        state.Reset();

        Assert.Equal(ProgramTemplates.ClassicHelloWorld, state.CurrentCode);
    }

    [Fact]
    public async Task EveryCuratedSnippetTracesAndExactlyOneUsesTopLevelMode()
    {
        foreach (var snippet in PlaygroundSnippets.All)
        {
            var trace = await interpreter.TraceAsync(snippet.Code);
            Assert.True(trace.Status == ExecutionTraceStatus.Completed, $"{snippet.Id}: {string.Join(" ", trace.Diagnostics.Select(item => item.Message))}");
        }

        var topLevel = Assert.Single(PlaygroundSnippets.All, snippet => structure.DetectForm(snippet.Code) == ProgramForm.TopLevel);
        Assert.EndsWith("(top-level mode)", topLevel.Title, StringComparison.Ordinal);
        Assert.Equal(7, PlaygroundSnippets.All.Count(snippet => structure.DetectForm(snippet.Code) == ProgramForm.Classic));
    }
}
