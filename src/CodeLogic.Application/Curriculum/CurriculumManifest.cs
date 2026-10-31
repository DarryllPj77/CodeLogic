using System.Collections.ObjectModel;

using CodeLogic.Domain.Curriculum;
using CodeLogic.Domain.Practice;

namespace CodeLogic.Application.Curriculum;

public sealed record SafeSnippetPolicy(
    int MaximumCharacters,
    int MaximumNestingDepth,
    string ExecutionPolicy,
    IReadOnlyList<string> AllowedConstructs,
    IReadOnlyList<string> ForbiddenCapabilities);

public sealed record QuizCriteria(
    QuizResponseKind ResponseKind,
    string AssessmentGoal,
    string ExpectedAnswer,
    AnswerNormalization Normalization,
    string Explanation);

public sealed record CurriculumLesson(
    int Order,
    CurriculumLessonStatus Status,
    CurriculumDifficulty Difficulty,
    Lesson Lesson,
    Exercise Exercise,
    string SampleCode,
    IReadOnlyList<string> AllowedConcepts,
    QuizCriteria QuizCriteria)
{
    public string StatusLabel => Status switch
    {
        CurriculumLessonStatus.Complete => "Complete pilot",
        CurriculumLessonStatus.Draft => "Draft",
        _ => "Unknown",
    };

    public string DifficultyLabel => Difficulty switch
    {
        CurriculumDifficulty.BeginnerOne => "Beginner 1",
        CurriculumDifficulty.BeginnerTwo => "Beginner 2",
        CurriculumDifficulty.BeginnerThree => "Beginner 3",
        _ => "Unknown",
    };
}

public sealed class CurriculumManifest
{
    public CurriculumManifest(
        int schemaVersion,
        SafeSnippetPolicy safeSnippetPolicy,
        IEnumerable<Topic> topics,
        IEnumerable<CurriculumLesson> lessons)
    {
        ArgumentNullException.ThrowIfNull(safeSnippetPolicy);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(lessons);

        SchemaVersion = schemaVersion;
        SafeSnippetPolicy = safeSnippetPolicy with
        {
            AllowedConstructs = Array.AsReadOnly(safeSnippetPolicy.AllowedConstructs.ToArray()),
            ForbiddenCapabilities = Array.AsReadOnly(safeSnippetPolicy.ForbiddenCapabilities.ToArray()),
        };
        Topics = Array.AsReadOnly(topics.ToArray());
        Lessons = Array.AsReadOnly(lessons.OrderBy(lesson => lesson.Order).ToArray());
    }

    public int SchemaVersion { get; }

    public SafeSnippetPolicy SafeSnippetPolicy { get; }

    public ReadOnlyCollection<Topic> Topics { get; }

    public ReadOnlyCollection<CurriculumLesson> Lessons { get; }
}
