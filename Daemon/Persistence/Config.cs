namespace Daemon;

public static class Config
{

    public struct DefaultValue()
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

    private static JsonFile? _file;
    private static JsonFile File => _file ??= new JsonFile(Platform.ConfigFile, new DefaultValue());

    public static void Initialize()
    {
        Set(nameof(DefaultValue.version), Core.Globals.VERSION);

        if (string.IsNullOrEmpty(Get<string>(nameof(DefaultValue.hosts))))
        {
            try
            {
                Set(nameof(DefaultValue.hosts), System.IO.File.ReadAllText(Platform.HostsPath));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error when reading hosts file: {e}");
                Set(nameof(DefaultValue.hosts), string.Empty);
            }
        }

        Save();
    }

    public static object? Get(string key, Type type) => File.Get(key, type);
    public static T? Get<T>(string key) where T : class => File.Get<T>(key);

    /// <summary>For tests: point Config at a file that is not the installed one.</summary>
    public static void Initialize(JsonFile file) => _file = file;

    public static void Set(string key, object value) => File.Set(key, value);
    public static void Save() => File.Save();

}
