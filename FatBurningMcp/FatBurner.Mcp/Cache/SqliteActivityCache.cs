using System.Text.Json;
using FatBurner.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FatBurner.Mcp.Cache;

public class SqliteActivityCache(ActivityCacheDbContext context) : IActivityCache
{
    public async Task<DateTime?> GetLastCachedActivityDateAsync()
    {
        if (!await context.Activities.AnyAsync())
            return null;

        return await context.Activities.MaxAsync(a => a.Date);
    }

    public async Task<IReadOnlyCollection<FatBurningActivity>> GetCachedActivitiesAsync(DateTime fromDate)
        => await context.Activities
            .Where(a => a.Date >= fromDate)
            .OrderBy(a => a.Date)
            .Select(e => new FatBurningActivity(
                e.ActivityId, e.Title, e.Activity, e.Date, e.TimeOfActivity, e.Distance, e.SerializedDetails))
            .ToListAsync();

    public async Task CacheActivityAsync(IReadOnlyCollection<FatBurningActivity> activities)
    {
        foreach (var activity in activities)
        {
            var existing = await context.Activities.FindAsync(activity.ActivityId);
            if (existing is null)
            {
                context.Activities.Add(new ActivityEntity
                {
                    ActivityId = activity.ActivityId,
                    Title = activity.Title,
                    Activity = activity.Activity,
                    Date = activity.Date,
                    TimeOfActivity = activity.TimeOfActivity,
                    Distance = activity.Distance,
                    SerializedDetails = activity.SerializedDetails
                });
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task<ActivityStreams?> GetCachedStreamsAsync(string activityId)
    {
        var serialized = await context.ActivityStreams
            .Where(s => s.ActivityId == activityId)
            .Select(s => s.SerializedStreams)
            .FirstOrDefaultAsync();

        return serialized is null ? null : JsonSerializer.Deserialize<ActivityStreams>(serialized);
    }

    // OR IGNORE: parallel tool calls can fetch the same activity, and both results are identical.
    public async Task CacheStreamsAsync(ActivityStreams streams)
    {
        var serialized = JsonSerializer.Serialize(streams);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT OR IGNORE INTO "ActivityStreams" ("ActivityId", "SerializedStreams") VALUES ({streams.ActivityId}, {serialized})""");
    }
}