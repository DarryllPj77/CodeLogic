using CodeLogic.Application.Curriculum;
using CodeLogic.Domain.Progress;

namespace CodeLogic.Application.Progress;

public interface IUserLearningProgressStore
{
    Task<LearningSessionProgress> LoadAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<ExerciseAttempt> RecordAttemptAsync(
        string userId,
        CurriculumLesson lesson,
        QuizEvaluationResult evaluation,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken = default);
}
