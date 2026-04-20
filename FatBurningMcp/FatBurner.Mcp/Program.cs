using FatBurner.Abstractions;
using FatBurner.Strava;
using ModelContextProtocol.Protocol;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StravaOptions>(builder.Configuration.GetSection("Strava"));
builder.Services.AddHttpClient<IFatBurningActivityReader, StravaActivitiesReader>();

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
    .WithToolsFromAssembly(); // Discovers the HealthTools class automatically

var app = builder.Build();

app.MapMcp("/mcp");

app.MapGet("/items", async (IFatBurningActivityReader reader) =>
{
    var activities = await reader.GetFatBurningActivitiesAsync();
    return Results.Ok(activities);
});

app.Run();