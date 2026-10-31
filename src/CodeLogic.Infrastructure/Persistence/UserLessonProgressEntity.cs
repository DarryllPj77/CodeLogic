namespace CodeLogic.Infrastructure.Persistence;

public sealed class UserLessonProgressEntity
{
    public string UserId { get; set; } = string.Empty;

    public Guid LessonId { get; set; }

    public DateTime CompletedAtUtc { get; set; }
}
