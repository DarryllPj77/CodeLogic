using CodeLogic.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace CodeLogic.Web.Deployment;

public static class DatabaseMigrationCommand
{
    public const string Argument = "--migrate";

    public static bool IsRequested(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Contains(Argument, StringComparer.Ordinal);
    }

    public static async Task ApplyAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CodeLogicDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
