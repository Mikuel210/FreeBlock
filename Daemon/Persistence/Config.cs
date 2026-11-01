using Core;
using System.Text.Json;

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
        ArgumentNullException.ThrowIfNull(values);
        ValidateWarningTime(values.defaultWarningTime);

        // Internal backup/version fields are not client-writable
        if (values.hosts != Get<string>(nameof(ConfigSchema.hosts)) ||
            values.version != Get<string>(nameof(ConfigSchema.version)))
        {
            throw new ArgumentException("Internal configuration fields cannot be changed.");
        }

        Set(nameof(ConfigSchema.defaultWarningTime), values.defaultWarningTime);
        Set(nameof(ConfigSchema.showAllSchedules), values.showAllSchedules);
        Set(nameof(ConfigSchema.showAllLists), values.showAllLists);
        Set(nameof(ConfigSchema.showWarningPeriodSchedules), values.showWarningPeriodSchedules);
    }

    public static void ValidateWarningTime(TimeSpan warning)
    {
        if (warning < TimeSpan.Zero || warning > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(warning), "Warning time must be between zero and 24 hours.");
    }

    public static void SetPublicValue(string name, JsonElement value)
    {
        // Decode the public field
        object typed = name switch
        {
            nameof(ConfigSchema.defaultWarningTime)
                => value.Deserialize<TimeSpan>(),
            nameof(ConfigSchema.showAllSchedules) or nameof(ConfigSchema.showAllLists) or nameof(ConfigSchema.showWarningPeriodSchedules)
                => value.GetBoolean(),
            _ => throw new ArgumentException("Unknown or internal configuration field.")
        };

        // Validate the value
        if (typed is TimeSpan warning)
        {
            ValidateWarningTime(warning);
        }

        // Save the change
        Set(name, typed);
        Save();
    }

    public static void Save() => File.Save();

}
