using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace FreeBlock;

public static class ConfigProvider
{

    [field: MaybeNull] private static string ConfigPath
    {
        get
        {
            if (field != null) return field;
            
            if (OperatingSystem.IsLinux()) field = ".config/freeblock/config.json";
            else if (OperatingSystem.IsMacOS()) field = "Library/Preferences/FreeBlock/config.json";
            else if (OperatingSystem.IsWindows()) field = "AppData/Roaming/FreeBlock/config.json";
            else throw new PlatformNotSupportedException("Only Linux, macOS and Windows are supported");
            
            field = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), field);
            return field;
        }
    }

    private static Dictionary<string, object> _values;

    static ConfigProvider()
    {
        if (!Path.Exists(ConfigPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            using StreamWriter file = File.CreateText(ConfigPath);
            
            file.WriteLine("{}");
            return;
        }

        _values = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(ConfigPath)) ?? [];
    }

    public static object Get(string key)
    {
        return _values[key];
    }

    public static void Set(string key, object value)
    {
        _values[key] = value;
    }
    
}