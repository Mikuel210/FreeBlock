namespace Core;

public class ConfigSchema
{
    public string version = string.Empty;
    public string hosts = string.Empty;

    // Schedule options
    public TimeSpan defaultWarningTime = TimeSpan.FromMinutes(15);

    // Status options
    public bool showAllSchedules = false;
    public bool showAllLists = false;
    public bool showWarningPeriodSchedules = true;
}
