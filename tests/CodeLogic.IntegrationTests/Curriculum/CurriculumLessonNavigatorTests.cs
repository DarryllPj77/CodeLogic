using CodeLogic.Application.Curriculum;
using CodeLogic.Infrastructure.Curriculum;

namespace CodeLogic.IntegrationTests.Curriculum;

public sealed class CurriculumLessonNavigatorTests
{
    [Fact]
    public void FirstPilotHasNextLessonAndNoPreviousLesson()
    {
        var context = Resolve("variables-and-types");

        Assert.Equal("Variables hold typed values", context.Current.Lesson.Title);
        Assert.Null(context.Previous);
        Assert.Equal("operators", context.Next?.Slug);
        Assert.Equal("Operators create new information", context.Next?.Title);
    }

    [Fact]
    public void SecondPilotHasPreviousLessonAndNoNextLesson()
    {
        var context = Resolve("operators");

        Assert.Equal("Operators create new information", context.Current.Lesson.Title);
        Assert.Equal("variables-and-types", context.Previous?.Slug);
        Assert.Equal("Variables hold typed values", context.Previous?.Title);
        Assert.Null(context.Next);
    }

    [Fact]
    public void LookupIgnoresSlugCaseAndSurroundingWhitespace()
    {
        var context = Resolve("  VARIABLES-AND-TYPES  ");

        Assert.Equal("variables-and-types", context.Slug);
    }

    [Theory]
    [InlineData("loops")]
    [InlineData("missing-lesson")]
    [InlineData("")]
    [InlineData("   ")]
    public void DraftAndMissingSlugsDoNotResolve(string slug)
    {
        var manifest = new SeededCurriculumCatalog().GetManifest();

        var context = CurriculumLessonNavigator.Resolve(manifest, slug);

        Assert.Null(context);
    }

    private static CurriculumLessonReadingContext Resolve(string slug)
    {
        var manifest = new SeededCurriculumCatalog().GetManifest();

        return Assert.IsType<CurriculumLessonReadingContext>(
            CurriculumLessonNavigator.Resolve(manifest, slug));
    }
}
