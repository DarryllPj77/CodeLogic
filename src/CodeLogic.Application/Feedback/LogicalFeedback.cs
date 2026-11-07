using System.Collections.ObjectModel;

using CodeLogic.Application.Analysis;

namespace CodeLogic.Application.Feedback;

public sealed record LogicalTestCase(
    string Name,
    int Input,
    string ExpectedOutput,
    bool IsBoundary,
    string FailureHint);

public sealed class LogicalExerciseSpecification
{
    public LogicalExerciseSpecification(
        string id,
        string title,
        string objective,
        string inputVariableName,
        string starterCode,
        IEnumerable<LogicalTestCase> testCases,
        string coverageLimitation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(objective);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputVariableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(starterCode);
        ArgumentNullException.ThrowIfNull(testCases);
        ArgumentException.ThrowIfNullOrWhiteSpace(coverageLimitation);

        var cases = testCases.ToArray();
        if (cases.Length == 0)
        {
            throw new ArgumentException("A logical exercise requires at least one test case.", nameof(testCases));
        }

        Id = id;
        Title = title;
        Objective = objective;
        InputVariableName = inputVariableName;
        StarterCode = starterCode;
        TestCases = new ReadOnlyCollection<LogicalTestCase>(cases);
        CoverageLimitation = coverageLimitation;
    }

    public string Id { get; }

    public string Title { get; }

    public string Objective { get; }

    public string InputVariableName { get; }

    public string StarterCode { get; }

    public IReadOnlyList<LogicalTestCase> TestCases { get; }

    public string CoverageLimitation { get; }
}

public static class KnownLogicalExercises
{
    public const string AgeBoundaryId = "age-adult-boundary";

    public static LogicalExerciseSpecification AgeBoundary { get; } = new(
        AgeBoundaryId,
        "Adult age boundary",
        "Display Adult for age 18 or older and Minor for younger ages.",
        inputVariableName: "age",
        starterCode: """
            using System;

            namespace MyProgram
            {
                class Program
                {
                    static void Main(string[] args)
                    {
                        int age = 18;

                        if (age >= 18)
                        {
                            Console.WriteLine("Adult");
                        }
                        else
                        {
                            Console.WriteLine("Minor");
                        }
                    }
                }
            }
            """,
        testCases:
        [
            new(
                "Just below the boundary",
                17,
                "Minor",
                IsBoundary: false,
                "Age 17 is below the adult boundary, so it should follow the Minor path."),
            new(
                "Exact boundary",
                18,
                "Adult",
                IsBoundary: true,
                "Age 18 is included. Compare with >= 18 rather than > 18."),
            new(
                "Just above the boundary",
                19,
                "Adult",
                IsBoundary: false,
                "Age 19 is above the boundary and should still follow the Adult path."),
        ],
        coverageLimitation:
            "Passing these three instructor-defined cases does not prove the logic is correct for every age. Untested values can still reveal a mistake.");
}

public enum LogicalFeedbackStatus
{
    Passed = 1,
    Failed = 2,
    CannotEvaluate = 3,
}

public sealed record LogicalTestResult(
    string Name,
    int Input,
    string ExpectedOutput,
    string ActualOutput,
    bool IsBoundary,
    bool Passed,
    string? Hint);

public sealed class LogicalFeedbackResult
{
    public LogicalFeedbackResult(
        LogicalFeedbackStatus status,
        string exerciseId,
        string coverageLimitation,
        IEnumerable<LogicalTestResult>? tests = null,
        IEnumerable<CodeDiagnostic>? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exerciseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(coverageLimitation);

        Status = status;
        ExerciseId = exerciseId;
        CoverageLimitation = coverageLimitation;
        Tests = new ReadOnlyCollection<LogicalTestResult>(tests?.ToArray() ?? []);
        Diagnostics = new ReadOnlyCollection<CodeDiagnostic>(diagnostics?.ToArray() ?? []);

        if (status == LogicalFeedbackStatus.CannotEvaluate && Tests.Count > 0)
        {
            throw new ArgumentException("Unavailable logic feedback cannot expose partial test results.", nameof(tests));
        }

        if (status != LogicalFeedbackStatus.CannotEvaluate && Tests.Count == 0)
        {
            throw new ArgumentException("Completed logic feedback requires test results.", nameof(tests));
        }
    }

    public LogicalFeedbackStatus Status { get; }

    public string ExerciseId { get; }

    public string CoverageLimitation { get; }

    public IReadOnlyList<LogicalTestResult> Tests { get; }

    public IReadOnlyList<CodeDiagnostic> Diagnostics { get; }

    public bool HasTestResults => Status != LogicalFeedbackStatus.CannotEvaluate;
}

public interface ILogicalFeedbackService
{
    Task<LogicalFeedbackResult> EvaluateAsync(
        LogicalExerciseSpecification exercise,
        string? source,
        CancellationToken cancellationToken = default);
}
