using FatBurner.Abstractions;
using FatBurner.Mcp.Cache;

namespace FatBurner.Mcp;

public class FatBurningActivityReader(IFatBurningActivityReader reader, IActivityCache cache)
{
    public async Task<IReadOnlyCollection<FatBurningActivity>> GetFatBurningActivitiesAsync()
    {
        var dateFrom = DateTime.UtcNow.AddDays(-30);
        var lastCachedActivityDate = await cache.GetLastCachedActivityDateAsync();
        var activities = await reader.GetFatBurningActivitiesAsync(lastCachedActivityDate ?? dateFrom);
        await cache.CacheActivityAsync(activities);

        return await cache.GetCachedActivitiesAsync(dateFrom);
    }
}