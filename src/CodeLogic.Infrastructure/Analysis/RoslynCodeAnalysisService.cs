using System.Globalization;
using System.Diagnostics;

using CodeLogic.Application.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeLogic.Infrastructure.Analysis;

public sealed partial class RoslynCodeAnalysisService : ICodeAnalysisService
{
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.CSharp14,
        DocumentationMode.None,
        SourceCodeKind.Regular);

    private readonly ILogger<RoslynCodeAnalysisService> logger;

    public RoslynCodeAnalysisService()
        : this(CodeAnalysisLimits.Default, NullLogger<RoslynCodeAnalysisService>.Instance)
    {
    }

    public RoslynCodeAnalysisService(CodeAnalysisLimits limits)
        : this(limits, NullLogger<RoslynCodeAnalysisService>.Instance)
    {
    }

    public RoslynCodeAnalysisService(ILogger<RoslynCodeAnalysisService> logger)
        : this(CodeAnalysisLimits.Default, logger)
    {
    }

    private RoslynCodeAnalysisService(
        CodeAnalysisLimits limits,
        ILogger<RoslynCodeAnalysisService> logger)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(logger);
        Limits = limits;
        this.logger = logger;
    }

    public CodeAnalysisLimits Limits { get; }

    public async Task<CodeAnalysisResult> AnalyzeAsync(
        string? source,
        CancellationToken cancellationToken = default)
    {
        var sourceLength = source?.Length ?? 0;
        var started = Stopwatch.GetTimestamp();
        using var activity = AnalysisTelemetry.ActivitySource.StartActivity(
            "analyze",
            ActivityKind.Internal);
        activity?.SetTag("code_logic.analysis.source_length", sourceLength);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(source))
            {
                return Complete(
                    InvalidInput(
                        "CL0001",
                        "Enter some C# source text before checking it.",
                        "The parser needs at least one non-whitespace character."),
                    sourceLength,
                    started,
                    activity);
            }

            if (source.Length > Limits.MaximumCharacters)
            {
                return Complete(
                    InvalidInput(
                        "CL0002",
                        $"The source has {source.Length:N0} characters; the limit is {Limits.MaximumCharacters:N0}.",
                        "Short snippets keep analysis bounded and the feedback suitable for a beginner lesson."),
                    sourceLength,
                    started,
                    activity);
            }

            using var timeoutSource = new CancellationTokenSource(Limits.MaximumDuration);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);

            try
            {
                var result = await Task.Run(
                    () => AnalyzeCore(source, linkedSource.Token),
                    linkedSource.Token);
                return Complete(result, sourceLength, started, activity);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Complete(
                    new CodeAnalysisResult(
                        CodeAnalysisStatus.TimedOut,
                        [
                            new CodeDiagnostic(
                                "CL0003",
                                CodeDiagnosticCategory.Limit,
                                CodeDiagnosticSeverity.Error,
                                $"Analysis exceeded the {Limits.MaximumDuration.TotalMilliseconds:N0} ms time limit.",
                                "Shorten the snippet and try again. CodeLogic stopped instead of continuing unbounded work.",
                                Line: null,
                                Column: null,
                                Length: 0),
                        ]),
                    sourceLength,
                    started,
                    activity);
            }
        }
        catch (OperationCanceledException)
        {
            Observe("cancelled", started, activity);
            throw;
        }
        catch (Exception exception)
        {
            var elapsed = Observe("faulted", started, activity);
            LogAnalysisFaulted(logger, exception.GetType().Name, sourceLength, elapsed.TotalMilliseconds);
            throw;
        }
    }

    private CodeAnalysisResult Complete(
        CodeAnalysisResult result,
        int sourceLength,
        long started,
        Activity? activity)
    {
        var status = result.Status.ToString();
        var elapsed = Observe(status, started, activity);
        LogAnalysisCompleted(logger, status, sourceLength, elapsed.TotalMilliseconds);
        return result;
    }

    private static TimeSpan Observe(
        string status,
        long started,
        Activity? activity)
    {
        var elapsed = Stopwatch.GetElapsedTime(started);
        var statusTag = new KeyValuePair<string, object?>("code_logic.analysis.status", status);
        AnalysisTelemetry.CompletedOperations.Add(1, statusTag);
        AnalysisTelemetry.Duration.Record(elapsed.TotalMilliseconds, statusTag);
        activity?.SetTag("code_logic.analysis.status", status);
        activity?.SetTag("code_logic.analysis.duration_ms", elapsed.TotalMilliseconds);
        activity?.SetStatus(status == "faulted" ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
        return elapsed;
    }

    private CodeAnalysisResult AnalyzeCore(string source, CancellationToken cancellationToken)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            ParseOptions,
            path: string.Empty,
            encoding: null,
            cancellationToken);
        var syntaxDiagnostics = syntaxTree
            .GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(ToCodeDiagnostic)
            .ToArray();

        if (syntaxDiagnostics.Any(diagnostic => diagnostic.Severity == CodeDiagnosticSeverity.Error))
        {
            return new CodeAnalysisResult(CodeAnalysisStatus.SyntaxError, syntaxDiagnostics);
        }

        var root = syntaxTree.GetRoot(cancellationToken);
        var unsupportedDiagnostics = SupportedSyntaxValidator.Validate(
            root,
            Limits.MaximumNestingDepth,
            cancellationToken);
        if (unsupportedDiagnostics.Count != 0)
        {
            return new CodeAnalysisResult(CodeAnalysisStatus.Unsupported, unsupportedDiagnostics);
        }

        return new CodeAnalysisResult(CodeAnalysisStatus.Valid, syntaxDiagnostics);
    }

    private static CodeAnalysisResult InvalidInput(
        string code,
        string message,
        string explanation) =>
        new(
            CodeAnalysisStatus.InvalidInput,
            [
                new CodeDiagnostic(
                    code,
                    CodeDiagnosticCategory.Input,
                    CodeDiagnosticSeverity.Error,
                    message,
                    explanation,
                    Line: null,
                    Column: null,
                    Length: 0),
            ]);

    private static CodeDiagnostic ToCodeDiagnostic(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        int? line = diagnostic.Location.IsInSource
            ? lineSpan.StartLinePosition.Line + 1
            : null;
        int? column = diagnostic.Location.IsInSource
            ? lineSpan.StartLinePosition.Character + 1
            : null;

        return new CodeDiagnostic(
            diagnostic.Id,
            CodeDiagnosticCategory.Syntax,
            diagnostic.Severity == DiagnosticSeverity.Error
                ? CodeDiagnosticSeverity.Error
                : CodeDiagnosticSeverity.Warning,
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
            ExplainSyntaxDiagnostic(diagnostic.Id),
            line,
            column,
            diagnostic.Location.IsInSource ? diagnostic.Location.SourceSpan.Length : 0);
    }

    private static string ExplainSyntaxDiagnostic(string diagnosticId) => diagnosticId switch
    {
        "CS1002" => "A C# statement usually ends with a semicolon. Check the end of this line.",
        "CS1513" => "An opening brace needs a matching closing brace to finish its block.",
        "CS1026" => "An opening parenthesis needs a matching closing parenthesis.",
        "CS1525" => "The parser found a token where it expected a value or expression.",
        "CS1010" => "A quoted string must finish before the line ends.",
        _ => "The C# parser could not understand this part of the snippet. Check nearby punctuation and keywords.",
    };

    [LoggerMessage(
        3001,
        LogLevel.Information,
        "Completed bounded analysis with status {Status} for {SourceLength} characters in {ElapsedMilliseconds:F2} ms.")]
    private static partial void LogAnalysisCompleted(
        ILogger logger,
        string status,
        int sourceLength,
        double elapsedMilliseconds);

    [LoggerMessage(
        3002,
        LogLevel.Error,
        "Bounded analysis failed with exception type {ExceptionType} for {SourceLength} characters after {ElapsedMilliseconds:F2} ms.")]
    private static partial void LogAnalysisFaulted(
        ILogger logger,
        string exceptionType,
        int sourceLength,
        double elapsedMilliseconds);
}
