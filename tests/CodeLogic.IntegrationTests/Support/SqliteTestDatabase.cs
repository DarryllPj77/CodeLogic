using CodeLogic.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CodeLogic.IntegrationTests.Support;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private SqliteTestDatabase(SqliteConnection connection, CodeLogicDbContext context)
    {
        this.connection = connection;
        Context = context;
    }

    public CodeLogicDbContext Context { get; }

    public static async Task<SqliteTestDatabase> CreateMigratedAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = CreateContext(connection);

        try
        {
            await context.Database.MigrateAsync();
            return new SqliteTestDatabase(connection, context);
        }
        catch
        {
            await context.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    public static CodeLogicDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<CodeLogicDbContext>()
            .UseSqlite(connection)
            .Options);

    public static CodeLogicDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<CodeLogicDbContext>()
            .UseSqlite(connectionString)
            .Options);

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}
