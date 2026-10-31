using CodeLogic.Application.Curriculum;
using CodeLogic.Domain;
using CodeLogic.Domain.Curriculum;
using CodeLogic.Domain.Practice;
using CodeLogic.Infrastructure.Curriculum;

using Microsoft.EntityFrameworkCore;

namespace CodeLogic.Infrastructure.Persistence;

public sealed class EfCurriculumCatalog(CodeLogicDbContext dbContext) : ICurriculumCatalog
{
    private static readonly CurriculumManifest BaselineManifest =
        new SeededCurriculumCatalog().GetManifest();

    public CurriculumManifest GetManifest()
    {
        var topicEntities = dbContext.CurriculumTopics
            .AsNoTracking()
            .OrderBy(topic => topic.Order)
            .ToArray();
        var topics = topicEntities
            .Select(topic => new Topic(
                new TopicId(topic.Id),
                topic.Slug,
                topic.Title,
                topic.Description))
            .ToArray();
        var topicsById = topics.ToDictionary(topic => topic.Id.Value);
        var lessons = dbContext.CurriculumLessons
            .AsNoTracking()
            .OrderBy(lesson => lesson.Order)
            .AsEnumerable()
            .Select(entity => CreateCurriculumLesson(entity, topicsById))
            .ToArray();
        var manifest = new CurriculumManifest(
            BaselineManifest.SchemaVersion,
            BaselineManifest.SafeSnippetPolicy,
            topics,
            lessons);
        var validationErrors = CurriculumManifestValidator.Validate(manifest);

        if (validationErrors.Count != 0)
        {
            throw new InvalidOperationException(
                $"The persisted curriculum manifest is invalid:{Environment.NewLine}{string.Join(Environment.NewLine, validationErrors)}");
        }

        return manifest;
    }

    private static CurriculumLesson CreateCurriculumLesson(
        CurriculumLessonEntity entity,
        Dictionary<Guid, Topic> topicsById)
    {
        if (!topicsById.TryGetValue(entity.TopicId, out var topic))
        {
            throw new InvalidOperationException(
                $"Persisted lesson '{entity.Title}' references unknown topic '{entity.TopicId}'.");
        }

        var lessonId = new LessonId(entity.Id);
        var lesson = new Lesson(
            lessonId,
            topic.Id,
            entity.Title,
            entity.Objective,
            CurriculumPersistenceSerializer.DeserializeSections(entity.SectionsJson));
        var exercise = new Exercise(
            new ExerciseId(entity.ExerciseId),
            lessonId,
            entity.ExerciseTitle,
            entity.ExercisePrompt,
            (ExerciseKind)entity.ExerciseKind,
            order: 1);
        var quizCriteria = new QuizCriteria(
            (QuizResponseKind)entity.QuizResponseKind,
            entity.QuizAssessmentGoal,
            entity.QuizExpectedAnswer,
            (AnswerNormalization)entity.QuizNormalization,
            entity.QuizExplanation,
            Array.AsReadOnly(
                CurriculumPersistenceSerializer.DeserializeStrings(entity.QuizAnswerOptionsJson)));

        return new CurriculumLesson(
            entity.Order,
            (CurriculumLessonStatus)entity.Status,
            (CurriculumDifficulty)entity.Difficulty,
            lesson,
            exercise,
            entity.SampleCode,
            Array.AsReadOnly(
                CurriculumPersistenceSerializer.DeserializeStrings(entity.AllowedConceptsJson)),
            quizCriteria);
    }
}
