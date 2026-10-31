using CodeLogic.Application.Curriculum;
using CodeLogic.Application.Progress;
using CodeLogic.Domain;
using CodeLogic.Domain.Progress;

using Microsoft.EntityFrameworkCore;

namespace CodeLogic.Infrastructure.Persistence;

public sealed class EfUserLearningProgressStore(CodeLogicDbContext dbContext)
    : IUserLearningProgressStore
{
    public async Task<LearningSessionProgress> LoadAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var attempts = await dbContext.UserExerciseAttempts
            .AsNoTracking()
            .Where(entity => entity.UserId == userId)
            .OrderBy(entity => entity.AttemptedAtUtc)
            .ThenBy(entity => entity.Id)
            .Select(entity => new ExerciseAttempt(
                new ExerciseAttemptId(entity.Id),
                new ExerciseId(entity.ExerciseId),
                entity.AttemptNumber,
                (AttemptOutcome)entity.Outcome,
                new DateTimeOffset(DateTime.SpecifyKind(entity.AttemptedAtUtc, DateTimeKind.Utc))))
            .ToListAsync(cancellationToken);
        var completedLessonIds = await dbContext.UserLessonProgress
            .AsNoTracking()
            .Where(entity => entity.UserId == userId)
            .Select(entity => new LessonId(entity.LessonId))
            .ToListAsync(cancellationToken);

        return new LearningSessionProgress(attempts, completedLessonIds);
    }

    public async Task<ExerciseAttempt> RecordAttemptAsync(
        string userId,
        CurriculumLesson lesson,
        QuizEvaluationResult evaluation,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(lesson);
        ArgumentNullException.ThrowIfNull(evaluation);

        var progress = await LoadAsync(userId, cancellationToken);
        var attempt = progress.RecordAttempt(
            lesson,
            evaluation,
            ExerciseAttemptId.New(),
            attemptedAtUtc);

        dbContext.UserExerciseAttempts.Add(new UserExerciseAttemptEntity
        {
            Id = attempt.Id.Value,
            UserId = userId,
            LessonId = lesson.Lesson.Id.Value,
            ExerciseId = attempt.ExerciseId.Value,
            AttemptNumber = attempt.AttemptNumber,
            Outcome = (int)attempt.Outcome,
            AttemptedAtUtc = attempt.AttemptedAtUtc.UtcDateTime,
        });

        if (attempt.Outcome == AttemptOutcome.Correct
            && !await dbContext.UserLessonProgress.AnyAsync(
                entity => entity.UserId == userId
                    && entity.LessonId == lesson.Lesson.Id.Value,
                cancellationToken))
        {
            dbContext.UserLessonProgress.Add(new UserLessonProgressEntity
            {
                UserId = userId,
                LessonId = lesson.Lesson.Id.Value,
                CompletedAtUtc = attempt.AttemptedAtUtc.UtcDateTime,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return attempt;
    }
}
