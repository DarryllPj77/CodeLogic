using CodeLogic.Application.Analysis;
using CodeLogic.Application.Curriculum;
using CodeLogic.Infrastructure.Analysis;
using CodeLogic.Infrastructure.Curriculum;

namespace CodeLogic.IntegrationTests.Analysis;

public sealed class RoslynCodeAnalysisServiceTests
{
    private readonly RoslynCodeAnalysisService service = new();

    [Fact]
    public async Task CompleteCurriculumSamplesAreValid()
    {
        var completeLessons = new SeededCurriculumCatalog().GetManifest().Lessons
            .Where(lesson => lesson.Status == CurriculumLessonStatus.Complete);

        foreach (var lesson in completeLessons)
        {
            var result = await service.AnalyzeAsync(lesson.SampleCode);

            Assert.True(
                result.IsValid,
                $"{lesson.Lesson.Title}: {string.Join(" | ", result.Diagnostics.Select(item => item.Message))}");
            Assert.Empty(result.Diagnostics);
        }
    }

    [Fact]
    public async Task SupportedControlFlowAndStaticMethodAreValid()
    {
        const string source = """
            using System;

            class Program
            {
                static void Main(string[] args)
                {
                    int total = 0;
                    for (int index = 0; index < 3; index++)
                    {
                        total = total + Double(index);
                    }

                    if (total > 0)
                    {
                        Console.WriteLine(total);
                    }

                    do
                    {
                        total--;
                    }
                    while (total > 0);
                }

                static int Double(int value)
                {
                    return value * 2;
                }
            }
            """;

        var result = await service.AnalyzeAsync(source);

        Assert.Equal(CodeAnalysisStatus.Valid, result.Status);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task SyntaxErrorIncludesOneBasedLineColumnAndExplanation()
    {
        const string source = "int value = 1;\nConsole.WriteLine(value";

        var result = await service.AnalyzeAsync(source);

        Assert.Equal(CodeAnalysisStatus.SyntaxError, result.Status);
        Assert.NotEmpty(result.Diagnostics);
        var diagnostic = result.Diagnostics[0];
        Assert.Equal(CodeDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(2, diagnostic.Line);
        Assert.NotNull(diagnostic.Column);
        Assert.True(diagnostic.Column > 0);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Explanation));
        Assert.StartsWith("CS", diagnostic.Code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n")]
    public async Task MissingSourceIsRejected(string? source)
    {
        var result = await service.AnalyzeAsync(source);

        Assert.Equal(CodeAnalysisStatus.InvalidInput, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CL0001", diagnostic.Code);
        Assert.Null(diagnostic.Line);
    }

    [Fact]
    public async Task SizeBoundaryIsEnforcedBeforeParsing()
    {
        var validAtLimit = "int value = 1;".PadRight(CodeAnalysisDefaults.MaximumCharacters);
        var oversized = validAtLimit + " ";

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var boundaryResult = await service.AnalyzeAsync(validAtLimit, cancellationSource.Token);
        var oversizedResult = await service.AnalyzeAsync(oversized);

        Assert.Equal(10_000, validAtLimit.Length);
        Assert.Equal(10_001, oversized.Length);
        Assert.Equal(CodeAnalysisStatus.Valid, boundaryResult.Status);
        Assert.Equal(CodeAnalysisStatus.InvalidInput, oversizedResult.Status);
        var diagnostic = Assert.Single(oversizedResult.Diagnostics);
        Assert.Equal("CL0002", diagnostic.Code);
        Assert.Contains("10,000", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("using static System.Console;\nint value = 1;", "plain using directives")]
    [InlineData("class Program { }", "No Main method")]
    [InlineData("foreach (int value in values) { Console.WriteLine(value); }", "foreach loop")]
    [InlineData("string value = new string('a', 2);", "Creating objects")]
    [InlineData("string value = File.ReadAllText(\"secret.txt\");", "not supported")]
    [InlineData("File.WriteAllText(\"payload.txt\", \"data\");", "not supported")]
    [InlineData("System.Diagnostics.Process.Start(\"calc\");", "not supported")]
    [InlineData("System.Reflection.Assembly.Load(\"payload\");", "not supported")]
    [InlineData("HttpClient client = new HttpClient();", "not supported")]
    [InlineData("int transform = values.Select(value => value + 1);", "not supported")]
    [InlineData("while (true) { break; }", "break is not supported")]
    [InlineData("for (;;) { continue; }", "continue is not supported")]
    public async Task UnsupportedFeaturesReturnActionableDiagnostics(
        string source,
        string expectedMessagePart)
    {
        var result = await service.AnalyzeAsync(source);

        Assert.Equal(CodeAnalysisStatus.Unsupported, result.Status);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Message.Contains(expectedMessagePart, StringComparison.OrdinalIgnoreCase)
                && diagnostic.Line is > 0
                && diagnostic.Column is > 0
                && !string.IsNullOrWhiteSpace(diagnostic.Explanation));
    }

    [Fact]
    public async Task ExcessiveControlFlowNestingIsRejected()
    {
        const string source = """
            if (true)
            {
                while (true)
                {
                    if (true)
                    {
                        for (int index = 0; index < 1; index++)
                        {
                            Console.WriteLine(index);
                        }
                    }
                }
            }
            """;

        var result = await service.AnalyzeAsync(source);

        Assert.Equal(CodeAnalysisStatus.Unsupported, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == "CL1004");
        Assert.Equal("This code is nested 4 levels deep; the limit is 3.", diagnostic.Message);
        Assert.Contains("static method", diagnostic.Suggestion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationIsHonored()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AnalyzeAsync("int value = 1;", cancellationSource.Token));
    }

    [Fact]
    public async Task ServiceTimeLimitReturnsAStableDiagnostic()
    {
        var source = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 50).Select(index => $"int value{index} = {index};"));
        var immediatelyLimitedService = new RoslynCodeAnalysisService(
            new CodeAnalysisLimits(1_200, 3, TimeSpan.FromMilliseconds(1)));

        var result = await immediatelyLimitedService.AnalyzeAsync(source);

        Assert.Equal(CodeAnalysisStatus.TimedOut, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CL0003", diagnostic.Code);
        Assert.Equal(CodeDiagnosticCategory.Limit, diagnostic.Category);
    }

    [Fact]
    public void DefaultLimitsAreExplicitAndMatchCurriculum()
    {
        var policy = new SeededCurriculumCatalog().GetManifest().SafeSnippetPolicy;

        Assert.Equal(10_000, service.Limits.MaximumCharacters);
        Assert.Equal(3, service.Limits.MaximumNestingDepth);
        Assert.Equal(TimeSpan.FromSeconds(2), service.Limits.MaximumDuration);
        Assert.Equal(policy.MaximumCharacters, service.Limits.MaximumCharacters);
        Assert.Equal(policy.MaximumNestingDepth, service.Limits.MaximumNestingDepth);
    }
}
