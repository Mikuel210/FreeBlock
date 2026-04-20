using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SDK;

namespace Daemon;

public static class Config
{

    private static readonly JObject _values = [];
    public static List<BlockList> BlockLists { get; }
    public static List<Schedule> Schedules { get; }
    public static IPlatform Platform { get; }

    static Config()
    {
        // Set platform
        if (OperatingSystem.IsLinux()) Platform = new Linux();
        else if (OperatingSystem.IsMacOS()) Platform = new MacOS();
        else if (OperatingSystem.IsWindows()) Platform = new Windows();
        else throw new PlatformNotSupportedException("Only Linux, macOS and Windows are supported");

        // Create file if missing
        if (!Path.Exists(Platform.ConfigFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Platform.ConfigFile)!);
            using StreamWriter file = File.CreateText(Platform.ConfigFile);

            file.Write(JsonConvert.SerializeObject(new
            {
                hosts = File.ReadAllText(Platform.HostsPath),
                lists = new List<BlockList>(),
                schedules = new List<Schedule>()
            }));
        }

        // Load values
        _values = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(Platform.ConfigFile))!;
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
        File.WriteAllText(Platform.ConfigFile, _values.ToString());
    }

    private static List<T> GetList<T>(string key)
        => _values[key]?.ToObject<List<JObject>>()?.Select(e => e.ToObject<T>()!).ToList() ?? [];

}
