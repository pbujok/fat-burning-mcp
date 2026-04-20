using System.Net.Http.Json;
using System.Text.Json;
using FatBurner.Abstractions;
using Microsoft.Extensions.Options;

namespace FatBurner.Strava;

public class StravaActivitiesReader(HttpClient httpClient, IOptions<StravaOptions> options) : IFatBurningActivityReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<IReadOnlyCollection<FatBurningActivity>> GetFatBurningActivitiesAsync()
    {
        var accessToken = await GetAccessTokenAsync();

        var after = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.strava.com/api/v3/athlete/activities?after={after}&per_page=100");
        request.Headers.Authorization = new("Bearer", accessToken);

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var activities = await response.Content.ReadFromJsonAsync<List<StravaActivity>>(JsonOptions) ?? [];

        return activities.Select(MapToFatBurningActivity).ToList();
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var config = options.Value;
        var tokenResponse = await httpClient.PostAsync(
            "https://www.strava.com/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = config.ClientId,
                ["client_secret"] = config.ClientSecret,
                ["refresh_token"] = config.RefreshToken,
                ["grant_type"] = "refresh_token"
            }));

        tokenResponse.EnsureSuccessStatusCode();

        var token = await tokenResponse.Content.ReadFromJsonAsync<StravaTokenResponse>(JsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize Strava token response.");

        return token.AccessToken;
    }

    private static FatBurningActivity MapToFatBurningActivity(StravaActivity activity)
    {
        var startDate = DateTime.Parse(activity.StartDate, null, System.Globalization.DateTimeStyles.RoundtripKind);

        return new FatBurningActivity(
            Title: activity.Name,
            Activity: activity.SportType,
            Date: startDate.Date,
            TimeOfActivity: TimeOnly.FromDateTime(startDate.ToLocalTime()),
            Distance: Math.Round((decimal)activity.Distance / 1000, 2),
            SerializedDetails: JsonSerializer.Serialize(activity, JsonOptions));
    }

    private record StravaTokenResponse(string AccessToken, string TokenType, int ExpiresAt, string RefreshToken);

    private record StravaActivity(
        long Id,
        string Name,
        string SportType,
        string StartDate,
        float Distance,
        int MovingTime,
        int ElapsedTime,
        float TotalElevationGain,
        float AverageSpeed,
        float MaxSpeed,
        float? AverageHeartrate,
        float? MaxHeartrate,
        float? Calories);
}
