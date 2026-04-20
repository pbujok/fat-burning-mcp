using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FatBurner.Abstractions;
using Microsoft.Extensions.Options;

namespace FatBurner.Strava;

public class StravaActivitiesReader(HttpClient httpClient, IOptions<StravaOptions> options) : IFatBurningActivityReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<IReadOnlyCollection<FatBurningActivity>> GetFatBurningActivitiesAsync(DateTimeOffset dateAfter)
    {
        var accessToken = await GetAccessTokenAsync();

        var summaries = await FetchActivitiesListAsync(accessToken, dateAfter.ToUnixTimeSeconds());

        var result = new List<FatBurningActivity>(summaries.Count);
        foreach (var summary in summaries)
        {
            var details = await FetchActivityDetailsAsync(accessToken, summary.Id);
            result.Add(MapToFatBurningActivity(summary, details));
        }

        return result;
    }

    private async Task<List<StravaActivitySummary>> FetchActivitiesListAsync(string accessToken, long after)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.strava.com/api/v3/athlete/activities?after={after}&page=1&per_page=100");
        request.Headers.Authorization = new("Bearer", accessToken);

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<StravaActivitySummary>>(JsonOptions) ?? [];
    }

    private async Task<JsonObject> FetchActivityDetailsAsync(string accessToken, long id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.strava.com/api/v3/activities/{id}?include_all_efforts=true");
        request.Headers.Authorization = new("Bearer", accessToken);

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions) ?? [];
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

    private static FatBurningActivity MapToFatBurningActivity(StravaActivitySummary summary, JsonObject details)
    {
        var startDate = DateTime.Parse(summary.StartDate, null, System.Globalization.DateTimeStyles.RoundtripKind);

        return new FatBurningActivity(
            ActivityId: summary.Id.ToString(),
            Title: summary.Name,
            Activity: summary.SportType,
            Date: startDate.Date,
            TimeOfActivity: TimeOnly.FromDateTime(startDate.ToLocalTime()),
            Distance: Math.Round((decimal)summary.Distance / 1000, 2),
            SerializedDetails: details.ToJsonString(JsonOptions));
    }

    private record StravaTokenResponse(string AccessToken, string TokenType, int ExpiresAt, string RefreshToken);

    private record StravaActivitySummary(long Id, string Name, string SportType, string StartDate, float Distance);
}
