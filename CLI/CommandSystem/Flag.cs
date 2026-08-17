using Microsoft.AspNetCore.SignalR.Client;
using Core;

namespace CLI;

public interface IFlag
{
    public string LongName { get; }
    public string? ShortName { get; }
    public string Description { get; }
    public string? ValueName { get; }

    public bool IsSwitch { get; }
    public bool Params { get; }
    public object? Value { get; }

    public Task<bool> Validate(string input);
}

public abstract record SwitchFlag(string Description, string LongName, string? ShortName = null) : IFlag
{
    public string? ValueName => null;
    public bool IsSwitch => true;
    public bool Params => false;
    public object? Value => null;

    public async Task<bool> Validate(string input) => true;
}

public abstract record ValueFlag<T>(string Description, bool Params, string LongName, string? ShortName = null, string? ValueName = null) : IFlag
{
    public bool IsSwitch => false;
    public T? Value { get; protected set; }
    object? IFlag.Value => Value;

    public abstract Task<bool> Validate(string input);
}

#region Flags

// Generic flags

public record EntriesFlag(string Description, string LongName, string? ShortName = null) 
    : ValueFlag<List<Entry>>(Description, true, LongName, ShortName, "entries")
{
    public override async Task<bool> Validate(string input)
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

        Value = entries;
        return true;
    }
}

public record TimeSpanFlag(string Description, string LongName, string? ShortName = null) 
    : ValueFlag<TimeSpan>(Description, false, LongName, ShortName, "timespan")
{
    public override async Task<bool> Validate(string input)
    {
        var result = TimeSpan.TryParse(input, out var value);

        if (!result)
        {
            Console.WriteLine($"[{LongName}] must be a valid timespan (HH:MM[:SS])");
            return false;
        }

        Value = value;
        return true;
    }
}

public record TimeFlag(string Description, string LongName, string? ShortName = null) 
    : ValueFlag<TimeOnly>(Description, false, LongName, ShortName, "time")
{
    public override async Task<bool> Validate(string input)
    {
        var result = TimeOnly.TryParse(input, out var value);

        if (!result)
        {
            Console.WriteLine($"[{LongName}] must be a valid time (HH:MM:SS)");
            return false;
        }

        Value = value;
        return true;
    }
}

public record DaysFlag(string Description, string LongName, string? ShortName = null) 
    : ValueFlag<DayOfWeek[]>(Description, false, LongName, ShortName, "days")
{
    public override async Task<bool> Validate(string input)
    {
        if (input.Equals("everyday", StringComparison.InvariantCultureIgnoreCase))
        {
            Value = Enum.GetValues<DayOfWeek>();
            return true;
        }

        if (input.Equals("weekdays", StringComparison.InvariantCultureIgnoreCase))
        {
            Value = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
            return true;
        }

        if (input.Equals("weekends", StringComparison.InvariantCultureIgnoreCase))
        {
            Value = [DayOfWeek.Saturday, DayOfWeek.Sunday];
            return true;
        }

        List<DayOfWeek> days = [];

        foreach (char day in input.ToLowerInvariant())
        {
            if (!"mtwhfsu".Contains(day))
            {
                Console.WriteLine($"[{LongName}] must be days of the week: MTWHFSU | weekdays | weekends | everyday");
                return false;
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

        Value = [.. days];
        return true;
    }
}

// Command flags

public record VersionFlag() : SwitchFlag("Show the FreeBlock version", "--version", "-v");

public record UninstallFlag() : SwitchFlag("Uninstall FreeBlock", "--uninstall");

// Locks
public record LockAddEntriesFlag() : EntriesFlag("Add entries to the lock", "--add-entries", "-a");

public record LockExtendFlag() : TimeSpanFlag("Extend the lock by the provided amount of time (HH:MM[:SS])", "--extend", "-e");

// Schedules
public record ScheduleStartTimeFlag() : TimeFlag("Change the start time of the schedule (HH:MM[:SS])", "--start-time", "-s");

public record ScheduleEndTimeFlag() : TimeFlag("Change the end time of the schedule (HH:MM[:SS])", "--end-time", "-x");

public record ScheduleDaysFlag() : DaysFlag("Change the days of the week to apply the schedule (MTWHFSU)", "--days", "-d");

public record ScheduleEntriesFlag() : EntriesFlag("Change the entries of the schedule", "--entries", "-e");

#endregion