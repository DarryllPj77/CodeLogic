using CodeLogic.Domain;

namespace CodeLogic.Domain.Curriculum;

public sealed class Topic
{
    public Topic(TopicId id, string slug, string title, string description)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("The topic identifier cannot be empty.", nameof(id));
        }

        Id = id;
        Slug = ValidateSlug(Guard.RequiredText(slug, nameof(slug), 80));
        Title = Guard.RequiredText(title, nameof(title), 120);
        Description = Guard.RequiredText(description, nameof(description), 500);
    }

    public TopicId Id { get; }

    public string Slug { get; }

    public string Title { get; }

    public string Description { get; }

    private static string ValidateSlug(string slug)
    {
        var containsInvalidCharacter = slug.Any(
            character => !char.IsAsciiLetterLower(character)
                && !char.IsAsciiDigit(character)
                && character != '-');

        if (containsInvalidCharacter
            || slug[0] == '-'
            || slug[^1] == '-'
            || slug.Contains("--", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The slug must contain lowercase ASCII letters, digits, and single hyphens only.",
                nameof(slug));
        }

        return slug;
    }
}
