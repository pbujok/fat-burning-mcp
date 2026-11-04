using FatBurner.Abstractions;
using FatBurner.Mcp;
using FatBurner.Mcp.Cache;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FatBurner.Garmin.Tests;

public sealed class ActivityStreamsCacheTests : IDisposable
{
    private static readonly DateTime Start = new(2024, 8, 10, 6, 7, 8, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ActivityCacheDbContext _context;
    private readonly FakeProvider _provider = new();
    private readonly FatBurningActivityReader _reader;

    public ActivityStreamsCacheTests()
    {
        _connection.Open();
        _context = new ActivityCacheDbContext(new DbContextOptionsBuilder<ActivityCacheDbContext>().UseSqlite(_connection).Options);
        _context.EnsureSchema();
        _reader = new FatBurningActivityReader(_provider, new SqliteActivityCache(_context));
    }

    [Fact]
    public async Task EnsureSchemaAddsStreamsTableToExistingDatabaseAndKeepsActivities()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        await using (var create = connection.CreateCommand())
        {
            // The schema of a cache created before ActivityStreams existed.
            create.CommandText =
                """
                CREATE TABLE "Activities" ("ActivityId" TEXT NOT NULL PRIMARY KEY, "Title" TEXT NOT NULL, "Activity" TEXT NOT NULL,
                    "Date" TEXT NOT NULL, "TimeOfActivity" TEXT NOT NULL, "Distance" TEXT NOT NULL, "SerializedDetails" TEXT NOT NULL);
                INSERT INTO "Activities" VALUES ('1', 'Run', 'running', '2024-08-10 00:00:00', '06:07:08', '5.0', '{}');
                """;
            await create.ExecuteNonQueryAsync();
        }

        await using var context = new ActivityCacheDbContext(new DbContextOptionsBuilder<ActivityCacheDbContext>().UseSqlite(connection).Options);
        context.EnsureSchema();
        context.EnsureSchema();

        var cache = new SqliteActivityCache(context);
        await cache.CacheStreamsAsync(CreateStreams("1", 1));

        Assert.NotNull(await cache.GetCachedStreamsAsync("1"));
        Assert.Equal(1, await context.Activities.CountAsync());
    }

    [Fact]
    public async Task ReadsFromProviderOnceThenReturnsIdenticalDataFromCache()
    {
        _provider.Streams = CreateStreams("1", 3);

        var first = await _reader.GetActivityStreamsAsync("1");
        var second = await _reader.GetActivityStreamsAsync("1");

        Assert.Equal(1, _provider.Calls);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Columns, second.Columns);
        Assert.Equal(first.Timestamps, second.Timestamps);
        Assert.Equal(DateTimeKind.Utc, second.Timestamps[0].Kind);
        Assert.Equal(first.Rows, second.Rows);
        Assert.Null(second.Rows[2][1]);
    }

    [Fact]
    public async Task DoesNotCacheMissingStreams()
    {
        Assert.Null(await _reader.GetActivityStreamsAsync("1"));

        _provider.Streams = CreateStreams("1", 1);

        Assert.NotNull(await _reader.GetActivityStreamsAsync("1"));
        Assert.Equal(2, _provider.Calls);
    }

    [Fact]
    public async Task DownsamplingDoesNotShrinkTheCachedData()
    {
        _provider.Streams = CreateStreams("1", 100);

        var reduced = await _reader.GetActivityStreamsAsync("1", maxPoints: 10);
        var full = await _reader.GetActivityStreamsAsync("1");

        Assert.Equal(10, reduced!.Rows.Count);
        Assert.Equal(100, reduced.TotalSamples);
        Assert.Equal(100, full!.Rows.Count);
    }

    [Theory]
    [InlineData(100, 10, 10)]
    [InlineData(100, 100, 100)]
    [InlineData(5, 500, 5)]
    [InlineData(100, 0, 2)]
    [InlineData(100, -3, 2)]
    public void DownsampleKeepsFirstAndLastAndTimestampsAligned(int samples, int maxPoints, int expected)
    {
        var result = CreateStreams("1", samples).Downsample(maxPoints);

        Assert.Equal(expected, result.Rows.Count);
        Assert.Equal(expected, result.Timestamps.Count);
        Assert.Equal(samples, result.TotalSamples);
        Assert.Equal(0d, result.Rows[0][0]);
        Assert.Equal(samples - 1d, result.Rows[^1][0]);
        Assert.All(Enumerable.Range(0, expected), i =>
            Assert.Equal(Start.AddSeconds(result.Rows[i][0]!.Value), result.Timestamps[i]));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static ActivityStreams CreateStreams(string id, int samples) => new(
        id,
        samples,
        ["HeartRate", "Cadence"],
        Enumerable.Range(0, samples).Select(i => Start.AddSeconds(i)).ToArray(),
        Enumerable.Range(0, samples).Select(i => new double?[] { i, null }).ToArray());

    private sealed class FakeProvider : IFatBurningActivityReader
    {
        public ActivityStreams? Streams { get; set; }
        public int Calls { get; private set; }

        public Task<IReadOnlyCollection<FatBurningActivity>> GetFatBurningActivitiesAsync(DateTimeOffset dateAfter) =>
            Task.FromResult<IReadOnlyCollection<FatBurningActivity>>([]);

        public Task<ActivityStreams?> GetActivityStreamsAsync(string activityId)
        {
            Calls++;
            return Task.FromResult(Streams);
        }
    }
}
