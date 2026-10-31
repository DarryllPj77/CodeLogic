namespace CodeLogic.Application.Curriculum;

public enum QuizEvaluationStatus
{
    Correct = 1,
    Incorrect = 2,
    AnswerRequired = 3,
}

public sealed record QuizEvaluationResult(
    QuizEvaluationStatus Status,
    string Feedback,
    string? Explanation)
{
    public bool IsCorrect => Status == QuizEvaluationStatus.Correct;
}

public static class DeterministicQuizEvaluator
{
    public static QuizEvaluationResult Evaluate(
        QuizCriteria criteria,
        string? submittedAnswer)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (!Enum.IsDefined(criteria.ResponseKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(criteria),
                criteria.ResponseKind,
                "The quiz response kind is not supported.");
        }

        if (!Enum.IsDefined(criteria.Normalization))
        {
            throw new ArgumentOutOfRangeException(
                nameof(criteria),
                criteria.Normalization,
                "The answer normalization rule is not supported.");
        }

        if (string.IsNullOrWhiteSpace(submittedAnswer))
        {
            return new QuizEvaluationResult(
                QuizEvaluationStatus.AnswerRequired,
                "Enter or choose an answer before checking it.",
                Explanation: null);
        }

        var isAllowedChoice = criteria.ResponseKind != QuizResponseKind.MultipleChoice
            || criteria.AnswerOptions.Any(option => AnswersMatch(
                option,
                submittedAnswer,
                criteria.Normalization));
        var isCorrect = isAllowedChoice && AnswersMatch(
            criteria.ExpectedAnswer,
            submittedAnswer,
            criteria.Normalization);

        return isCorrect
            ? new QuizEvaluationResult(
                QuizEvaluationStatus.Correct,
                "Correct. Your answer matches the lesson specification.",
                criteria.Explanation)
            : new QuizEvaluationResult(
                QuizEvaluationStatus.Incorrect,
                "Not quite. Review the explanation and try again.",
                criteria.Explanation);
    }

    internal static bool AnswersMatch(
        string expectedAnswer,
        string submittedAnswer,
        AnswerNormalization normalization) =>
        normalization switch
        {
            AnswerNormalization.Exact => string.Equals(
                expectedAnswer,
                submittedAnswer,
                StringComparison.Ordinal),
            AnswerNormalization.Trim => string.Equals(
                expectedAnswer.Trim(),
                submittedAnswer.Trim(),
                StringComparison.Ordinal),
            AnswerNormalization.TrimAndIgnoreCase => string.Equals(
                expectedAnswer.Trim(),
                submittedAnswer.Trim(),
                StringComparison.OrdinalIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(
                nameof(normalization),
                normalization,
                "The answer normalization rule is not supported."),
        };
}
