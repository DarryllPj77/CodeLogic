using CodeLogic.Infrastructure.Curriculum;

namespace CodeLogic.Infrastructure.Persistence;

internal static class CurriculumPersistenceSeed
{
    public static CurriculumTopicEntity[] CreateTopics()
    {
        var manifest = new SeededCurriculumCatalog().GetManifest();
        var lessonOrderByTopic = manifest.Lessons.ToDictionary(
            lesson => lesson.Lesson.TopicId,
            lesson => lesson.Order);

        return manifest.Topics
            .Select(topic => new CurriculumTopicEntity
            {
                Id = topic.Id.Value,
                Order = lessonOrderByTopic[topic.Id],
                Slug = topic.Slug,
                Title = topic.Title,
                Description = topic.Description,
            })
            .ToArray();
    }

    public static CurriculumLessonEntity[] CreateLessons() =>
        new SeededCurriculumCatalog().GetManifest().Lessons
            .Select(lesson => new CurriculumLessonEntity
            {
                Id = lesson.Lesson.Id.Value,
                TopicId = lesson.Lesson.TopicId.Value,
                Order = lesson.Order,
                Status = (int)lesson.Status,
                Difficulty = (int)lesson.Difficulty,
                Title = lesson.Lesson.Title,
                Objective = lesson.Lesson.Objective,
                SectionsJson = CurriculumPersistenceSerializer.SerializeSections(lesson.Lesson.Sections),
                ExerciseId = lesson.Exercise.Id.Value,
                ExerciseTitle = lesson.Exercise.Title,
                ExercisePrompt = lesson.Exercise.Prompt,
                ExerciseKind = (int)lesson.Exercise.Kind,
                SampleCode = lesson.SampleCode,
                AllowedConceptsJson = CurriculumPersistenceSerializer.SerializeStrings(lesson.AllowedConcepts),
                QuizResponseKind = (int)lesson.QuizCriteria.ResponseKind,
                QuizAssessmentGoal = lesson.QuizCriteria.AssessmentGoal,
                QuizExpectedAnswer = lesson.QuizCriteria.ExpectedAnswer,
                QuizNormalization = (int)lesson.QuizCriteria.Normalization,
                QuizExplanation = lesson.QuizCriteria.Explanation,
                QuizAnswerOptionsJson = CurriculumPersistenceSerializer.SerializeStrings(
                    lesson.QuizCriteria.AnswerOptions),
            })
            .ToArray();
}
