using CodeLogic.Application.Analysis;
using CodeLogic.Application.Execution;
using CodeLogic.Application.Feedback;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeLogic.Infrastructure.Feedback;

public sealed class InterpreterLogicalFeedbackService(
    IPedagogicalInterpreter interpreter) : ILogicalFeedbackService
{
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.CSharp14,
        DocumentationMode.None,
        SourceCodeKind.Regular);

    public async Task<LogicalFeedbackResult> EvaluateAsync(
        LogicalExerciseSpecification exercise,
        string? source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var baseline = await interpreter.TraceAsync(source, cancellationToken);
        if (baseline.Status != ExecutionTraceStatus.Completed)
        {
            return CannotEvaluate(exercise, baseline.Diagnostics);
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            source!,
            ParseOptions,
            cancellationToken: cancellationToken);
        var root = await syntaxTree.GetRootAsync(cancellationToken);
        var inputs = root.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(variable => string.Equals(
                variable.Identifier.ValueText,
                exercise.InputVariableName,
                StringComparison.Ordinal))
            .ToArray();

        if (inputs.Length != 1 || inputs[0].Initializer is null)
        {
            return CannotEvaluate(
                exercise,
                [Diagnostic(
                    "LF1001",
                    $"The known exercise requires one initialized '{exercise.InputVariableName}' variable.",
                    $"Keep one declaration such as 'int {exercise.InputVariableName} = 18;' so instructor tests can replace only its authored input.")]);
        }

        var inputExpression = inputs[0].Initializer!.Value;
        var results = new List<LogicalTestResult>();
        foreach (var test in exercise.TestCases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var replacement = SyntaxFactory.LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    SyntaxFactory.Literal(test.Input))
                .WithTriviaFrom(inputExpression);
            var testSource = root.ReplaceNode(inputExpression, replacement).ToFullString();
            var trace = await interpreter.TraceAsync(testSource, cancellationToken);
            if (trace.Status != ExecutionTraceStatus.Completed)
            {
                return CannotEvaluate(exercise, trace.Diagnostics);
            }

            var actual = string.Join(Environment.NewLine, trace.Steps[^1].Output);
            var passed = string.Equals(
                test.ExpectedOutput,
                actual,
                StringComparison.Ordinal);
            results.Add(new LogicalTestResult(
                test.Name,
                test.Input,
                test.ExpectedOutput,
                string.IsNullOrEmpty(actual) ? "(no output)" : actual,
                test.IsBoundary,
                passed,
                passed ? null : test.FailureHint));
        }

        return new LogicalFeedbackResult(
            results.All(test => test.Passed)
                ? LogicalFeedbackStatus.Passed
                : LogicalFeedbackStatus.Failed,
            exercise.Id,
            exercise.CoverageLimitation,
            results);
    }

    private static LogicalFeedbackResult CannotEvaluate(
        LogicalExerciseSpecification exercise,
        IEnumerable<CodeDiagnostic> diagnostics) => new(
            LogicalFeedbackStatus.CannotEvaluate,
            exercise.Id,
            exercise.CoverageLimitation,
            diagnostics: diagnostics);

    private static CodeDiagnostic Diagnostic(
        string code,
        string message,
        string explanation) => new(
            code,
            CodeDiagnosticCategory.Unsupported,
            CodeDiagnosticSeverity.Error,
            message,
            explanation,
            null,
            null,
            0);
}
