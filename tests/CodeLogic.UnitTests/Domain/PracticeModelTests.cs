using CodeLogic.Domain;
using CodeLogic.Domain.Practice;
using CodeLogic.Domain.Progress;

namespace CodeLogic.UnitTests.Domain;

public sealed class PracticeModelTests
{
    [Fact]
    public void ExerciseRetainsItsLessonRelationshipAndKind()
    {
        var lessonId = LessonId.New();

        var exercise = new Exercise(
            ExerciseId.New(),
            lessonId,
            "Choose the variable type",
            "Which type stores a whole number?",
            ExerciseKind.MultipleChoice,
            order: 1);

        Assert.Equal(lessonId, exercise.LessonId);
        Assert.Equal(ExerciseKind.MultipleChoice, exercise.Kind);
    }

    [Fact]
    public void ExerciseRejectsUnsupportedKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Exercise(
                ExerciseId.New(),
                LessonId.New(),
                "Exercise",
                "Prompt",
                (ExerciseKind)999,
                order: 1));
    }

    [Fact]
    public void ExerciseRejectsDefaultLessonIdentifier()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Exercise(
                ExerciseId.New(),
                default,
                "Exercise",
                "Prompt",
                ExerciseKind.ValueInput,
                order: 1));

        Assert.Equal("lessonId", exception.ParamName);
    }

    [Fact]
    public void AttemptRecordsEvaluatedOutcome()
    {
        var attemptedAtUtc = new DateTimeOffset(2026, 10, 31, 8, 0, 0, TimeSpan.Zero);

        var attempt = new ExerciseAttempt(
            ExerciseAttemptId.New(),
            ExerciseId.New(),
            attemptNumber: 2,
            AttemptOutcome.Correct,
            attemptedAtUtc);

        Assert.Equal(2, attempt.AttemptNumber);
        Assert.Equal(AttemptOutcome.Correct, attempt.Outcome);
        Assert.Equal(attemptedAtUtc, attempt.AttemptedAtUtc);
    }

    [Fact]
    public void AttemptRejectsNonUtcTimestamp()
    {
        var localOffset = new DateTimeOffset(2026, 10, 31, 16, 0, 0, TimeSpan.FromHours(8));

        var exception = Assert.Throws<ArgumentException>(
            () => new ExerciseAttempt(
                ExerciseAttemptId.New(),
                ExerciseId.New(),
                attemptNumber: 1,
                AttemptOutcome.Incorrect,
                localOffset));

        Assert.Equal("attemptedAtUtc", exception.ParamName);
    }

    [Fact]
    public void AttemptRejectsNonPositiveAttemptNumber()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ExerciseAttempt(
                ExerciseAttemptId.New(),
                ExerciseId.New(),
                attemptNumber: 0,
                AttemptOutcome.Incorrect,
                DateTimeOffset.UnixEpoch));
    }
}
