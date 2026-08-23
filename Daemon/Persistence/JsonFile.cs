using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Daemon;

public class JsonFile
{

    private readonly string _path;
    private readonly JObject _values;

    public JsonFile(string path, object defaultValues)
    {
        _path = path;

        if (!Path.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using StreamWriter file = File.CreateText(path);
            file.Write(JsonConvert.SerializeObject(defaultValues));
        }

        _values = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(path))!;

        // Backfill any keys the on-disk file predates (e.g. an older version's config)
        var defaults = JObject.FromObject(defaultValues);
        bool changed = false;

        foreach (var prop in defaults.Properties())
        {
            if (_values.ContainsKey(prop.Name)) continue;
            
            _values[prop.Name] = prop.Value;
            changed = true;
        }

        if (changed) Save();
    }

    public object? Get(string key, Type type) => _values.SelectToken(key)?.ToObject(type);
    public T? Get<T>(string key) where T : class => _values.SelectToken(key)?.ToObject<T>();

    public List<T> GetList<T>(string key)
        => _values[key]?.ToObject<List<JObject>>()?.Select(e => e.ToObject<T>()!).ToList() ?? [];

    public void Set(string key, object value)
        => _values[key] = JToken.FromObject(value);

    public void Save() => File.WriteAllText(_path, _values.ToString());

}
