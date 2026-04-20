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
}