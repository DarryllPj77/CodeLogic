using CodeLogic.Application.Curriculum;
using CodeLogic.Application.Progress;
using CodeLogic.Domain;
using CodeLogic.Domain.Progress;
using CodeLogic.Infrastructure.Curriculum;

namespace CodeLogic.IntegrationTests.Progress;

public sealed class LearningSessionProgressTests
{
    private static readonly DateTimeOffset AttemptedAtUtc =
        new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IncorrectThenCorrectAttemptsCompleteLessonAndUpdateSnapshot()
    {
        var lesson = CompleteLessons()[0];
        var progress = new LearningSessionProgress();

        var incorrect = progress.RecordAttempt(
            lesson,
            Evaluate(lesson, "double"),
            ExerciseAttemptId.New(),
            AttemptedAtUtc);
        var beforeCorrect = progress.CreateSnapshot(2, AttemptedAtUtc);
        var correct = progress.RecordAttempt(
            lesson,
            Evaluate(lesson, "int"),
            ExerciseAttemptId.New(),
            AttemptedAtUtc.AddMinutes(1));
        var afterCorrect = progress.CreateSnapshot(2, AttemptedAtUtc.AddMinutes(1));

        Assert.Equal(1, incorrect.AttemptNumber);
        Assert.Equal(AttemptOutcome.Incorrect, incorrect.Outcome);
        Assert.Equal(1, beforeCorrect.AttemptCount);
        Assert.Equal(0, beforeCorrect.CompletedLessonCount);

        Assert.Equal(2, correct.AttemptNumber);
        Assert.Equal(AttemptOutcome.Correct, correct.Outcome);
        Assert.Equal(2, afterCorrect.AttemptCount);
        Assert.Equal(1, afterCorrect.CorrectAttemptCount);
        Assert.Equal(1, afterCorrect.CompletedLessonCount);
        Assert.Equal(50m, afterCorrect.CompletionPercentage);
        Assert.True(progress.IsLessonComplete(lesson.Lesson.Id));
    }

    [Fact]
    public void RepeatedCorrectAttemptDoesNotDuplicateLessonCompletion()
    {
        var lesson = CompleteLessons()[0];
        var progress = new LearningSessionProgress();

        progress.RecordAttempt(
            lesson,
            Evaluate(lesson, "int"),
            ExerciseAttemptId.New(),
            AttemptedAtUtc);
        var repeated = progress.RecordAttempt(
            lesson,
            Evaluate(lesson, "int"),
            ExerciseAttemptId.New(),
            AttemptedAtUtc.AddMinutes(1));
        var snapshot = progress.CreateSnapshot(2, AttemptedAtUtc.AddMinutes(1));

        Assert.Equal(2, repeated.AttemptNumber);
        Assert.Equal(2, snapshot.AttemptCount);
        Assert.Equal(2, snapshot.CorrectAttemptCount);
        Assert.Equal(1, snapshot.CompletedLessonCount);
    }

    [Fact]
    public void AttemptNumbersAreIndependentForEachExercise()
    {
        var lessons = CompleteLessons();
        var progress = new LearningSessionProgress();

        var firstLessonAttempt = progress.RecordAttempt(
            lessons[0],
            Evaluate(lessons[0], "double"),
            ExerciseAttemptId.New(),
            AttemptedAtUtc);
        var secondLessonAttempt = progress.RecordAttempt(
            lessons[1],
            Evaluate(lessons[1], "false"),
            ExerciseAttemptId.New(),
            AttemptedAtUtc.AddMinutes(1));

        Assert.Equal(1, firstLessonAttempt.AttemptNumber);
        Assert.Equal(1, secondLessonAttempt.AttemptNumber);
    }

    [Fact]
    public void AnswerRequiredResultIsNotRecorded()
    {
        var lesson = CompleteLessons()[1];
        var progress = new LearningSessionProgress();
        var answerRequired = DeterministicQuizEvaluator.Evaluate(
            lesson.QuizCriteria,
            submittedAnswer: null);

        Assert.Throws<InvalidOperationException>(() => progress.RecordAttempt(
            lesson,
            answerRequired,
            ExerciseAttemptId.New(),
            AttemptedAtUtc));
        Assert.Empty(progress.Attempts);
    }

    private static CurriculumLesson[] CompleteLessons() =>
        new SeededCurriculumCatalog().GetManifest().Lessons
            .Where(lesson => lesson.Status == CurriculumLessonStatus.Complete)
            .ToArray();

    private static QuizEvaluationResult Evaluate(
        CurriculumLesson lesson,
        string answer) =>
        DeterministicQuizEvaluator.Evaluate(lesson.QuizCriteria, answer);
}
