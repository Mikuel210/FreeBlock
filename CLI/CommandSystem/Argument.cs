using Microsoft.AspNetCore.SignalR.Client;
using Core;
using Lock = Core.Lock;

namespace CLI;

public interface IArgument
{
    public string Name { get; }
    public string Description { get; }

    public object? Value { get; }
    public bool Params { get; }

    public Task<bool> Validate(string input);
}

public abstract record Argument<T>(string Name, string Description, bool Params = false) : IArgument
{
    public T? Value { get; protected set; }
    object? IArgument.Value => Value;

    public abstract Task<bool> Validate(string input);
}

#region Arguments

public record StringArgument(string Name, string Description) : Argument<string>(Name, Description)
{
    public override async Task<bool> Validate(string input)
    {
        Value = input;
        return true;
    }
}

public record EntriesArgument(string Name, string Description) : Argument<List<Entry>>(Name, Description, true)
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

public record INameArgument<T>(string Name, string Description) : Argument<T>(Name, Description) where T : IName
{
    public override async Task<bool> Validate(string input)
    {
        Value = await ConnectionManager.Connection!.InvokeAsync<T>($"Get{typeof(T).Name}FromNameAsync", input);

        if (Value == null)
        {
            Console.WriteLine($"{typeof(T).Name} not found: {input}");
            return false;
        }

        return true;
    }
}

public record AddINameArgument<T>(string Name, string Description) : Argument<string>(Name, Description) where T : IName
{
    public override async Task<bool> Validate(string input)
    {
        // Check if object already exists
        var @object = await ConnectionManager.Connection!.InvokeAsync<T>($"Get{typeof(T).Name}FromNameAsync", input);

        if (@object != null)
        {
            Console.WriteLine($"{typeof(T).Name} already exists: {input}");
            return false;
        }

        // Restrict name
        foreach (var character in input)
        {
            if (char.IsLetterOrDigit(character) || character == '-' || character == '_') continue;
            
            Console.WriteLine("Name can only contain letters, digits, '-' and '_'");
            return false;
        }

        Value = input;
        return true;
    }
}

// Lists
public record ListArgument(string Name, string Description) : INameArgument<List>(Name, Description);
public record AddListArgument(string Name, string Description) : AddINameArgument<List>(Name, Description);

// Locks
public record LockArgument(string Name, string Description) : INameArgument<Lock>(Name, Description);
public record AddLockArgument(string Name, string Description) : AddINameArgument<Lock>(Name, Description);

public record TimeSpanArgument(string Name, string Description) : Argument<TimeSpan>(Name, Description)
{
    public override async Task<bool> Validate(string input)
    {
        var result = TimeSpan.TryParse(input, out var value);

        if (!result)
        {
            Console.WriteLine($"[{Name}] must be a valid timespan (HH:MM[:SS])");
            return false;
        }

        Value = value;
        return true;
    }
}

// Schedules
public record ScheduleArgument(string Name, string Description) : INameArgument<Schedule>(Name, Description);
public record AddScheduleArgument(string Name, string Description) : AddINameArgument<Schedule>(Name, Description);

public record DaysArgument(string Name, string Description) : Argument<DayOfWeek[]>(Name, Description)
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
                Console.WriteLine($"[{Name}] must be days of the week: MTWHFSU | weekdays | weekends | everyday");
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

public record TimeArgument(string Name, string Description) : Argument<TimeOnly>(Name, Description)
{
    public override async Task<bool> Validate(string input)
    {
        var result = TimeOnly.TryParse(input, out var value);

        if (!result)
        {
            Console.WriteLine($"[{Name}] must be a valid time (HH:MM:SS)");
            return false;
        }

        Value = value;
        return true;
    }
}

#endregion
