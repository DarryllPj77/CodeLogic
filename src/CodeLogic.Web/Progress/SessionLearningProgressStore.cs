using System.Text.Json;

using CodeLogic.Application.Curriculum;
using CodeLogic.Application.Progress;
using CodeLogic.Domain;
using CodeLogic.Domain.Progress;

namespace CodeLogic.Web.Progress;

public sealed class SessionLearningProgressStore(IHttpContextAccessor httpContextAccessor)
{
    private const string SessionKey = "CodeLogic.LearningProgress.v1";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public LearningSessionProgress Load()
    {
        var serialized = Session.GetString(SessionKey);
        if (serialized is null)
        {
            return new LearningSessionProgress();
        }

        var data = JsonSerializer.Deserialize<SessionProgressData>(serialized, SerializerOptions)
            ?? throw new InvalidOperationException("The session progress payload could not be read.");
        var attempts = data.Attempts.Select(item => new ExerciseAttempt(
            new ExerciseAttemptId(item.AttemptId),
            new ExerciseId(item.ExerciseId),
            item.AttemptNumber,
            item.Outcome,
            item.AttemptedAtUtc));
        var completedLessonIds = data.CompletedLessonIds.Select(value => new LessonId(value));

        return new LearningSessionProgress(attempts, completedLessonIds);
    }

    public ExerciseAttempt RecordAttempt(
        CurriculumLesson lesson,
        QuizEvaluationResult evaluation,
        DateTimeOffset attemptedAtUtc)
    {
        var progress = Load();
        var attempt = progress.RecordAttempt(
            lesson,
            evaluation,
            ExerciseAttemptId.New(),
            attemptedAtUtc);

        Save(progress);
        return attempt;
    }

    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("Learning progress requires an active HTTP session.");

    private void Save(LearningSessionProgress progress)
    {
        var data = new SessionProgressData(
            progress.Attempts.Select(attempt => new SessionAttemptData(
                attempt.Id.Value,
                attempt.ExerciseId.Value,
                attempt.AttemptNumber,
                attempt.Outcome,
                attempt.AttemptedAtUtc)).ToArray(),
            progress.CompletedLessonIds.Select(id => id.Value).ToArray());

        Session.SetString(SessionKey, JsonSerializer.Serialize(data, SerializerOptions));
    }

    private sealed record SessionProgressData(
        IReadOnlyList<SessionAttemptData> Attempts,
        IReadOnlyList<Guid> CompletedLessonIds);

    private sealed record SessionAttemptData(
        Guid AttemptId,
        Guid ExerciseId,
        int AttemptNumber,
        AttemptOutcome Outcome,
        DateTimeOffset AttemptedAtUtc);
}
