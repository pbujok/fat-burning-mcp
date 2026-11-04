using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace FatBurner.Mcp;

[McpServerToolType]
public class FatBurningActivityTools(FatBurningActivityReader _reader)
{
    [McpServerTool, Description("Reads Fitnes Activity from selected provider")]
    public async Task<string> GetLatWeekActivities()
    {
        var results = await _reader.GetFatBurningActivitiesAsync();
        return JsonSerializer.Serialize(results);
    }

    [McpServerTool, Description("Reads the recorded samples of one activity (heart rate, GPS track as Latitude/Longitude, cadence, speed, power and every other numeric field the device recorded). Rows[i][j] is the value of Columns[j] at Timestamps[i] (UTC). Garmin provider only.")]
    public async Task<string> GetActivityStreams(
        [Description("ActivityId returned by the activity list.")] string activityId,
        [Description("Maximum number of evenly spaced samples to return. TotalSamples in the result is the full count.")] int maxPoints = 500)
    {
        var streams = await _reader.GetActivityStreamsAsync(activityId, maxPoints);
        return streams is null
            ? $"Activity {activityId} has no sample data (for example a manually entered activity, or an upload that is not a FIT file)."
            : JsonSerializer.Serialize(streams);
    }
}