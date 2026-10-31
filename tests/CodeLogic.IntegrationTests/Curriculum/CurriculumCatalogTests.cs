using CodeLogic.Application.Curriculum;
using CodeLogic.Infrastructure.Curriculum;

namespace CodeLogic.IntegrationTests.Curriculum;

public sealed class CurriculumCatalogTests
{
    [Fact]
    public void SeedManifestPassesValidationAndCoversRequiredCurriculum()
    {
        var manifest = CreateManifest();

        var errors = CurriculumManifestValidator.Validate(manifest);

        Assert.Empty(errors);
        Assert.Equal(9, manifest.Topics.Count);
        Assert.Equal(9, manifest.Lessons.Count);
        Assert.Equal(2, manifest.Lessons.Count(lesson => lesson.Status == CurriculumLessonStatus.Complete));
        Assert.Equal(7, manifest.Lessons.Count(lesson => lesson.Status == CurriculumLessonStatus.Draft));
        Assert.Equal("static-analysis-only", manifest.SafeSnippetPolicy.ExecutionPolicy);
    }

    [Fact]
    public void CompletePilotsContainDisplayableContentAndQuizCriteria()
    {
        var completeLessons = CreateManifest().Lessons
            .Where(lesson => lesson.Status == CurriculumLessonStatus.Complete)
            .ToArray();

        Assert.All(
            completeLessons,
            lesson =>
            {
                Assert.True(lesson.Lesson.Sections.Count >= 2);
                Assert.False(string.IsNullOrWhiteSpace(lesson.Lesson.Objective));
                Assert.False(string.IsNullOrWhiteSpace(lesson.SampleCode));
                Assert.False(string.IsNullOrWhiteSpace(lesson.QuizCriteria.AssessmentGoal));
                Assert.NotEmpty(lesson.AllowedConcepts);
            });

        var multipleChoice = Assert.Single(
            completeLessons,
            lesson => lesson.QuizCriteria.ResponseKind == QuizResponseKind.MultipleChoice);
        Assert.Contains(
            multipleChoice.QuizCriteria.ExpectedAnswer,
            multipleChoice.QuizCriteria.AnswerOptions);

        var valueInput = Assert.Single(
            completeLessons,
            lesson => lesson.QuizCriteria.ResponseKind == QuizResponseKind.ValueInput);
        Assert.Empty(valueInput.QuizCriteria.AnswerOptions);
    }

    [Fact]
    public void ValidatorRejectsUnsafeExecutionPolicy()
    {
        var source = CreateManifest();
        var unsafePolicy = source.SafeSnippetPolicy with { ExecutionPolicy = "execute-code" };
        var manifest = new CurriculumManifest(1, unsafePolicy, source.Topics, source.Lessons);

        var errors = CurriculumManifestValidator.Validate(manifest);

        Assert.Contains(errors, error => error.Contains("static-analysis-only", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatorRejectsUnknownSafeConceptAndDuplicateOrder()
    {
        var source = CreateManifest();
        var lessons = source.Lessons.ToArray();
        lessons[0] = lessons[0] with { AllowedConcepts = ["arbitrary-code-execution"] };
        lessons[1] = lessons[1] with { Order = lessons[0].Order };
        var manifest = new CurriculumManifest(1, source.SafeSnippetPolicy, source.Topics, lessons);

        var errors = CurriculumManifestValidator.Validate(manifest);

        Assert.Contains(errors, error => error.Contains("unknown safe concept", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Lesson order", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatorRejectsInvalidAnswerOptionShapes()
    {
        var source = CreateManifest();
        var lessons = source.Lessons.ToArray();
        lessons[0] = lessons[0] with
        {
            QuizCriteria = lessons[0].QuizCriteria with
            {
                AnswerOptions = ["double", "string"],
            },
        };
        lessons[1] = lessons[1] with
        {
            QuizCriteria = lessons[1].QuizCriteria with
            {
                AnswerOptions = ["true", "false"],
            },
        };
        var manifest = new CurriculumManifest(1, source.SafeSnippetPolicy, source.Topics, lessons);

        var errors = CurriculumManifestValidator.Validate(manifest);

        Assert.Contains(errors, error => error.Contains("must contain the expected answer", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("cannot define multiple-choice options", StringComparison.Ordinal));
    }

    private static CurriculumManifest CreateManifest() =>
        new SeededCurriculumCatalog().GetManifest();
}
