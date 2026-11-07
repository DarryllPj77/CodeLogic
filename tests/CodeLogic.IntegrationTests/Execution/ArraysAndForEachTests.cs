using CodeLogic.Application.Analysis;
using CodeLogic.Application.Execution;
using CodeLogic.Application.Flowcharts;
using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Execution;
using CodeLogic.Infrastructure.Flowcharts;

namespace CodeLogic.IntegrationTests.Execution;

/// <summary>Phase 2: arrays, indexing, .Length, foreach, and args.</summary>
public sealed class ArraysAndForEachTests
{
    private readonly RoslynCodeAnalysisService analysis = new();
    private readonly RoslynFlowchartTranslator translator;
    private readonly RoslynPedagogicalInterpreter interpreter;

    public ArraysAndForEachTests()
    {
        translator = new RoslynFlowchartTranslator(analysis);
        interpreter = new RoslynPedagogicalInterpreter(translator);
    }

    [Fact]
    public async Task ArraysCanBeCreatedReadChangedAndMeasured()
    {
        var trace = await Trace("""
            int[] scores = { 90, 72, 85 };
            scores[1] = 75;
            scores[2] += 5;
            scores[0]++;
            int first = scores[0];
            Console.WriteLine($"{first} {scores[1]} {scores[2]} {scores.Length}");
            """);

        Assert.Equal(["91 75 90 3"], trace.Steps[^1].Output);
        Assert.Contains(trace.Steps, step => step.Variables.Any(variable => variable.Key == "scores" && variable.Value == "{ 91, 75, 90 }"));
    }

