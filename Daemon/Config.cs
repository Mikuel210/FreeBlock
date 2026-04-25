namespace Daemon;

public static class Config
{

    public struct DefaultValue()
    {
        public string hosts = string.Empty;
    }

    private static readonly JsonFile _file = new(Platform.ConfigFile, new DefaultValue());

    public static void Initialize()
    {
        if (!string.IsNullOrEmpty(Get<string>(nameof(DefaultValue.hosts)))) return;
        Set(nameof(DefaultValue.hosts), File.ReadAllText(Platform.HostsPath));
    }

    public static T? Get<T>(string key) where T : class => _file.Get<T>(key);
    public static void Set(string key, object value) => _file.Set(key, value);

    public static void Save() => _file.Save();

}
