using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CLI;

public static class Config
{

    private static JObject _values;
    public static List<BlockList> BlockLists { get; }
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
            _values = new JObject();

            using StreamWriter file = File.CreateText(ConfigFile);
            file.Write(JsonConvert.SerializeObject(new
            {
                lines = new Dictionary<string, BlockList>(),
                hosts = File.ReadAllText(HostsPath)
            }));
        }

        // Load values
        _values = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(ConfigFile))!;
        BlockLists = _values["lists"]?.ToObject<List<JObject>>()?.Select(e => e.ToObject<BlockList>()!).ToList() ?? [];
    }

    public static object? Get(string key) => _values[key];
    public static T? Get<T>(string key) where T : class => _values.SelectToken(key)?.ToObject<T>();
    public static void Set(string key, object value) => _values[key] = JToken.FromObject(value);
    public static void Save()
    {
        _values["lists"] = JToken.FromObject(BlockLists);
        File.WriteAllText(ConfigFile, _values.ToString());
    }

}
