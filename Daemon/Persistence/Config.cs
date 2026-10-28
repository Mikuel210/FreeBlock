using Core;

namespace Daemon;

public static class Config
{

    private static JsonFile? _file;
    private static JsonFile File => _file ??= new JsonFile(Platform.ConfigFile, new ConfigSchema());

    public static void Initialize()
    {
        Set(nameof(ConfigSchema.version), Globals.VERSION);

        if (string.IsNullOrEmpty(Get<string>(nameof(ConfigSchema.hosts))))
        {
            try
            {
                Set(nameof(ConfigSchema.hosts), System.IO.File.ReadAllText(Platform.HostsPath));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error when reading hosts file: {e}");
                Set(nameof(ConfigSchema.hosts), string.Empty);
            }
        }

        Save();
    }

    public static T? Get<T>(string key) where T : class => File.Get<T>(key);
    public static T GetStruct<T>(string key) where T : struct => File.GetStruct<T>(key);
    public static void Set(string key, object value) => File.Set(key, value);

    public static ConfigSchema GetAll()
    {
        return new ConfigSchema
        {
            version = Get<string>(nameof(ConfigSchema.version))!,
            hosts = Get<string>(nameof(ConfigSchema.hosts))!,
            defaultWarningTime = GetStruct<TimeSpan>(nameof(ConfigSchema.defaultWarningTime))!,
            showAllSchedules = GetStruct<bool>(nameof(ConfigSchema.showAllSchedules))!,
            showAllLists = GetStruct<bool>(nameof(ConfigSchema.showAllLists))!,
            showWarningPeriodSchedules = GetStruct<bool>(nameof(ConfigSchema.showWarningPeriodSchedules))!,
        };
    }

    public static void SetAll(ConfigSchema values)
    {
        foreach (var field in values.GetType().GetFields())
            Set(field.Name, field.GetValue(values)!);
    }

    public static void Save() => File.Save();

}
