namespace CLI;

public static class ConfigCommands
{

    public static async Task<int> ShowStatus()
    {
        var options = await ConfigSystem.GetAll();
        int spaces = options.Select(e => e.Key).Max(e => e.Length) + 3;

        foreach (var option in options)
        {
            var spaceString = new string(' ', spaces - option.Key.Length);
            Console.WriteLine($"{option.Key}{spaceString}{option.Value}");
        }

        return 0;
    }

    public static async Task<int> Get(ConfigKeyArgument keyArgument)
    {
        Console.WriteLine(await ConfigSystem.Get(keyArgument.Value!));
        return 0;
    }

    public static async Task<int> Set(ConfigKeyArgument keyArgument, ConfigValueArgument valueArgument)
    {
        await ConfigSystem.Set(keyArgument.Value!, valueArgument.Value!);
        Console.WriteLine($"Set {keyArgument.Value!}: {valueArgument.Value}");

        return 0;
    }

}
