using System.Globalization;

using CodeLogic.Application.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeLogic.Infrastructure.Analysis;

public sealed class RoslynCodeAnalysisService : ICodeAnalysisService
{
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.CSharp14,
        DocumentationMode.None,
        SourceCodeKind.Regular);

    public RoslynCodeAnalysisService()
        : this(CodeAnalysisLimits.Default)
    {
    }

    public RoslynCodeAnalysisService(CodeAnalysisLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        Limits = limits;
    }

    public CodeAnalysisLimits Limits { get; }

    public async Task<CodeAnalysisResult> AnalyzeAsync(
        string? source,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(source))
        {
            return InvalidInput(
                "CL0001",
                "Enter some C# source text before checking it.",
                "The parser needs at least one non-whitespace character.");
        }

        if (source.Length > Limits.MaximumCharacters)
        {
            return InvalidInput(
                "CL0002",
                $"The source has {source.Length:N0} characters; the limit is {Limits.MaximumCharacters:N0}.",
                "Short snippets keep analysis bounded and the feedback suitable for a beginner lesson.");
        }

        using var timeoutSource = new CancellationTokenSource(Limits.MaximumDuration);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            return await Task.Run(
                () => AnalyzeCore(source, linkedSource.Token),
                linkedSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CodeAnalysisResult(
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
                ]);
        }
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
}
