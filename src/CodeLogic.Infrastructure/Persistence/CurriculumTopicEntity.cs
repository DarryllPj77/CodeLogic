namespace CodeLogic.Infrastructure.Persistence;

public sealed class CurriculumTopicEntity
{
    public Guid Id { get; set; }

    public int Order { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ICollection<CurriculumLessonEntity> Lessons { get; set; } = [];
}
