using System.Text.Json;
using Daemon;
using Microsoft.AspNetCore.SignalR.Client;

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
        var jsonElement = await ConnectionManager.Connection!.InvokeAsync<JsonElement>("GetConfig", _options[key].FieldName);
        var type = typeof(Config.DefaultValue).GetField(_options[key].FieldName)!.FieldType;

        return jsonElement.Deserialize(type);
    }

    public static async Task<T> Get<T>(string key) 
    {
        var jsonElement = await ConnectionManager.Connection!.InvokeAsync<JsonElement>("GetConfig", _options[key].FieldName);
        return jsonElement.Deserialize<T>()!;
    }

    public static async Task<Dictionary<string, object?>> GetAll()
    {
        string[] fieldNames = [.. _options.Select(e => e.Value.FieldName)];
        var options = await ConnectionManager.Connection!.InvokeAsync<Dictionary<string, object?>>("GetBatchConfig", fieldNames);
        
        Dictionary<string, string> keys = options
            .Select(e => new KeyValuePair<string, string>(e.Key, _options
            .Single(x => x.Value.FieldName == e.Key).Key))
            .ToDictionary();
        
        return options
            .Select(e => new KeyValuePair<string, object?>(keys[e.Key], e.Value))
            .ToDictionary();
    }

    public static Task Set(string key, object? value)
        => ConnectionManager.Connection!.InvokeAsync("SetConfig", _options[key].FieldName, value);

    public static IArgument GetArgument(string key)
        => _options[key].ArgumentFactory();

}