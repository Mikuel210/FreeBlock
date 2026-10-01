using System.Globalization;
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
        var directory = Path.GetDirectoryName(path);

        if (!Path.Exists(path))
        {
            if (directory != null) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonConvert.SerializeObject(defaultValues));
        }

        JObject? values;

        try
        {
            values = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(path));
        }
        catch (JsonException e)
        {
            // A corrupt file used to NRE the static constructor of State/Config, which makes the type
            // permanently unusable in that process — the daemon would then fail to start forever (B2).
            // Back the corrupt file up and fall back to defaults instead of dying.
            string date = DateTime.Now.ToString("dd-MM-yyyy-HH.mm.ss", CultureInfo.InvariantCulture);
            var backupPath = $"{_path}.{date}.corrupt";

            try { File.Move(_path, backupPath); }
            catch { /* keep going: losing the file beats losing the daemon */ }

            Console.Error.WriteLine($"Corrupt JSON in {_path} backed up to {backupPath}: {e.Message}");
            values = [];
        }

        _values = values ?? [];

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

    /// <summary>
    /// Writes to a temp file in the same directory and moves it over the target, so a reader never
    /// sees a half-written file. Same reasoning as the hosts-file write (P3).
    /// </summary>
    public void Save()
    {
        var directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException($"Cannot resolve directory of {_path}");
        var tempPath = Path.Join(directory, $".freeblock-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, _values.ToString());
            File.Move(tempPath, _path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }

}
