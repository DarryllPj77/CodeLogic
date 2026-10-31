namespace CodeLogic.Application.Curriculum;

public enum CurriculumLessonStatus
{
    Complete = 1,
    Draft = 2,
}

public enum CurriculumDifficulty
{
    BeginnerOne = 1,
    BeginnerTwo = 2,
    BeginnerThree = 3,
}

public enum QuizResponseKind
{
    MultipleChoice = 1,
    ValueInput = 2,
}

public enum AnswerNormalization
{
    Exact = 1,
    Trim = 2,
    TrimAndIgnoreCase = 3,
}
