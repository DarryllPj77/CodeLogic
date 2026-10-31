using CodeLogic.Domain;

namespace CodeLogic.Domain.Curriculum;

public sealed class LessonSection
{
    public LessonSection(LessonSectionId id, string heading, string content, int order)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("The lesson section identifier cannot be empty.", nameof(id));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order);

        Id = id;
        Heading = Guard.RequiredText(heading, nameof(heading), 160);
        Content = Guard.RequiredText(content, nameof(content), 8_000);
        Order = order;
    }

    public LessonSectionId Id { get; }

    public string Heading { get; }

    public string Content { get; }

    public int Order { get; }
}
