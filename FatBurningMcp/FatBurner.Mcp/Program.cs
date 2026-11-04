using FatBurner.Abstractions;
using FatBurner.Mcp;
using FatBurner.Mcp.Cache;
using FatBurner.Mcp.Garmin;
using FatBurner.Strava;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;

var builder = WebApplication.CreateBuilder(args);

var activityProvider = builder.Configuration["ActivityProvider"]?.Trim();
if (string.IsNullOrEmpty(activityProvider))
    activityProvider = "Strava";

switch (activityProvider.ToLowerInvariant())
{
    case "strava":
        builder.Services.Configure<StravaOptions>(builder.Configuration.GetSection("Strava"));
        builder.Services.AddHttpClient<IFatBurningActivityReader, StravaActivitiesReader>();
        break;
    case "garmin":
        builder.Services.Configure<GarminOptions>(builder.Configuration.GetSection("Garmin"));
        builder.Services.AddHttpClient<IFatBurningActivityReader, GarminActivitiesReader>();
        break;
    default:
        throw new InvalidOperationException($"Unsupported activity provider '{activityProvider}'. Supported providers are Strava and Garmin.");
}

builder.Services.AddTransient<FatBurningActivityReader>();

var cacheConnectionString = activityProvider.Equals("Garmin", StringComparison.OrdinalIgnoreCase)
    ? builder.Configuration.GetConnectionString("GarminActivityCache") ?? "Data Source=garmin-activity.db"
    : builder.Configuration.GetConnectionString("ActivityCache") ?? "Data Source=fat-burning.db";
builder.Services.AddDbContext<ActivityCacheDbContext>(opt =>
    opt.UseSqlite(cacheConnectionString));
builder.Services.AddScoped<IActivityCache, SqliteActivityCache>();

builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "Fat-Burning-Mcp-Bridge",
            Version = "1.0.0",
            Description = "Bridge between Workout Activity tracking apps and OpenClaw agents."
        };
    })
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<ActivityCacheDbContext>().EnsureSchema();

app.MapMcp("/mcp");

app.MapGet("/items", async (FatBurningActivityReader reader) =>
{
    var activities = await reader.GetFatBurningActivitiesAsync();
    return Results.Ok(activities);
});

app.MapGet("/items/{activityId:long}/streams", async (long activityId, int? maxPoints, FatBurningActivityReader reader) =>
{
    var streams = await reader.GetActivityStreamsAsync(activityId.ToString(), maxPoints);
    return streams is null ? Results.NotFound() : Results.Ok(streams);
});

app.Run();
