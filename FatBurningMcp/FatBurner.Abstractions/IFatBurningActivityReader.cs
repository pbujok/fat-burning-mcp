namespace FatBurner.Abstractions;

public interface IFatBurningActivityReader
{
    Task<IReadOnlyCollection<FatBurningActivity>> GetFatBurningActivitiesAsync(DateTimeOffset dateAfter);

    /// <summary>Reads every sample of an activity, or returns null when the provider has no sample data for it.</summary>
    Task<ActivityStreams?> GetActivityStreamsAsync(string activityId);
}
