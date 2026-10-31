using System.Text.Json.Serialization;

namespace Core;

public class ConfigSchema
{
    [JsonInclude]
    public string version = string.Empty;
    [JsonInclude]
    public string hosts = string.Empty;

    // Schedule options
    [JsonInclude]
    public TimeSpan defaultWarningTime = TimeSpan.FromMinutes(15);

    // Status options
    [JsonInclude]
    public bool showAllSchedules = false;
    [JsonInclude]
    public bool showAllLists = false;
    [JsonInclude]
    public bool showWarningPeriodSchedules = true;
}
