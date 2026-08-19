using Microsoft.AspNetCore.SignalR.Client;

namespace CLI;

public record ConfigOption(string FieldName, string Description, Func<IArgument> ArgumentFactory);

public static class ConfigSystem
{

    private static readonly Dictionary<string, ConfigOption> _options = [];
    public static List<string> Keys => [.. _options.Keys];

    public static void Register(string key, ConfigOption configOption)
        => _options.Add(key, configOption);

    public static Task<object?> Get(string key) 
        => ConnectionManager.Connection!.InvokeAsync<object?>("GetConfig", _options[key]);

    public static Task Set(string key, object? value)
        => ConnectionManager.Connection!.InvokeAsync("SetConfig", _options[key].FieldName, value);

    public static IArgument GetArgument(string key)
        => _options[key].ArgumentFactory();

}