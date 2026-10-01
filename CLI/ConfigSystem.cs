using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Core;

namespace CLI;

public record ConfigOption(string FieldName, string Description, Func<IArgument> ArgumentFactory);

public static class ConfigSystem
{

    private static readonly Dictionary<string, ConfigOption> _options = [];
    public static Dictionary<string, ConfigOption> Options => _options;

    public static void Register(string key, ConfigOption configOption)
        => _options.Add(key, configOption);

    public static async Task<object?> Get(string key)
    {
        try
        {
            var jsonElement = await ConnectionManager.Connection!.InvokeAsync<JsonElement>("GetConfig", _options[key].FieldName);
            var type = ConfigOptions.GetFieldType(key);

            return jsonElement.Deserialize(type);
        }
        catch
        {
            return default;
        }
    }

    public static async Task<T?> Get<T>(string key)
    {
        try
        {
            var jsonElement = await ConnectionManager.Connection!.InvokeAsync<JsonElement>("GetConfig", _options[key].FieldName);
            return jsonElement.Deserialize<T>();
        }
        catch
        {
            return default;
        }
    }

    public static async Task<Dictionary<string, object?>> GetAll()
    {
        string[] fieldNames = [.. _options.Select(e => e.Value.FieldName)];
        var options = await ConnectionManager.Connection!.InvokeAsync<Dictionary<string, object?>>("GetBatchConfig", fieldNames);

        // FirstOrDefault + skip: the daemon may report a field this CLI registry does not know about
        // (a downgrade), which must not take down the whole command.
        var keys = new Dictionary<string, string>();

        foreach (var pair in options)
        {
            var cliKey = _options.Keys.FirstOrDefault(k => _options[k].FieldName == pair.Key);
            if (cliKey is null) continue;

            keys[pair.Key] = cliKey;
        }

        // Re-key the daemon response from field names to the CLI keys used everywhere else.
        return options
            .Where(e => keys.ContainsKey(e.Key))
            .Select(e => new KeyValuePair<string, object?>(keys[e.Key], e.Value))
            .ToDictionary();
    }

    public static Task Set(string key, object? value)
        => ConnectionManager.Connection!.InvokeAsync("SetConfig", _options[key].FieldName, value);

    public static IArgument GetArgument(string key)
        => _options[key].ArgumentFactory();

}
