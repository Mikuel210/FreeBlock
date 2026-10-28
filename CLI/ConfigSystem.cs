using Microsoft.AspNetCore.SignalR.Client;
using static CLI.InvokeHelpers;
using Core;
using Daemon;

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

    public static async Task SetAll(ConfigSchema values)
        => (await Invoke(nameof(CommunicationHub.SetConfigAsync), values)).Unwrap();

    public static async Task Set(string key, object? value)
    {
        try
        {
            string schemaKey = _options[key].FieldName;
            var values = await GetAll();
            var field = values.GetType().GetField(schemaKey)!;

            field.SetValue(values, value);
            await SetAll(values);
        }
        catch
        {
            new Failure("Key not found in options").Unwrap();
        }
    }

    public static IArgument GetArgument(string key)
        => _options[key].ArgumentFactory();

}
