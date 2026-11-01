using CodeLogic.Infrastructure.Persistence;
using CodeLogic.IntegrationTests.Support;

using Microsoft.Extensions.Caching.Memory;

namespace CodeLogic.IntegrationTests.Persistence;

public sealed class CachedCurriculumCatalogTests
{
    [Fact]
    public async Task RepeatedReadsUseTheBoundedImmutableManifestEntry()
    {
        await using var database = await SqliteTestDatabase.CreateMigratedAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1 });
        var catalog = new CachedCurriculumCatalog(
            new EfCurriculumCatalog(database.Context),
            cache);

        var first = catalog.GetManifest();
        var originalTitle = first.Lessons[0].Lesson.Title;
        database.Context.CurriculumLessons.First().Title = "Changed after cache fill";
        await database.Context.SaveChangesAsync();

        var second = catalog.GetManifest();

        Assert.Same(first, second);
        Assert.Equal(originalTitle, second.Lessons[0].Lesson.Title);
    }
}
