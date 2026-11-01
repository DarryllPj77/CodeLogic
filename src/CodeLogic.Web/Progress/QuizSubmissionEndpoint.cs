using CodeLogic.Application.Curriculum;
using CodeLogic.Application.Progress;

using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CodeLogic.Web.Progress;

public static partial class QuizSubmissionEndpoint
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
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CodeLogic.Web.QuizSubmission");
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            LogMissingOwner(logger);
            return Results.Challenge();
        }

        var readingContext = CurriculumLessonNavigator.Resolve(
            curriculumCatalog.GetManifest(),
            lessonSlug);
        if (readingContext is null)
        {
            LogUnknownLesson(logger, lessonSlug);
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

        LogAttemptRecorded(
            logger,
            attempt.Id.Value,
            readingContext.Slug,
            attempt.Outcome.ToString());

        return Results.LocalRedirect(
            $"/lessons/{readingContext.Slug}?attempt={attempt.Id.Value:D}#practice-preview");
    }

    [LoggerMessage(1001, LogLevel.Warning, "Rejected an authorized quiz request without a name identifier claim.")]
    private static partial void LogMissingOwner(ILogger logger);

    [LoggerMessage(1002, LogLevel.Information, "Redirected a quiz submission for unknown lesson slug {LessonSlug}.")]
    private static partial void LogUnknownLesson(ILogger logger, string lessonSlug);

    [LoggerMessage(1003, LogLevel.Information, "Recorded quiz attempt {AttemptId} for lesson {LessonSlug} with outcome {Outcome}.")]
    private static partial void LogAttemptRecorded(
        ILogger logger,
        Guid attemptId,
        string lessonSlug,
        string outcome);
}

public sealed class QuizSubmission
{
    public string? Answer { get; set; }
}
