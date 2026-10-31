namespace CodeLogic.Infrastructure.Persistence;

public sealed class CurriculumLessonEntity
{
    public Guid Id { get; set; }

    public Guid TopicId { get; set; }

    public int Order { get; set; }

    public int Status { get; set; }

    public int Difficulty { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Objective { get; set; } = string.Empty;

    public string SectionsJson { get; set; } = string.Empty;

    public Guid ExerciseId { get; set; }

    public string ExerciseTitle { get; set; } = string.Empty;

    public string ExercisePrompt { get; set; } = string.Empty;

    public int ExerciseKind { get; set; }

    public string SampleCode { get; set; } = string.Empty;

    public string AllowedConceptsJson { get; set; } = string.Empty;

    public int QuizResponseKind { get; set; }

    public string QuizAssessmentGoal { get; set; } = string.Empty;

    public string QuizExpectedAnswer { get; set; } = string.Empty;

    public int QuizNormalization { get; set; }

    public string QuizExplanation { get; set; } = string.Empty;

    public string QuizAnswerOptionsJson { get; set; } = string.Empty;

    public CurriculumTopicEntity Topic { get; set; } = null!;
}
