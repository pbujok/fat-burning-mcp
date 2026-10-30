using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FatBurner.Abstractions;
using Microsoft.Extensions.Options;

namespace FatBurner.Mcp.Garmin;

public class GarminActivitiesReader(HttpClient httpClient, IOptions<GarminOptions> options) : IFatBurningActivityReader
{
    private const string ApiBaseUrl = "https://personal.connectapi.garmin.com";
    private const string TokenPath = "/personalapitoken-service/personal-api/personal-api-key/token";
    private const int PageSize = 99;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyCollection<FatBurningActivity>> GetFatBurningActivitiesAsync(DateTimeOffset dateAfter)
    {
        var accessToken = await GetAccessTokenAsync();
        var summaries = await FetchActivitiesListAsync(accessToken, dateAfter);
        var result = new List<FatBurningActivity>(summaries.Count);

        foreach (var summary in summaries)
        {
            var details = await FetchActivityDetailsAsync(accessToken, summary.ActivityId);
            result.Add(MapToFatBurningActivity(summary, details));
        }

        return result;
    }

    private async Task<List<GarminActivitySummary>> FetchActivitiesListAsync(string accessToken, DateTimeOffset dateAfter)
    {
        var activities = new List<GarminActivitySummary>();
        var startDate = dateAfter.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var start = 0;

        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{ApiBaseUrl}/activitylist-service/activities/search/activities?start={start}&limit={PageSize}&startDate={startDate}&endDate={endDate}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var page = await response.Content.ReadFromJsonAsync<List<GarminActivitySummary>>(JsonOptions) ?? [];
            activities.AddRange(page);
            if (page.Count < PageSize)
                return activities;

            start += page.Count;
        }
    }

    private async Task<JsonObject> FetchActivityDetailsAsync(string accessToken, long activityId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{ApiBaseUrl}/activity-service/activity/{activityId.ToString(CultureInfo.InvariantCulture)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions)
               ?? throw new InvalidOperationException($"Garmin returned no details for activity {activityId}.");
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Set Garmin:ApiKey to use the Garmin activity provider.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}{TokenPath}");
        request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var accessToken = (await response.Content.ReadAsStringAsync()).Trim();
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Garmin returned an empty access token.");

        return accessToken;
    }

    private static FatBurningActivity MapToFatBurningActivity(GarminActivitySummary summary, JsonObject details)
    {
        var startTimeLocal = summary.StartTimeLocal
                             ?? throw new InvalidOperationException($"Garmin activity {summary.ActivityId} has no local start time.");
        var startDateTime = DateTime.ParseExact(
            startTimeLocal,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);

        return new FatBurningActivity(
            ActivityId: summary.ActivityId.ToString(CultureInfo.InvariantCulture),
            Title: summary.ActivityName ?? string.Empty,
            Activity: summary.ActivityType?.TypeKey ?? string.Empty,
            Date: startDateTime.Date,
            TimeOfActivity: TimeOnly.FromDateTime(startDateTime),
            Distance: Math.Round((decimal)(summary.Distance ?? 0d) / 1000m, 2),
            SerializedDetails: details.ToJsonString(JsonOptions));
    }

    private sealed record GarminActivitySummary(
        [property: JsonPropertyName("activityId")] long ActivityId,
        [property: JsonPropertyName("activityName")] string? ActivityName,
        [property: JsonPropertyName("activityType")] GarminActivityType? ActivityType,
        [property: JsonPropertyName("startTimeLocal")] string? StartTimeLocal,
        [property: JsonPropertyName("distance")] double? Distance);

    private sealed record GarminActivityType([property: JsonPropertyName("typeKey")] string? TypeKey);
}