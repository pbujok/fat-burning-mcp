namespace FatBurner.Abstractions;

public interface IFatBurningActivityReader
{
    Task<IReadOnlyCollection<FatBurningActivity>> GetFatBurningActivitiesAsync(DateTimeOffset dateAfter);
}