using System.Net;
using System.Text;
using System.Text.Json;
using FatBurner.Mcp.Garmin;
using Microsoft.Extensions.Options;
using Xunit;

namespace FatBurner.Garmin.Tests;

public class GarminActivitiesReaderTests
{
    [Fact]
    public async Task ReadsAllPagesAndMapsLocalActivityData()
    {
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
                return TextResponse("test-access-token");

            if (request.RequestUri!.AbsolutePath == "/activitylist-service/activities/search/activities")
            {
                var start = int.Parse(GetQueryValue(request.RequestUri, "start"));
                return JsonResponse(CreateSummaries(start, start == 0 ? 99 : 1));
            }

            return JsonResponse("{\"activityId\":1,\"averageSpeed\":3.2}");
        });
        using var client = new HttpClient(handler);
        var reader = CreateReader(client);

        var activities = await reader.GetFatBurningActivitiesAsync(
            new DateTimeOffset(2024, 8, 10, 23, 59, 0, TimeSpan.FromHours(5)));

        Assert.Equal(100, activities.Count);
        var firstActivity = activities.First();
        Assert.Equal("1", firstActivity.ActivityId);
        Assert.Equal("Run 1", firstActivity.Title);
        Assert.Equal("running", firstActivity.Activity);
        Assert.Equal(new DateTime(2024, 8, 10), firstActivity.Date);
        Assert.Equal(new TimeOnly(6, 7, 8), firstActivity.TimeOfActivity);
        Assert.Equal(5.28m, firstActivity.Distance);
        Assert.Contains("\"averageSpeed\":3.2", firstActivity.SerializedDetails);

        var searchRequests = handler.Requests
            .Where(request => request.Uri.AbsolutePath == "/activitylist-service/activities/search/activities")
            .ToArray();
        Assert.Equal(new[] { 0, 99 }, searchRequests.Select(request => int.Parse(GetQueryValue(request.Uri, "start"))));
        Assert.All(searchRequests, request =>
        {
            Assert.Equal("99", GetQueryValue(request.Uri, "limit"));
            Assert.Equal("2024-08-10", GetQueryValue(request.Uri, "startDate"));
            Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), GetQueryValue(request.Uri, "endDate"));
        });

        Assert.Equal("https://personal.connectapi.garmin.com/personalapitoken-service/personal-api/personal-api-key/token", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("ApiKey secret-key", handler.Requests[0].Authorization);
        Assert.Equal("Bearer test-access-token", searchRequests[0].Authorization);
        Assert.Equal("Bearer test-access-token", handler.Requests.First(request => request.Uri.AbsolutePath.StartsWith("/activity-service/activity/")).Authorization);
        Assert.Equal(100, handler.Requests.Count(request => request.Uri.AbsolutePath.StartsWith("/activity-service/activity/")));
    }

    [Fact]
    public async Task ReturnsEmptyCollectionWhenSearchHasNoActivities()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Post
            ? TextResponse("test-access-token")
            : JsonResponse("[]"));
        using var client = new HttpClient(handler);
        var reader = CreateReader(client);

        var activities = await reader.GetFatBurningActivitiesAsync(DateTimeOffset.UtcNow.AddDays(-1));

        Assert.Empty(activities);
        Assert.Single(handler.Requests, request => request.Uri.AbsolutePath == "/activitylist-service/activities/search/activities");
    }

    [Fact]
    public async Task MapsMissingDistanceToZero()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Post
            ? TextResponse("test-access-token")
            : request.RequestUri!.AbsolutePath == "/activitylist-service/activities/search/activities"
                ? JsonResponse("[{\"activityId\":1,\"activityName\":\"Yoga\",\"startTimeLocal\":\"2024-08-10 06:07:08\"}]")
                : JsonResponse("{}"));
        using var client = new HttpClient(handler);
        var reader = CreateReader(client);

        var activity = Assert.Single(await reader.GetFatBurningActivitiesAsync(DateTimeOffset.UtcNow.AddDays(-1)));

        Assert.Equal(0m, activity.Distance);
    }

    [Fact]
    public async Task RejectsEmptyTokenResponse()
    {
        var handler = new RecordingHandler(_ => TextResponse("  "));
        using var client = new HttpClient(handler);
        var reader = CreateReader(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetFatBurningActivitiesAsync(DateTimeOffset.UtcNow.AddDays(-1)));

        Assert.Contains("empty access token", exception.Message);
    }

    [Fact]
    public async Task ThrowsForUnsuccessfulActivitySearchResponse()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Post
            ? TextResponse("test-access-token")
            : new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var client = new HttpClient(handler);
        var reader = CreateReader(client);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => reader.GetFatBurningActivitiesAsync(DateTimeOffset.UtcNow.AddDays(-1)));
    }

    private static GarminActivitiesReader CreateReader(HttpClient client) =>
        new(client, Options.Create(new GarminOptions { ApiKey = "secret-key" }));

    private static string CreateSummaries(int start, int count) => JsonSerializer.Serialize(
        Enumerable.Range(start, count).Select(index => new
        {
            activityId = index + 1,
            activityName = $"Run {index + 1}",
            activityType = new { typeKey = "running" },
            startTimeLocal = "2024-08-10 06:07:08",
            distance = index == 0 ? 5280 : 1000
        }));

    private static string GetQueryValue(Uri uri, string name) => uri.Query
        .TrimStart('?')
        .Split('&')
        .Select(part => part.Split('=', 2))
        .Where(part => part[0] == name)
        .Select(part => Uri.UnescapeDataString(part[1]))
        .Single();

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage TextResponse(string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(text, Encoding.UTF8, "text/plain")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RequestRecord> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RequestRecord(request.RequestUri!, request.Headers.Authorization?.ToString()));
            return Task.FromResult(respond(request));
        }
    }

    private sealed record RequestRecord(Uri Uri, string? Authorization);
}