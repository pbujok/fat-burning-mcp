namespace FatBurner.Mcp.Cache;

public class ActivityEntity
{
    public string ActivityId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Activity { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TimeOnly TimeOfActivity { get; set; }
    public decimal Distance { get; set; }
    public string SerializedDetails { get; set; } = string.Empty;
}
