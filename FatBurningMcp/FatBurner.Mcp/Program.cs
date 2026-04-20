using FatBurner.Abstractions;
using FatBurner.Mcp;
using FatBurner.Mcp.Cache;
using FatBurner.Strava;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StravaOptions>(builder.Configuration.GetSection("Strava"));
builder.Services.AddHttpClient<IFatBurningActivityReader, StravaActivitiesReader>();
builder.Services.AddTransient<FatBurningActivityReader>();

builder.Services.AddDbContext<ActivityCacheDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("ActivityCache") ?? "Data Source=fat-burning.db"));
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
    scope.ServiceProvider.GetRequiredService<ActivityCacheDbContext>().Database.EnsureCreated();

app.MapMcp("/mcp");

app.MapGet("/items", async (FatBurningActivityReader reader) =>
{
    var activities = await reader.GetFatBurningActivitiesAsync();
    return Results.Ok(activities);
});

app.Run();
