namespace CLI;

public static class ConfigCommands
{

    public static async Task<int> ShowStatus()
    {
        var options = await ConfigSystem.GetDictionary();
        ConsoleUtils.PrintDictionary(options);

        return 0;
    }

    public static async Task<int> Get(ConfigKeyArgument keyArgument)
    {
        Console.WriteLine(await ConfigSystem.Get<object?>(keyArgument.Value!));
        return 0;
    }

    public static async Task<int> Set(ConfigKeyArgument keyArgument, ConfigValueArgument valueArgument)
    {
        await ConfigSystem.Set(keyArgument.Value!, valueArgument.Value!);
        Console.WriteLine($"Set {keyArgument.Value!}: {valueArgument.Value}");

        return 0;
    }

}
