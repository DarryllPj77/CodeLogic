using System.Collections.ObjectModel;

using CodeLogic.Application.Curriculum;
using CodeLogic.Domain;
using CodeLogic.Domain.Progress;

namespace CodeLogic.Application.Progress;

public sealed class LearningSessionProgress
{
    private readonly List<ExerciseAttempt> attempts;
    private readonly HashSet<LessonId> completedLessonIds;
    private readonly ReadOnlyCollection<ExerciseAttempt> readOnlyAttempts;

    public LearningSessionProgress(
        IEnumerable<ExerciseAttempt>? attempts = null,
        IEnumerable<LessonId>? completedLessonIds = null)
    {
        this.attempts = attempts?.ToList() ?? [];
        this.completedLessonIds = completedLessonIds?.ToHashSet() ?? [];
        readOnlyAttempts = this.attempts.AsReadOnly();
    }

    public IReadOnlyList<ExerciseAttempt> Attempts => readOnlyAttempts;

    public IReadOnlySet<LessonId> CompletedLessonIds => completedLessonIds;

    public ExerciseAttempt RecordAttempt(
        CurriculumLesson lesson,
        QuizEvaluationResult evaluation,
        ExerciseAttemptId attemptId,
        DateTimeOffset attemptedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        ArgumentNullException.ThrowIfNull(evaluation);

        var outcome = evaluation.Status switch
        {
            QuizEvaluationStatus.Correct => AttemptOutcome.Correct,
            QuizEvaluationStatus.Incorrect => AttemptOutcome.Incorrect,
            _ => throw new InvalidOperationException(
                "An answer-required result cannot be recorded as an attempt."),
        };
        var attemptNumber = attempts.Count(
            attempt => attempt.ExerciseId == lesson.Exercise.Id) + 1;
        var attempt = new ExerciseAttempt(
            attemptId,
            lesson.Exercise.Id,
            attemptNumber,
            outcome,
            attemptedAtUtc);

        attempts.Add(attempt);
        if (outcome == AttemptOutcome.Correct)
        {
            completedLessonIds.Add(lesson.Lesson.Id);
        }

        return attempt;
    }

    public bool IsLessonComplete(LessonId lessonId) =>
        completedLessonIds.Contains(lessonId);

    public ProgressSnapshot CreateSnapshot(
        int totalLessonCount,
        DateTimeOffset capturedAtUtc) =>
        new(
            totalLessonCount,
            completedLessonIds,
            attempts.Count,
            attempts.Count(attempt => attempt.Outcome == AttemptOutcome.Correct),
            capturedAtUtc);
}
