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

    public async Task<ActivityStreams?> GetActivityStreamsAsync(string activityId, int? maxPoints = null)
    {
        var streams = await cache.GetCachedStreamsAsync(activityId);
        if (streams is null)
        {
            streams = await reader.GetActivityStreamsAsync(activityId);
            if (streams is null)
                return null;

            await cache.CacheStreamsAsync(streams);
        }

        return maxPoints is { } points ? streams.Downsample(points) : streams;
    }
}