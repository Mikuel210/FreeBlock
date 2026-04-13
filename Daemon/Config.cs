using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SDK;

namespace Daemon;

public static class Config
{

    public static List<BlockList> BlockLists { get; }
    public static List<Schedule> Schedules { get; }

    private static readonly JObject _values = [];
    public static string ConfigDirectory { get; }
    public static string ConfigFile { get; }
    public static string HostsPath { get; }

    static Config()
    {
        // Set hosts path
        if (OperatingSystem.IsLinux()) HostsPath = "/etc/hosts";
        else if (OperatingSystem.IsMacOS()) HostsPath = "/private/etc/hosts";
        else if (OperatingSystem.IsWindows()) HostsPath = "C:/Windows/System32/drivers/etc/hosts";
        else throw new PlatformNotSupportedException("Only Linux, macOS and Windows are supported");

        // Set config paths
        if (OperatingSystem.IsLinux()) ConfigDirectory = ".config/freeblock";
        else if (OperatingSystem.IsMacOS()) ConfigDirectory = "Library/Preferences/FreeBlock";
        else if (OperatingSystem.IsWindows()) ConfigDirectory = "AppData/Roaming/FreeBlock";
        else throw new PlatformNotSupportedException("Only Linux, macOS and Windows are supported");

        ConfigDirectory = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ConfigDirectory);
        ConfigFile = Path.Join(ConfigDirectory, "config.json");

        // Create file if missing
        if (!Path.Exists(ConfigFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);
            using StreamWriter file = File.CreateText(ConfigFile);

            file.Write(JsonConvert.SerializeObject(new
            {
                hosts = File.ReadAllText(HostsPath),
                lists = new List<BlockList>(),
                schedules = new List<Schedule>()
            }));
        }

        // Load values
        _values = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(ConfigFile))!;
        BlockLists = GetList<BlockList>("lists");
        Schedules = GetList<Schedule>("schedules");
    }

    public static object? Get(string key) => _values[key];
    public static T? Get<T>(string key) where T : class => _values.SelectToken(key)?.ToObject<T>();
    public static void Set(string key, object value) => _values[key] = JToken.FromObject(value);

    public static void Save()
    {
        _values["lists"] = JToken.FromObject(BlockLists);
        _values["schedules"] = JToken.FromObject(Schedules);
        File.WriteAllText(ConfigFile, _values.ToString());
    }

    private static List<T> GetList<T>(string key)
        => _values[key]?.ToObject<List<JObject>>()?.Select(e => e.ToObject<T>()!).ToList() ?? [];

}
