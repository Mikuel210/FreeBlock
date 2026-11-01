using Core;
using System.Globalization;
using static CLI.InvokeHelpers;
using Daemon;

namespace CLI;

public static class Validator
{
    public const string DURATION_FORMAT = "HH:MM[:SS] or D.HH:MM[:SS]";

    private static string GetPosixName(IName argument)
        => argument.Name.StartsWith('-') ? $"[{argument.Name}]" : $"<{argument.Name}>";

    public static async Task<(bool, object?)> ValidateString(IName argument, string input)
        => (true, input);

    public static async Task<(bool, object?)> ValidateBool(IName argument, string input)
    {
        string value = input.Trim().ToLowerInvariant();

        if (value is "true" or "1") return (true, true);
        if (value is "false" or "0") return (true, false);

        Console.WriteLine($"{GetPosixName(argument)} must be a boolean (true | false)");
        return (false, false);
    }

    public static async Task<(bool, object?)> ValidateEntries(IName argument, string input)
    {
        var lines = input.Split(' ');
        var lists = (await Invoke<List<List>>(nameof(CommunicationHub.GetListsAsync))).Unwrap();

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

        errors.ForEach(e => Console.WriteLine($"{GetPosixName(argument)}: {e}"));
        return (errors.Count == 0, entries);
    }

    public static async Task<(bool, object?)> ValidateIName<T>(IName argument, string input) where T : IName
    {
        var value = (await Invoke<T?>($"Get{typeof(T).Name}FromNameAsync", input)).Unwrap();

        if (value == null)
        {
            Console.WriteLine($"{GetPosixName(argument)}: {typeof(T).Name} not found: {input}");
            return (false, default);
        }

        return (true, value);
    }

    public static async Task<(bool, object?)> ValidateAddIName<T>(IName argument, string input) where T : IName
    {
        // Check if object already exists
        var @object = (await Invoke<T?>($"Get{typeof(T).Name}FromNameAsync", input)).Unwrap();

        if (@object != null)
        {
            Console.WriteLine($"{GetPosixName(argument)}: {typeof(T).Name} already exists: {input}");
            return (false, null);
        }

        // Restrict name
        foreach (var character in input)
        {
            if (char.IsLetterOrDigit(character) || character == '-' || character == '_') continue;

            Console.WriteLine($"{GetPosixName(argument)}: Name can only contain letters, digits, '-' and '_'");
            return (false, null);
        }

        return (true, input);
    }

    public static async Task<(bool, object?)> ValidateTimeSpan(IName argument, string input)
    {
        var result = TryParseDuration(input, out var value);

        if (!result)
        {
            Console.WriteLine($"{GetPosixName(argument)} must be a valid timespan (DD.HH:MM:SS)");
            return (false, default);
        }

        if (value < TimeSpan.Zero)
        {
            Console.WriteLine($"{GetPosixName(argument)} must be a positive timespan");
            return (false, default);
        }

        return (true, value);
    }

    private static bool TryParseDuration(string input, out TimeSpan value)
    {
        var parts = input.Trim().Split(':');

        // Normalize total hours before parsing so three-part inputs cannot be read as days.
        if (parts.Length is 2 or 3
            && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            && hours >= 24)
        {
            var normalized = FormattableString.Invariant($"{hours / 24}.{hours % 24:00}:{string.Join(':', parts.Skip(1))}");
            return TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out value);
        }

        return TimeSpan.TryParse(input, out value);
    }

    public static async Task<(bool, object?)> ValidateDays(IName argument, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine($"{GetPosixName(argument)} must include at least one day");
            return (false, null);
        }

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
                Console.WriteLine($"{GetPosixName(argument)} must be days of the week: MTWHFSU | weekdays | weekends | everyday");
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

        return (true, (DayOfWeek[])[.. days.Distinct().Order()]);
    }

    public static async Task<(bool, object?)> ValidateTimeOnly(IName argument, string input)
    {
        var result = TimeOnly.TryParse(input, out var value);

        if (!result)
        {
            Console.WriteLine($"{GetPosixName(argument)} must be a valid time (HH:MM:SS)");
            return (false, default);
        }

        return (true, value);
    }

    public static async Task<(bool, object?)> ValidateConfigKey(IName argument, string input)
    {
        var keys = ConfigSystem.Options.Keys;
        var match = keys.FirstOrDefault(e => e.ToLowerInvariant() == input.ToLowerInvariant());

        if (match != null) return (true, match);

        Console.WriteLine($"{GetPosixName(argument)} must be a valid configuration key");
        return (false, null);
    }

    public static async Task<(bool, object?)> ValidateConfigValue(IName argument, string input)
    {
        var keyArgument = (argument as ConfigValueArgument)?.KeyArgument ?? throw new ArgumentException("Argument is not a config value", nameof(argument));

        var valueArgument = ConfigSystem.GetArgument(keyArgument.Value!);
        var valid = await valueArgument.Validate(input);

        return valid ? (true, valueArgument.Value) : (false, null);
    }

}
