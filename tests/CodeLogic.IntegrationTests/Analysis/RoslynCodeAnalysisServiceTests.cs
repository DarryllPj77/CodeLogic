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
    public async Task SupportedControlFlowAndLocalFunctionAreValid()
    {
        const string source = """
            int Double(int value)
            {
                return value * 2;
            }

            int total = 0;
            for (int index = 0; index < 3; index++)
            {
                total = total + Double(index);
            }

            if (total > 0)
            {
                Console.WriteLine(total);
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
        var validAtLimit = "int value = 1;".PadRight(service.Limits.MaximumCharacters);
        var oversized = validAtLimit + " ";

        var boundaryResult = await service.AnalyzeAsync(validAtLimit);
        var oversizedResult = await service.AnalyzeAsync(oversized);

        Assert.Equal(CodeAnalysisStatus.Valid, boundaryResult.Status);
        Assert.Equal(CodeAnalysisStatus.InvalidInput, oversizedResult.Status);
        Assert.Equal("CL0002", Assert.Single(oversizedResult.Diagnostics).Code);
    }

    [Theory]
    [InlineData("using System;\nint value = 1;", "Using directives")]
    [InlineData("class Program { }", "class declaration")]
    [InlineData("foreach (int value in values) { Console.WriteLine(value); }", "foreach loop")]
    [InlineData("string value = new string('a', 2);", "Object creation")]
    [InlineData("string value = File.ReadAllText(\"secret.txt\");", "not supported")]
    [InlineData("int transform = values.Select(value => value + 1);", "not supported")]
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
        Assert.Contains("exceeds the limit of 3", diagnostic.Message, StringComparison.Ordinal);
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
        var immediatelyLimitedService = new RoslynCodeAnalysisService(
            new CodeAnalysisLimits(1_200, 3, TimeSpan.FromTicks(1)));

        var result = await immediatelyLimitedService.AnalyzeAsync("int value = 1;");

        Assert.Equal(CodeAnalysisStatus.TimedOut, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CL0003", diagnostic.Code);
        Assert.Equal(CodeDiagnosticCategory.Limit, diagnostic.Category);
    }

    [Fact]
    public void DefaultLimitsAreExplicitAndMatchCurriculum()
    {
        var policy = new SeededCurriculumCatalog().GetManifest().SafeSnippetPolicy;

        Assert.Equal(1_200, service.Limits.MaximumCharacters);
        Assert.Equal(3, service.Limits.MaximumNestingDepth);
        Assert.Equal(TimeSpan.FromSeconds(2), service.Limits.MaximumDuration);
        Assert.Equal(policy.MaximumCharacters, service.Limits.MaximumCharacters);
        Assert.Equal(policy.MaximumNestingDepth, service.Limits.MaximumNestingDepth);
    }
}
