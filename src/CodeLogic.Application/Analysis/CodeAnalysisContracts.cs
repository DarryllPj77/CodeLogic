using System.Collections.ObjectModel;

namespace CodeLogic.Application.Analysis;

public static class CodeAnalysisDefaults
{
    public const int MaximumCharacters = 1_200;

    public const int MaximumNestingDepth = 3;

    public static readonly TimeSpan MaximumDuration = TimeSpan.FromSeconds(2);
}

public sealed record CodeAnalysisLimits
{
    public CodeAnalysisLimits(
        int maximumCharacters,
        int maximumNestingDepth,
        TimeSpan maximumDuration)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumNestingDepth);
        if (maximumDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDuration),
                maximumDuration,
                "The analysis duration must be positive.");
        }

        MaximumCharacters = maximumCharacters;
        MaximumNestingDepth = maximumNestingDepth;
        MaximumDuration = maximumDuration;
    }

    public int MaximumCharacters { get; }

    public int MaximumNestingDepth { get; }

    public TimeSpan MaximumDuration { get; }

    public static CodeAnalysisLimits Default { get; } = new(
        CodeAnalysisDefaults.MaximumCharacters,
        CodeAnalysisDefaults.MaximumNestingDepth,
        CodeAnalysisDefaults.MaximumDuration);
}

public enum CodeAnalysisStatus
{
    Valid = 1,
    InvalidInput = 2,
    SyntaxError = 3,
    Unsupported = 4,
    TimedOut = 5,
}

public enum CodeDiagnosticCategory
{
    Input = 1,
    Syntax = 2,
    Unsupported = 3,
    Limit = 4,
}

public enum CodeDiagnosticSeverity
{
    Information = 1,
    Warning = 2,
    Error = 3,
}

public sealed record CodeDiagnostic(
    string Code,
    CodeDiagnosticCategory Category,
    CodeDiagnosticSeverity Severity,
    string Message,
    string Explanation,
    int? Line,
    int? Column,
    int Length);

public sealed class CodeAnalysisResult
{
    public CodeAnalysisResult(
        CodeAnalysisStatus status,
        IEnumerable<CodeDiagnostic>? diagnostics = null)
    {
        Status = status;
        Diagnostics = new ReadOnlyCollection<CodeDiagnostic>(
            diagnostics?.ToArray() ?? []);
    }

    public CodeAnalysisStatus Status { get; }

    public IReadOnlyList<CodeDiagnostic> Diagnostics { get; }

    public bool IsValid => Status == CodeAnalysisStatus.Valid;
}
