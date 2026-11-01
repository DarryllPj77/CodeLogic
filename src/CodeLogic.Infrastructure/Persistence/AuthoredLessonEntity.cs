namespace CodeLogic.Infrastructure.Persistence;

public sealed class AuthoredLessonEntity
{
    public Guid Id { get; set; }

    public int Status { get; set; }

    public int Version { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Objective { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string SampleCode { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? PublishedAtUtc { get; set; }
}
