using CodeLogic.Application.Curriculum;
using CodeLogic.Application.Progress;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CodeLogic.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddCodeLogicPersistence(
        this IServiceCollection services,
        string connectionString,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var connectionStringBuilder = new SqliteConnectionStringBuilder(connectionString);
        if (!string.Equals(connectionStringBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
            && !Path.IsPathRooted(connectionStringBuilder.DataSource))
        {
            connectionStringBuilder.DataSource = Path.GetFullPath(
                Path.Combine(contentRootPath, connectionStringBuilder.DataSource));
        }

        var databaseDirectory = Path.GetDirectoryName(connectionStringBuilder.DataSource);
        if (!string.IsNullOrEmpty(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        services.AddDbContext<CodeLogicDbContext>(options =>
            options.UseSqlite(connectionStringBuilder.ConnectionString));
        services.AddScoped<ICurriculumCatalog, EfCurriculumCatalog>();
        services.AddScoped<IUserLearningProgressStore, EfUserLearningProgressStore>();

        return services;
    }
}
