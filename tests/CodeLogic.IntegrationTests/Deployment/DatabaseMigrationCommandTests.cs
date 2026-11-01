using CodeLogic.Infrastructure.Persistence;
using CodeLogic.Web.Deployment;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CodeLogic.IntegrationTests.Deployment;

public sealed class DatabaseMigrationCommandTests
{
    [Fact]
    public void MigrationMustBeExplicitlyRequested()
    {
        Assert.True(DatabaseMigrationCommand.IsRequested(["--migrate"]));
        Assert.False(DatabaseMigrationCommand.IsRequested([]));
        Assert.False(DatabaseMigrationCommand.IsRequested(["--MIGRATE"]));
    }

    [Fact]
    public async Task ExplicitCommandAppliesMigrationsAndSeedToEmptyDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CodeLogicDbContext>()
            .UseSqlite(connection)
            .Options;
        var services = new ServiceCollection();
        services.AddScoped(_ => new CodeLogicDbContext(options));
        await using var provider = services.BuildServiceProvider();

        await DatabaseMigrationCommand.ApplyAsync(provider);

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CodeLogicDbContext>();
        Assert.Equal(9, await dbContext.CurriculumLessons.CountAsync());
    }
}
