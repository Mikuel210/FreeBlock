namespace Core;

/// <summary>
/// The schema of the daemon's config file: one field per option, plus the description shown by the CLI.
/// Lives in Core because both the daemon (defaults, RPC) and the CLI (registry, validation) need the same
/// definition — previously the CLI reflected over a Daemon static, which pulled the whole ASP.NET Core
/// web stack into the client binary (D5).
/// </summary>
public static class ConfigOptions
{

    public record Option(string FieldName, string Description);

    public static readonly Dictionary<string, Option> All = new()
    {
        ["schedule.defaultWarningTime"] = new(nameof(DefaultValue.defaultWarningTime), "The default warning time for new schedules"),
        ["status.showAllLists"] = new(nameof(DefaultValue.showAllLists), "Whether to show all lists in status or just active ones"),
        ["status.showAllSchedules"] = new(nameof(DefaultValue.showAllSchedules), "Whether to show all schedules in status or just active ones"),
        ["status.showWarningPeriodSchedules"] = new(nameof(DefaultValue.showWarningPeriodSchedules), "Whether to show schedules in warning period, in case status.showAllSchedules is false")
    };

    /// <summary>Field names as the daemon's config file stores them.</summary>
    public static readonly Dictionary<string, string> FieldNames = All.ToDictionary(e => e.Key, e => e.Value.FieldName);

    /// <summary>The type of a field, looked up by the config key the CLI exposes.</summary>
    public static Type GetFieldType(string key)
        => DefaultValue.Instance.GetType().GetField(All[key].FieldName)!.FieldType;

    /// <summary>The config key (e.g. status.showAllLists) for a daemon field name, if known.</summary>
    public static string? GetKey(string fieldName)
        => All.FirstOrDefault(e => e.Value.FieldName == fieldName).Key;

    /// <summary>
    /// The default values. Mirrors <c>Daemon.Config.DefaultValue</c> field-for-field so the CLI can
    /// validate config values without a daemon reference. Kept in sync by ConfigTests.
    /// </summary>
    public class DefaultValue
    {
        public static readonly DefaultValue Instance = new();

        public string version = string.Empty;
        public string hosts = string.Empty;

        // Schedule options
        public TimeSpan defaultWarningTime = TimeSpan.FromMinutes(15);

        // Status options
        public bool showAllSchedules = false;
        public bool showAllLists = false;
        public bool showWarningPeriodSchedules = true;
    }

}