    [Fact]
    public async Task SizedArraysStartWithDefaultValues()
    {
        var trace = await Trace("""
            int[] counts = new int[3];
            bool[] flags = new bool[2];
            double[] prices = new double[1];
            string[] names = new string[2];
            names[0] = "Ada";
            Console.WriteLine(names[0]);
            """);

        var variables = trace.Steps[^1].Variables.ToDictionary(variable => variable.Key, variable => variable.Value);
        Assert.Equal("{ 0, 0, 0 }", variables["counts"]);
        Assert.Equal("{ false, false }", variables["flags"]);
        Assert.Equal("{ 0 }", variables["prices"]);
        Assert.Equal("{ \"Ada\", null }", variables["names"]);
        Assert.Equal(["Ada"], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task ImplicitAndExplicitArrayCreationInferOrUseTheItemType()
    {
        var trace = await Trace("""
            double[] mixed = new[] { 1, 2.5 };
            string[] words = new string[] { "hi", "there" };
            char[] letters = { 'a', 'b' };
            Console.WriteLine(mixed[0] + mixed[1]);
            Console.WriteLine(words[1]);
            Console.WriteLine(letters[1]);
            """);

        Assert.Equal(["3.5", "there", "b"], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task ArraysAreSharedNotCopied()
    {
        var trace = await Trace("""
            int[] original = { 1, 2 };
            int[] alias = original;
            alias[0] = 99;
            Console.WriteLine(original[0]);
            """);

        Assert.Equal(["99"], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task MethodsReceiveTheSameArrayAndCanReturnArrays()
    {
        var source = ProgramTemplates.Classic(
            "int[] values = { 1, 2, 3 };\nDouble(values);\nint[] copy = Make(2);\nConsole.WriteLine($\"{values[2]} {copy.Length}\");",
            "static void Double(int[] items)\n{\n    for (int index = 0; index < items.Length; index++)\n    {\n        items[index] *= 2;\n    }\n}\n\nstatic int[] Make(int size)\n{\n    int[] result = new int[size];\n    return result;\n}");

        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal(["6 2"], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task PrintingAnArrayShowsItsTypeLikeRealCSharp()
    {
        var trace = await Trace("int[] scores = { 1 };\nConsole.WriteLine(scores);");

        Assert.Equal(["System.Int32[]"], trace.Steps[^1].Output);
        Assert.Contains("use foreach", trace.Steps[^2].Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("int[] scores = { 1, 2 };\nConsole.WriteLine(scores[2]);", "valid indexes are 0 to 1")]
    [InlineData("int[] scores = new int[0];\nscores[0] = 1;", "has no items")]
    [InlineData("int[] scores = { 1 };\nConsole.WriteLine(scores[-1]);", "IndexOutOfRangeException")]
    public async Task IndexOutsideTheArrayStopsWithAnExplanation(string body, string expected)
    {
        var trace = await interpreter.TraceAsync(ProgramTemplates.Classic(body));

        Assert.Equal(ExecutionTraceStatus.Unsupported, trace.Status);
        Assert.Contains(expected, Assert.Single(trace.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NegativeWholeNumbersStayWholeNumbers()
    {
        var trace = await Trace("int below = -5;\nint offset = -below;\ndouble half = -0.5;\nConsole.WriteLine($\"{below} {offset} {half}\");");

        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal(["-5 5 -0.5"], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task VeryLargeArraysAreRefusedSoEveryItemStaysVisible()
    {
        var trace = await Trace("int[] big = new int[51];");

        Assert.Equal(ExecutionTraceStatus.Unsupported, trace.Status);
        Assert.Contains("limited to 50 items", Assert.Single(trace.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StringsCanBeIndexedAndMeasuredButNotChanged()
    {
        var reads = await Trace("string word = \"code\";\nchar first = word[0];\nConsole.WriteLine($\"{first} {word.Length}\");");
        var changes = await Trace("string word = \"code\";\nword[0] = 'C';");

        Assert.Equal(["c 4"], reads.Steps[^1].Output);
        Assert.Equal(ExecutionTraceStatus.Unsupported, changes.Status);
    }

    [Fact]
    public async Task ForEachTakesEachItemAndRemovesTheLoopVariableAfterwards()
    {
        var source = SupportedFeatureRegistry.Get(SupportedFeatureIds.ForEach).Example;

        var trace = await interpreter.TraceAsync(source);

        Assert.Equal(ExecutionTraceStatus.Completed, trace.Status);
        Assert.Equal(["Total: 247"], trace.Steps[^1].Output);
        Assert.Contains(trace.Steps, step => step.Explanation == "Set score to 72 (item 2 of 3).");
        Assert.Contains(trace.Steps, step => step.Explanation == "All 3 items of scores have been used, so the loop ends.");
        Assert.DoesNotContain(trace.Steps[^1].Variables, variable => variable.Key == "score");
    }

    [Fact]
    public async Task ForEachIsDrawnAsAMoreItemsDecisionAndANextItemStep()
    {
        var graph = (await translator.TranslateAsync(SupportedFeatureRegistry.Get(SupportedFeatureIds.ForEach).Example)).Graph!;

        var decision = Assert.Single(graph.Nodes, node => node.Type == FlowNodeType.Decision);
        Assert.Equal("more items in scores?", decision.Label);
        var next = Assert.Single(graph.Nodes, node => node.Label == "int score = next item of scores");
        Assert.Contains(graph.Edges, edge => edge.FromNodeId == decision.Id && edge.ToNodeId == next.Id && edge.Type == FlowEdgeType.True);
        Assert.Contains(graph.Edges, edge => edge.ToNodeId == decision.Id && edge.Type == FlowEdgeType.LoopBack);
        Assert.Contains(graph.Edges, edge => edge.FromNodeId == decision.Id && edge.Type == FlowEdgeType.False);
    }

    [Fact]
    public async Task ForEachSupportsBreakContinueAndStrings()
    {
        var trace = await Trace("""
            foreach (char letter in "abcde")
            {
                if (letter == 'b')
                {
                    continue;
                }

                if (letter == 'd')
                {
                    break;
                }

                Console.Write(letter);
            }

            Console.WriteLine("!");
            """);

        Assert.Equal(["ac!"], trace.Steps[^1].Output);
    }

    [Fact]
    public async Task ForEachOverAnEmptyArrayExplainsThatTheBodyNeverRuns()
    {
        var trace = await Trace("int[] none = new int[0];\nforeach (int item in none)\n{\n    Console.WriteLine(item);\n}");

        Assert.Contains(trace.Steps, step => step.Explanation == "none has no items, so the loop body never runs.");
    }

    [Theory]
    [InlineData("int[] scores = { 1 };\nforeach (int score in scores)\n{\n    score = 5;\n}")]
    [InlineData("int[] scores = { 1 };\nforeach (int score in scores)\n{\n    score++;\n}")]
    public async Task AssigningTheForEachVariableIsExplainedAsReadOnly(string body)
    {
        var result = await analysis.AnalyzeAsync(ProgramTemplates.Classic(body));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CL1016", diagnostic.Code);
        Assert.Equal("The foreach variable score is read-only.", diagnostic.Message);
        Assert.Equal(SupportedFeatureIds.ForEach, diagnostic.HelpTopicId);
    }

    [Theory]
    [InlineData("int[,] grid = new int[2, 2];")]
    [InlineData("int[][] rows = new int[2][];")]
    public async Task MultiDimensionalAndJaggedArraysAreNotSupportedYet(string body)
    {
        var result = await analysis.AnalyzeAsync(ProgramTemplates.Classic(body));

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.HelpTopicId == SupportedFeatureIds.MultiDimensionalArrays);
    }

    [Fact]
    public async Task ForEachWithVarAsksForAnExplicitType()
    {
        var result = await analysis.AnalyzeAsync(ProgramTemplates.Classic("int[] scores = { 1 };\nforeach (var score in scores)\n{\n    Console.WriteLine(score);\n}"));

        Assert.Equal("var is not supported yet.", Assert.Single(result.Diagnostics).Message);
    }

    private Task<ExecutionTraceResult> Trace(string mainBody) => interpreter.TraceAsync(ProgramTemplates.Classic(mainBody));
}
