using CodeLogic.Domain.Curriculum;

namespace CodeLogic.Application.Curriculum;

public sealed record CurriculumLessonLink(string Slug, string Title);

public sealed record CurriculumLessonReadingContext(
    string Slug,
    Topic Topic,
    CurriculumLesson Current,
    CurriculumLessonLink? Previous,
    CurriculumLessonLink? Next);

public static class CurriculumLessonNavigator
{
    public static CurriculumLessonReadingContext? Resolve(
        CurriculumManifest manifest,
        string? slug)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var completeLessons = manifest.Lessons
            .Where(lesson => lesson.Status == CurriculumLessonStatus.Complete)
            .OrderBy(lesson => lesson.Order)
            .Select(lesson => new LessonWithTopic(
                lesson,
                manifest.Topics.Single(topic => topic.Id == lesson.Lesson.TopicId)))
            .ToArray();
        var currentIndex = Array.FindIndex(
            completeLessons,
            item => string.Equals(item.Topic.Slug, slug.Trim(), StringComparison.OrdinalIgnoreCase));

        if (currentIndex < 0)
        {
            return null;
        }

        var current = completeLessons[currentIndex];
        var previous = currentIndex > 0
            ? CreateLink(completeLessons[currentIndex - 1])
            : null;
        var next = currentIndex < completeLessons.Length - 1
            ? CreateLink(completeLessons[currentIndex + 1])
            : null;

        return new CurriculumLessonReadingContext(
            current.Topic.Slug,
            current.Topic,
            current.Lesson,
            previous,
            next);
    }

    private static CurriculumLessonLink CreateLink(LessonWithTopic item) =>
        new(item.Topic.Slug, item.Lesson.Lesson.Title);

    private sealed record LessonWithTopic(CurriculumLesson Lesson, Topic Topic);
}
