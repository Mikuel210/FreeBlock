using Microsoft.AspNetCore.SignalR.Client;
using Core;
using System.Reflection.Metadata.Ecma335;

namespace CLI;

public static class ConfigCommands
{

    public static async Task Get(ConfigKeyArgument keyArgument)
    {
        Console.WriteLine(await ConfigSystem.Get(keyArgument.Value!));
    }

    public static async Task Set(ConfigKeyArgument keyArgument, ConfigValueArgument valueArgument)
    {
        await ConfigSystem.Set(keyArgument.Value!, valueArgument.Value!);
        Console.WriteLine($"Set {keyArgument.Value!}: {valueArgument.Value}");
    }

}