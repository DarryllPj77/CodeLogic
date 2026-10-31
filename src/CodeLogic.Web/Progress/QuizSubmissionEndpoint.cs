using CodeLogic.Application.Curriculum;

using Microsoft.AspNetCore.Mvc;

namespace CodeLogic.Web.Progress;

public static class QuizSubmissionEndpoint
{
    public static IEndpointRouteBuilder MapQuizSubmission(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/lessons/{lessonSlug}/quiz",
            SubmitQuiz);

        return endpoints;
    }

    private static IResult SubmitQuiz(
        string lessonSlug,
        [FromForm] QuizSubmission submission,
        ICurriculumCatalog curriculumCatalog,
        SessionLearningProgressStore progressStore,
        TimeProvider timeProvider)
    {
        var readingContext = CurriculumLessonNavigator.Resolve(
            curriculumCatalog.GetManifest(),
            lessonSlug);
        if (readingContext is null)
        {
            return Results.LocalRedirect("/lessons");
        }

        var evaluation = DeterministicQuizEvaluator.Evaluate(
            readingContext.Current.QuizCriteria,
            submission.Answer);
        if (evaluation.Status == QuizEvaluationStatus.AnswerRequired)
        {
            return Results.LocalRedirect(
                $"/lessons/{readingContext.Slug}?answerRequired=true#practice-preview");
        }

        var attempt = progressStore.RecordAttempt(
            readingContext.Current,
            evaluation,
            timeProvider.GetUtcNow());

        return Results.LocalRedirect(
            $"/lessons/{readingContext.Slug}?attempt={attempt.Id.Value:D}#practice-preview");
    }
}

public sealed class QuizSubmission
{
    public string? Answer { get; set; }
}
