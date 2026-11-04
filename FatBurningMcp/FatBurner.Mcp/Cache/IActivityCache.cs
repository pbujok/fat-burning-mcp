using FatBurner.Abstractions;

namespace FatBurner.Mcp.Cache;

public interface IActivityCache
{
    Task<DateTime?> GetLastCachedActivityDateAsync();
    Task<IReadOnlyCollection<FatBurningActivity>> GetCachedActivitiesAsync(DateTime fromDate);
    Task CacheActivityAsync(IReadOnlyCollection<FatBurningActivity> activity);
    Task<ActivityStreams?> GetCachedStreamsAsync(string activityId);
    Task CacheStreamsAsync(ActivityStreams streams);
}
