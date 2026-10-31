using CodeLogic.Application.Curriculum;
using CodeLogic.Infrastructure.Curriculum;

namespace CodeLogic.IntegrationTests.Curriculum;

public sealed class DeterministicQuizEvaluatorTests
{
    [Fact]
    public void ExactMultipleChoiceAcceptsOnlyTheSpecifiedAnswer()
    {
        var criteria = CriteriaFor("variables-and-types");

        var correct = DeterministicQuizEvaluator.Evaluate(criteria, "int");
        var wrongCase = DeterministicQuizEvaluator.Evaluate(criteria, "Int");
        var extraWhitespace = DeterministicQuizEvaluator.Evaluate(criteria, " int ");

        Assert.Equal(QuizEvaluationStatus.Correct, correct.Status);
        Assert.True(correct.IsCorrect);
        Assert.Equal(criteria.Explanation, correct.Explanation);
        Assert.Equal(QuizEvaluationStatus.Incorrect, wrongCase.Status);
        Assert.Equal(QuizEvaluationStatus.Incorrect, extraWhitespace.Status);
    }

    [Theory]
    [InlineData("true")]
    [InlineData(" TRUE ")]
    [InlineData("\tTrUe\r\n")]
    public void TrimAndIgnoreCaseAcceptsEquivalentValueInput(string answer)
    {
        var result = DeterministicQuizEvaluator.Evaluate(
            CriteriaFor("operators"),
            answer);

        Assert.Equal(QuizEvaluationStatus.Correct, result.Status);
    }

    [Fact]
    public void IncorrectValueReturnsAuthoredExplanation()
    {
        var criteria = CriteriaFor("operators");

        var result = DeterministicQuizEvaluator.Evaluate(criteria, "false");

        Assert.Equal(QuizEvaluationStatus.Incorrect, result.Status);
        Assert.False(result.IsCorrect);
        Assert.Equal(criteria.Explanation, result.Explanation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingAnswerRequestsInputWithoutRevealingExplanation(string? answer)
    {
        var result = DeterministicQuizEvaluator.Evaluate(
            CriteriaFor("operators"),
            answer);

        Assert.Equal(QuizEvaluationStatus.AnswerRequired, result.Status);
        Assert.Null(result.Explanation);
    }

    [Fact]
    public void TrimRuleRemovesOuterWhitespaceButPreservesCase()
    {
        var criteria = CriteriaFor("operators") with
        {
            ExpectedAnswer = "Ready",
            Normalization = AnswerNormalization.Trim,
        };

        var correct = DeterministicQuizEvaluator.Evaluate(criteria, "  Ready\t");
        var wrongCase = DeterministicQuizEvaluator.Evaluate(criteria, "ready");

        Assert.Equal(QuizEvaluationStatus.Correct, correct.Status);
        Assert.Equal(QuizEvaluationStatus.Incorrect, wrongCase.Status);
    }

    [Fact]
    public void CodeLikeTextIsComparedAsData()
    {
        const string codeLikeAnswer = "System.Diagnostics.Process.Start(\"calc\")";

        var result = DeterministicQuizEvaluator.Evaluate(
            CriteriaFor("operators"),
            codeLikeAnswer);

        Assert.Equal(QuizEvaluationStatus.Incorrect, result.Status);
    }

    [Fact]
    public void UnsupportedNormalizationIsRejected()
    {
        var criteria = CriteriaFor("operators") with
        {
            Normalization = (AnswerNormalization)999,
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => DeterministicQuizEvaluator.Evaluate(criteria, "true"));
    }

    [Fact]
    public void UnsupportedResponseKindIsRejected()
    {
        var criteria = CriteriaFor("operators") with
        {
            ResponseKind = (QuizResponseKind)999,
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => DeterministicQuizEvaluator.Evaluate(criteria, "true"));
    }

    private static QuizCriteria CriteriaFor(string topicSlug)
    {
        var manifest = new SeededCurriculumCatalog().GetManifest();
        var topic = manifest.Topics.Single(item => item.Slug == topicSlug);

        return manifest.Lessons
            .Single(lesson => lesson.Lesson.TopicId == topic.Id)
            .QuizCriteria;
    }
}
