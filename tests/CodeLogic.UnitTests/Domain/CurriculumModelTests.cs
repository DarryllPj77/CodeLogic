using CodeLogic.Domain;
using CodeLogic.Domain.Curriculum;

namespace CodeLogic.UnitTests.Domain;

public sealed class CurriculumModelTests
{
    [Fact]
    public void TopicTrimsRequiredText()
    {
        var topic = new Topic(
            TopicId.New(),
            "variables-and-types",
            "  Variables and types  ",
            "  Understand names, types, and values.  ");

        Assert.Equal("Variables and types", topic.Title);
        Assert.Equal("Understand names, types, and values.", topic.Description);
    }

    [Theory]
    [InlineData("Variables")]
    [InlineData("variables_and_types")]
    [InlineData("-variables")]
    [InlineData("variables-")]
    [InlineData("variables--types")]
    public void TopicRejectsInvalidSlug(string slug)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Topic(TopicId.New(), slug, "Variables", "Learn variables."));

        Assert.Equal("slug", exception.ParamName);
    }

    [Fact]
    public void LessonCopiesAndOrdersItsSections()
    {
        var firstSection = CreateSection(order: 1, heading: "First");
        var secondSection = CreateSection(order: 2, heading: "Second");
        var source = new List<LessonSection> { secondSection, firstSection };

        var lesson = new Lesson(
            LessonId.New(),
            TopicId.New(),
            "Variables",
            "Explain how variables hold typed values.",
            source);

        source.Add(CreateSection(order: 3, heading: "Later mutation"));

        Assert.Collection(
            lesson.Sections,
            section => Assert.Same(firstSection, section),
            section => Assert.Same(secondSection, section));
    }

    [Fact]
    public void LessonRejectsEmptySectionCollection()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Lesson(
                LessonId.New(),
                TopicId.New(),
                "Variables",
                "Explain variables.",
                []));

        Assert.Equal("sections", exception.ParamName);
    }

    [Fact]
    public void LessonRejectsDuplicateSectionOrder()
    {
        var sections = new[]
        {
            CreateSection(order: 1, heading: "First"),
            CreateSection(order: 1, heading: "Also first"),
        };

        var exception = Assert.Throws<ArgumentException>(
            () => new Lesson(
                LessonId.New(),
                TopicId.New(),
                "Variables",
                "Explain variables.",
                sections));

        Assert.Equal("sections", exception.ParamName);
    }

    [Fact]
    public void LessonSectionRejectsNonPositiveOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LessonSection(
                LessonSectionId.New(),
                "Heading",
                "Content",
                order: 0));
    }

    private static LessonSection CreateSection(int order, string heading) =>
        new(LessonSectionId.New(), heading, $"Content for {heading}.", order);
}
