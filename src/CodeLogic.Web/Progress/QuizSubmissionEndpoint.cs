using CodeLogic.Application.Curriculum;
using CodeLogic.Application.Progress;

using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CodeLogic.Web.Progress;

public static class QuizSubmissionEndpoint
{
    public static IEndpointRouteBuilder MapQuizSubmission(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/lessons/{lessonSlug}/quiz",
            SubmitQuizAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> SubmitQuizAsync(
        string lessonSlug,
        [FromForm] QuizSubmission submission,
        ClaimsPrincipal user,
        ICurriculumCatalog curriculumCatalog,
        IUserLearningProgressStore progressStore,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Challenge();
        }

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

        var attempt = await progressStore.RecordAttemptAsync(
            userId,
            readingContext.Current,
            evaluation,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return Results.LocalRedirect(
            $"/lessons/{readingContext.Slug}?attempt={attempt.Id.Value:D}#practice-preview");
    }
}

public sealed class QuizSubmission
{
    public string? Answer { get; set; }
}
