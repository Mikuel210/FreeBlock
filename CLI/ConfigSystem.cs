using Microsoft.AspNetCore.SignalR.Client;
using static CLI.InvokeHelpers;
using Core;
using Daemon;
using System.Text.Json;

namespace CLI;

public record ConfigOption(string FieldName, string Description, Func<IArgument> ArgumentFactory);

public static class ConfigSystem
{

    private static readonly Dictionary<string, ConfigOption> _options = [];
    public static Dictionary<string, ConfigOption> Options => _options;

    public static void Register(string key, ConfigOption configOption)
        => _options.Add(key, configOption);

    public static async Task<ConfigSchema> GetAll()
        => (await Invoke<ConfigSchema>(nameof(CommunicationHub.GetConfigAsync))).Unwrap();

    public static async Task<Dictionary<string, object?>> GetDictionary()
    {
        string[] fieldNames = [.. _options.Select(e => e.Value.FieldName)];
        var values = await GetAll();
        var keys = new Dictionary<string, string>();

        foreach (var fieldName in values.GetType().GetFields().Select(e => e.Name))
        {
            var cliKey = _options.Keys.FirstOrDefault(k => _options[k].FieldName == fieldName);
            if (cliKey is null) continue;

            keys[fieldName] = cliKey;
        }

        // Re-key the daemon response from field names to the CLI keys used everywhere else.
        return keys.Keys.Select(e => new KeyValuePair<string, object?>(
            keys[e],
            values.GetType().GetField(e)?.GetValue(values))
        ).ToDictionary();
    }

    public static async Task<T?> Get<T>(string key)
    {
        var options = await GetDictionary();

        try
        {
            return (T?)options[key];
        }
        catch
        {
            new Failure("Key not found in options").Unwrap();
        }

        throw new NotImplementedException();
    }

    public static async Task Set(string key, object? value)
    {
        if (!_options.TryGetValue(key, out var option))
        {
            new Failure("Key not found in options").Unwrap();
            return;
        }

        // Patch only the requested public field, never overwrite a stale config snapshot
        (await Invoke(nameof(CommunicationHub.SetConfigValueAsync), option.FieldName, JsonSerializer.SerializeToElement(value))).Unwrap();
    }

    public static IArgument GetArgument(string key)
        => _options[key].ArgumentFactory();

}
