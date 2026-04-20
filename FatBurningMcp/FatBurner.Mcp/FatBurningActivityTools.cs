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
}