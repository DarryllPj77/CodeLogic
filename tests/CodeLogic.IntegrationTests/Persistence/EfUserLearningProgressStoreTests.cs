using CodeLogic.Application.Curriculum;
using CodeLogic.Domain.Progress;
using CodeLogic.Infrastructure.Identity;
using CodeLogic.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CodeLogic.IntegrationTests.Persistence;

public sealed class EfUserLearningProgressStoreTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankOwnerIdentifierIsRejected(string userId)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        var store = new EfUserLearningProgressStore(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(() => store.LoadAsync(userId));
    }

    [Fact]
    public async Task ProgressIsIsolatedByAuthenticatedOwner()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var firstUser = CreateUser("first@example.test");
        var secondUser = CreateUser("second@example.test");
        dbContext.Users.AddRange(firstUser, secondUser);
        await dbContext.SaveChangesAsync();
        var lesson = GetFirstCompleteLesson(dbContext);
        var store = new EfUserLearningProgressStore(dbContext);

        await store.RecordAttemptAsync(
            firstUser.Id,
            lesson,
            CorrectEvaluation(),
            new DateTimeOffset(2026, 10, 31, 2, 0, 0, TimeSpan.Zero));

        var firstProgress = await store.LoadAsync(firstUser.Id);
        var secondProgress = await store.LoadAsync(secondUser.Id);

        Assert.Single(firstProgress.Attempts);
        Assert.True(firstProgress.IsLessonComplete(lesson.Lesson.Id));
        Assert.Empty(secondProgress.Attempts);
        Assert.False(secondProgress.IsLessonComplete(lesson.Lesson.Id));
    }

    [Fact]
    public async Task AttemptsAndCompletionPersistAcrossContextRestart()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"codelogic-owned-progress-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var user = CreateUser("learner@example.test");
        Guid lessonId;

        try
        {
            await using (var firstContext = CreateDbContext(connectionString))
            {
                await firstContext.Database.MigrateAsync();
                firstContext.Users.Add(user);
                await firstContext.SaveChangesAsync();
                var lesson = GetFirstCompleteLesson(firstContext);
                lessonId = lesson.Lesson.Id.Value;

                await new EfUserLearningProgressStore(firstContext).RecordAttemptAsync(
                    user.Id,
                    lesson,
                    CorrectEvaluation(),
                    new DateTimeOffset(2026, 10, 31, 2, 30, 0, TimeSpan.Zero));
            }

            await using var restartedContext = CreateDbContext(connectionString);
            var progress = await new EfUserLearningProgressStore(restartedContext)
                .LoadAsync(user.Id);

            var attempt = Assert.Single(progress.Attempts);
            Assert.Equal(AttemptOutcome.Correct, attempt.Outcome);
            Assert.Equal(1, attempt.AttemptNumber);
            Assert.True(progress.IsLessonComplete(new CodeLogic.Domain.LessonId(lessonId)));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task AttemptNumbersAreIndependentForEachUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();
        var firstUser = CreateUser("one@example.test");
        var secondUser = CreateUser("two@example.test");
        dbContext.Users.AddRange(firstUser, secondUser);
        await dbContext.SaveChangesAsync();
        var lesson = GetFirstCompleteLesson(dbContext);
        var store = new EfUserLearningProgressStore(dbContext);

        var firstAttempt = await store.RecordAttemptAsync(
            firstUser.Id,
            lesson,
            IncorrectEvaluation(),
            new DateTimeOffset(2026, 10, 31, 3, 0, 0, TimeSpan.Zero));
        var secondAttempt = await store.RecordAttemptAsync(
            secondUser.Id,
            lesson,
            IncorrectEvaluation(),
            new DateTimeOffset(2026, 10, 31, 3, 1, 0, TimeSpan.Zero));

        Assert.Equal(1, firstAttempt.AttemptNumber);
        Assert.Equal(1, secondAttempt.AttemptNumber);
    }

    private static CodeLogicUser CreateUser(string email) => new()
    {
        Id = Guid.NewGuid().ToString("D"),
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        SecurityStamp = Guid.NewGuid().ToString("D"),
    };

    private static CurriculumLesson GetFirstCompleteLesson(CodeLogicDbContext dbContext) =>
        new EfCurriculumCatalog(dbContext).GetManifest().Lessons
            .First(lesson => lesson.Status == CurriculumLessonStatus.Complete);

    private static QuizEvaluationResult CorrectEvaluation() => new(
        QuizEvaluationStatus.Correct,
        "Correct.",
        "Explanation.");

    private static QuizEvaluationResult IncorrectEvaluation() => new(
        QuizEvaluationStatus.Incorrect,
        "Try again.",
        "Explanation.");

    private static CodeLogicDbContext CreateDbContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<CodeLogicDbContext>()
            .UseSqlite(connection)
            .Options);

    private static CodeLogicDbContext CreateDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<CodeLogicDbContext>()
            .UseSqlite(connectionString)
            .Options);
}
