using CodeLogic.Application.Execution;
using CodeLogic.Application.Feedback;
using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Execution;
using CodeLogic.Infrastructure.Feedback;
using CodeLogic.Infrastructure.Flowcharts;

namespace CodeLogic.IntegrationTests.Feedback;

public sealed class InterpreterLogicalFeedbackServiceTests
{
    [Fact]
    public async Task CorrectAgeComparisonPassesBelowExactAndAboveCases()
    {
        var service = CreateService(out _);

        var result = await service.EvaluateAsync(
            KnownLogicalExercises.AgeBoundary,
            KnownLogicalExercises.AgeBoundary.StarterCode);

        Assert.Equal(LogicalFeedbackStatus.Passed, result.Status);
        Assert.Collection(
            result.Tests,
            test => AssertCase(test, 17, "Minor", passed: true, isBoundary: false),
            test => AssertCase(test, 18, "Adult", passed: true, isBoundary: true),
            test => AssertCase(test, 19, "Adult", passed: true, isBoundary: false));
        Assert.Contains("does not prove", result.CoverageLimitation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GreaterThanMistakeFailsExactBoundaryWithActionableHint()
    {
        const string source = """
            int age = 18;
            if (age > 18)
            {
                Console.WriteLine("Adult");
            }
            else
            {
                Console.WriteLine("Minor");
            }
            """;
        var service = CreateService(out _);

        var result = await service.EvaluateAsync(
            KnownLogicalExercises.AgeBoundary,
            source);

        Assert.Equal(LogicalFeedbackStatus.Failed, result.Status);
        Assert.True(result.Tests[0].Passed);
        var boundary = result.Tests[1];
        Assert.False(boundary.Passed);
        Assert.True(boundary.IsBoundary);
        Assert.Equal("Adult", boundary.ExpectedOutput);
        Assert.Equal("Minor", boundary.ActualOutput);
        Assert.Contains(">= 18", boundary.Hint, StringComparison.Ordinal);
        Assert.True(result.Tests[2].Passed);
    }

    [Fact]
    public async Task PassingKnownCasesCanStillBeAFalsePositiveForUntestedInput()
    {
        const string source = """
            int age = 18;
            if (age == 18 || age == 19)
            {
                Console.WriteLine("Adult");
            }
            else
            {
                Console.WriteLine("Minor");
            }
            """;
        var service = CreateService(out var interpreter);

        var knownResult = await service.EvaluateAsync(
            KnownLogicalExercises.AgeBoundary,
            source);
        var untestedTrace = await interpreter.TraceAsync(source.Replace(
            "int age = 18;",
            "int age = 20;",
            StringComparison.Ordinal));

        Assert.Equal(LogicalFeedbackStatus.Passed, knownResult.Status);
        Assert.All(knownResult.Tests, test => Assert.True(test.Passed));
        Assert.Contains("Untested values", knownResult.CoverageLimitation, StringComparison.Ordinal);
        Assert.Equal(ExecutionTraceStatus.Completed, untestedTrace.Status);
        Assert.Equal(["Minor"], untestedTrace.Steps[^1].Output);
    }

    [Fact]
    public async Task SyntaxProblemsAreReturnedSeparatelyWithoutLogicalTestResults()
    {
        const string source = """
            int age = 18;
            if (age >= 18 { Console.WriteLine("Adult"); }
            """;
        var service = CreateService(out _);

        var result = await service.EvaluateAsync(
            KnownLogicalExercises.AgeBoundary,
            source);

        Assert.Equal(LogicalFeedbackStatus.CannotEvaluate, result.Status);
        Assert.Empty(result.Tests);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.StartsWith("CS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingKnownInputVariableReturnsSetupHintNotLogicVerdict()
    {
        const string source = """
            int years = 18;
            Console.WriteLine("Adult");
            """;
        var service = CreateService(out _);

        var result = await service.EvaluateAsync(
            KnownLogicalExercises.AgeBoundary,
            source);

        Assert.Equal(LogicalFeedbackStatus.CannotEvaluate, result.Status);
        Assert.Empty(result.Tests);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("LF1001", diagnostic.Code);
        Assert.Contains("int age = 18", diagnostic.Explanation, StringComparison.Ordinal);
    }

    private static InterpreterLogicalFeedbackService CreateService(
        out RoslynPedagogicalInterpreter interpreter)
    {
        var analyzer = new RoslynCodeAnalysisService();
        var translator = new RoslynFlowchartTranslator(analyzer);
        interpreter = new RoslynPedagogicalInterpreter(translator);
        return new InterpreterLogicalFeedbackService(interpreter);
    }

    private static void AssertCase(
        LogicalTestResult result,
        int input,
        string output,
        bool passed,
        bool isBoundary)
    {
        Assert.Equal(input, result.Input);
        Assert.Equal(output, result.ExpectedOutput);
        Assert.Equal(output, result.ActualOutput);
        Assert.Equal(passed, result.Passed);
        Assert.Equal(isBoundary, result.IsBoundary);
        Assert.Null(result.Hint);
    }
}
