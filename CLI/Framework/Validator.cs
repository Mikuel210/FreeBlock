using Core;
using Microsoft.AspNetCore.SignalR.Client;

namespace CLI;

public static class Validator
{
    public static async Task<(bool, object?)> ValidateString(IName argument, string input)
        => (true, input);

    public static async Task<(bool, object?)> ValidateEntries(IName argument, string input)
    {
        var lines = input.Split(' ');
        var lists = await ConnectionManager.Connection!.InvokeAsync<List<List>>("GetListsAsync");

        List<Entry> entries = [];
        List<string> errors = [];

        foreach (var line in lines)
        {
            bool result = line.ToEntry(out var entry, out var error, lists);

            if (!result)
            {
                if (error != null) errors.Add(error);
                continue;
            }

            entries.Add(entry!);
        }

        errors.ForEach(e => ConsoleUtils.Warning(e));
        if (errors.Count > 0) Console.WriteLine();

        return (true, entries);
    }

    public static async Task<(bool, object?)> ValidateIName<T>(IName argument, string input) where T : IName
    {
        var value = await ConnectionManager.Connection!.InvokeAsync<T>($"Get{typeof(T).Name}FromNameAsync", input);

        if (value == null)
        {
            Console.WriteLine($"{typeof(T).Name} not found: {input}");
            return (false, default);
        }

        return (true, value);
    }

    public static async Task<(bool, object?)> ValidateAddIName<T>(IName argument, string input) where T : IName
    {
        // Check if object already exists
        var @object = await ConnectionManager.Connection!.InvokeAsync<T>($"Get{typeof(T).Name}FromNameAsync", input);

        if (@object != null)
        {
            Console.WriteLine($"{typeof(T).Name} already exists: {input}");
            return (false, null);
        }

        // Restrict name
        foreach (var character in input)
        {
            if (char.IsLetterOrDigit(character) || character == '-' || character == '_') continue;

            Console.WriteLine("Name can only contain letters, digits, '-' and '_'");
            return (false, null);
        }

        return (true, input);
    }

    public static async Task<(bool, object?)> ValidateTimeSpan(IName argument, string input) {
        var result = TimeSpan.TryParse(input, out var value);

        if (!result)
        {
            Console.WriteLine($"[{argument.Name}] must be a valid timespan (HH:MM[:SS])");
            return (false, default);
        }

        if (value < TimeSpan.Zero)
        {
            Console.WriteLine($"[{argument.Name}] must be a positive timespan");
            return (false, default);
        }

        return (true, value);
    }

    public static async Task<(bool, object?)> ValidateDays(IName argument, string input)
    {
        if (input.Equals("everyday", StringComparison.InvariantCultureIgnoreCase))
            return (true, Enum.GetValues<DayOfWeek>());

        if (input.Equals("weekdays", StringComparison.InvariantCultureIgnoreCase))
            return (true, (DayOfWeek[])[DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);

        if (input.Equals("weekends", StringComparison.InvariantCultureIgnoreCase))
            return (true, (DayOfWeek[])[DayOfWeek.Saturday, DayOfWeek.Sunday]);

        List<DayOfWeek> days = [];

        foreach (char day in input.ToLowerInvariant())
        {
            if (!"mtwhfsu".Contains(day))
            {
                Console.WriteLine($"[{argument.Name}] must be days of the week: MTWHFSU | weekdays | weekends | everyday");
                return (false, null);
            }

            days.Add(day switch
            {
                'm' => DayOfWeek.Monday,
                't' => DayOfWeek.Tuesday,
                'w' => DayOfWeek.Wednesday,
                'h' => DayOfWeek.Thursday,
                'f' => DayOfWeek.Friday,
                's' => DayOfWeek.Saturday,
                'u' => DayOfWeek.Sunday,
                _ => throw new NotImplementedException(),
            });
        }

        return (true, (DayOfWeek[])[.. days]);
    }

    public static async Task<(bool, object?)> ValidateTimeOnly(IName argument, string input)
    {
        var result = TimeOnly.TryParse(input, out var value);

        if (!result)
        {
            Console.WriteLine($"[{argument.Name}] must be a valid time (HH:MM:SS)");
            return (false, default);
        }

        return (true, value);
    }

    public static async Task<(bool, object?)> ValidateConfigKey(IName argument, string input)
    {
        var keys = ConfigSystem.Options.Keys;
        var keysLower = keys.Select(e => e.ToLowerInvariant());

        if (keysLower.Contains(input.ToLowerInvariant()))
            return (true, keys.Single(e => e.ToLowerInvariant() == input.ToLowerInvariant()));

        Console.WriteLine($"[{argument.Name}] must be a valid configuration key");
        return (false, null);
    }

    public static async Task<(bool, object?)> ValidateConfigValue(string input, ConfigKeyArgument keyArgument)
    {
        var valueArgument = ConfigSystem.GetArgument(keyArgument.Value!);

        if (await valueArgument.Validate(input))
            return (true, valueArgument.Value);

        return (false, null);
    }

}
