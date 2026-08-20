using System.Data;

namespace Daemon;

public static class Config
{

    public struct DefaultValue()
    {
        public string hosts = string.Empty;

        // Schedule options
        public TimeSpan defaultWarningTime = TimeSpan.FromMinutes(15);

        // Status options
        public bool showAllSchedules = false;
        public bool showAllLists = false;
        public bool showWarningPeriodSchedules = true;
    }

    private static readonly JsonFile _file = new(Platform.ConfigFile, new DefaultValue());

    public static void Initialize()
    {
        if (!string.IsNullOrEmpty(Get<string>(nameof(DefaultValue.hosts)))) return;
        
        try 
        {
            Set(nameof(DefaultValue.hosts), File.ReadAllText(Platform.HostsPath));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error when reading hosts file: {e}");
            Set(nameof(DefaultValue.hosts), string.Empty);
        }
        
        Save();
    }

    public static object? Get(string key, Type type) => _file.Get(key, type);
    public static T? Get<T>(string key) where T : class => _file.Get<T>(key);

    public static void Set(string key, object value) => _file.Set(key, value);
    public static void Save() => _file.Save();

}
