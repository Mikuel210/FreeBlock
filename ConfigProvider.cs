using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FreeBlock;

public static class ConfigProvider
{

    public static List<BlockList> BlockLists { get; }
    private static string ConfigDirectory
    {
        get
        {
            if (OperatingSystem.IsLinux()) field = ".config/freeblock";
            else if (OperatingSystem.IsMacOS()) field = "Library/Preferences/FreeBlock";
            else if (OperatingSystem.IsWindows()) field = "AppData/Roaming/FreeBlock";
            else throw new PlatformNotSupportedException("Only Linux, macOS and Windows are supported");
            
            return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), field);
        }   
    }
    private static string ConfigFile => Path.Join(ConfigDirectory, "config.json");

    private const string DEFAULT_DATA = "{\"lists\": {}}";
    private static JObject _values;

    static ConfigProvider()
    {
        // Create file if missing
        if (!Path.Exists(ConfigFile)) {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);
            _values = new JObject();
            
            using StreamWriter file = File.CreateText(ConfigFile);
            file.WriteLine(DEFAULT_DATA);
        }
        
        // Load values
        _values = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(ConfigFile))!;
        BlockLists = _values["lists"]!.ToObject<List<JObject>>()!.Select(e => e.ToObject<BlockList>()).ToList()!;
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