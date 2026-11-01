using CodeLogic.Application.Curriculum;

using Microsoft.Extensions.Caching.Memory;

namespace CodeLogic.Infrastructure.Persistence;

public sealed class CachedCurriculumCatalog(
    EfCurriculumCatalog innerCatalog,
    IMemoryCache cache) : ICurriculumCatalog
{
    private const string ManifestCacheKey = "codelogic:curriculum:manifest:v1";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public CurriculumManifest GetManifest()
    {
        if (cache.TryGetValue(ManifestCacheKey, out CurriculumManifest? cachedManifest)
            && cachedManifest is not null)
        {
            return cachedManifest;
        }

        var manifest = innerCatalog.GetManifest();
        cache.Set(
            ManifestCacheKey,
            manifest,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = 1,
            });

        return manifest;
    }
}
