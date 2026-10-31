using CodeLogic.Domain;

namespace CodeLogic.Domain.Curriculum;

public sealed class Lesson
{
    private readonly IReadOnlyList<LessonSection> sections;

    public Lesson(
        LessonId id,
        TopicId topicId,
        string title,
        string objective,
        IEnumerable<LessonSection> sections)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("The lesson identifier cannot be empty.", nameof(id));
        }

        if (topicId.IsEmpty)
        {
            throw new ArgumentException("The topic identifier cannot be empty.", nameof(topicId));
        }

        ArgumentNullException.ThrowIfNull(sections);

        var orderedSections = sections.OrderBy(section => section.Order).ToArray();
        if (orderedSections.Length == 0)
        {
            throw new ArgumentException("A lesson must contain at least one section.", nameof(sections));
        }

        if (orderedSections.Select(section => section.Id).Distinct().Count() != orderedSections.Length)
        {
            throw new ArgumentException("Lesson section identifiers must be unique.", nameof(sections));
        }

        if (orderedSections.Select(section => section.Order).Distinct().Count() != orderedSections.Length)
        {
            throw new ArgumentException("Lesson section order values must be unique.", nameof(sections));
        }

        Id = id;
        TopicId = topicId;
        Title = Guard.RequiredText(title, nameof(title), 160);
        Objective = Guard.RequiredText(objective, nameof(objective), 500);
        this.sections = Array.AsReadOnly(orderedSections);
    }

    public LessonId Id { get; }

    public TopicId TopicId { get; }

    public string Title { get; }

    public string Objective { get; }

    public IReadOnlyList<LessonSection> Sections => sections;
}
