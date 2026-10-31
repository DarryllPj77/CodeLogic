using CodeLogic.Application.Curriculum;
using CodeLogic.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CodeLogic.IntegrationTests.Persistence;

public sealed class CodeLogicDbContextTests
{
    [Fact]
    public async Task MigrationCreatesValidatedSeededCurriculum()
    {
        await using var connection = await OpenInMemoryConnectionAsync();
        await using var dbContext = CreateDbContext(connection);

        await dbContext.Database.MigrateAsync();
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
        await using var firstConnection = await OpenInMemoryConnectionAsync();
        await using var secondConnection = await OpenInMemoryConnectionAsync();
        await using var firstContext = CreateDbContext(firstConnection);
        await using var secondContext = CreateDbContext(secondConnection);
        await firstContext.Database.MigrateAsync();
        await secondContext.Database.MigrateAsync();

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
            await using (var firstContext = CreateDbContext(connectionString))
            {
                await firstContext.Database.MigrateAsync();
                Assert.Equal(9, await firstContext.CurriculumLessons.CountAsync());
            }

            await using (var restartedContext = CreateDbContext(connectionString))
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

    private static async Task<SqliteConnection> OpenInMemoryConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static CodeLogicDbContext CreateDbContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<CodeLogicDbContext>()
            .UseSqlite(connection)
            .Options);

    private static CodeLogicDbContext CreateDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<CodeLogicDbContext>()
            .UseSqlite(connectionString)
            .Options);
}
