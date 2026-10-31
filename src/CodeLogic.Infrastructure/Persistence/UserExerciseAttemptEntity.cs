namespace CodeLogic.Infrastructure.Persistence;

public sealed class UserExerciseAttemptEntity
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public Guid LessonId { get; set; }

    public Guid ExerciseId { get; set; }

    public int AttemptNumber { get; set; }

    public int Outcome { get; set; }

    public DateTime AttemptedAtUtc { get; set; }
}
