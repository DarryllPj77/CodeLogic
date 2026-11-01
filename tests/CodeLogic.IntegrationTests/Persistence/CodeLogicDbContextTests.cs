using CodeLogic.Application.Curriculum;
using CodeLogic.Infrastructure.Persistence;
using CodeLogic.IntegrationTests.Support;

using Microsoft.EntityFrameworkCore;

namespace CodeLogic.IntegrationTests.Persistence;

public sealed class CodeLogicDbContextTests
{
    [Fact]
    public async Task MigrationCreatesValidatedSeededCurriculum()
    {
        await using var database = await SqliteTestDatabase.CreateMigratedAsync();
        var dbContext = database.Context;
        var manifest = new EfCurriculumCatalog(dbContext).GetManifest();

        Assert.Empty(CurriculumManifestValidator.Validate(manifest));
        Assert.Equal(9, manifest.Topics.Count);
        Assert.Equal(9, manifest.Lessons.Count);
        Assert.Equal(
            2,
            manifest.Lessons.Count(lesson => lesson.Status == CurriculumLessonStatus.Complete));
    }

    [Fact]
    public async Task EachTestDatabaseIsIsolated()
    {
        await using var firstDatabase = await SqliteTestDatabase.CreateMigratedAsync();
        await using var secondDatabase = await SqliteTestDatabase.CreateMigratedAsync();
        var firstContext = firstDatabase.Context;
        var secondContext = secondDatabase.Context;

        await firstContext.CurriculumLessons.ExecuteDeleteAsync();

        Assert.Equal(0, await firstContext.CurriculumLessons.CountAsync());
        Assert.Equal(9, await secondContext.CurriculumLessons.CountAsync());
    }

    [Fact]
    public async Task FileDatabaseRetainsSeedAcrossContextRestart()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"codelogic-persistence-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";

        try
        {
            await using (var firstContext = SqliteTestDatabase.CreateContext(connectionString))
            {
                await firstContext.Database.MigrateAsync();
                Assert.Equal(9, await firstContext.CurriculumLessons.CountAsync());
            }

            await using (var restartedContext = SqliteTestDatabase.CreateContext(connectionString))
            {
                await restartedContext.Database.MigrateAsync();
                var manifest = new EfCurriculumCatalog(restartedContext).GetManifest();

                Assert.Equal(9, manifest.Topics.Count);
                Assert.Equal(9, manifest.Lessons.Count);
                Assert.Equal("variables-and-types", manifest.Topics[0].Slug);
            }
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

}
