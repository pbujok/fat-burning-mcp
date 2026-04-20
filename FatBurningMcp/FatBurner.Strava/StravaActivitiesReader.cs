using FatBurner.Abstractions;

namespace FatBurner.Strava;

public class StravaActivitiesReader : IFatBurningActivityReader
{
    public Task<IReadOnlyCollection<FatBurningActivity>> GetFatBurningActivitiesAsync()
        => Task.FromResult((IReadOnlyCollection<FatBurningActivity>)new List<FatBurningActivity>()
        {
            
        });
}