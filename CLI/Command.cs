using Microsoft.AspNetCore.SignalR.Client;
using SDK;

namespace CLI;

public interface IArgument
{
    public string Name { get; }
    public object? Value { get; }
    public bool Params { get; }

    public Task<bool> Validate(string input);
}

public abstract record Argument<T>(string Name, bool Params = false) : IArgument
{
    public T? Value { get; protected set; }
    object? IArgument.Value => Value;

    public abstract Task<bool> Validate(string input);
}

public record Command(string[] Route, List<IArgument> Arguments, Delegate Run, bool Edit = false, 
                      Func<Command, int, Task<string>>? GetDefault = null);

#region Arguments

public record StringArgument(string Name) : Argument<string>(Name)
{
    public override async Task<bool> Validate(string input)
    {
        Value = input;
        return true;
    }
}

public record EntriesArgument(string Name) : Argument<List<Entry>>(Name, true)
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

public record ListArgument(string Name) : Argument<List>(Name)
{
    public override async Task<bool> Validate(string input)
    {
        Value = await ConnectionManager.Connection!.InvokeAsync<List>("GetListFromNameAsync", input);

        if (Value == null)
        {
            Console.WriteLine($"List not found: {input}");
            return false;
        }

        return true;
    }
}

public record AddListArgument(string Name) : Argument<string>(Name)
{
    public override async Task<bool> Validate(string input)
    {
        var list = await ConnectionManager.Connection!.InvokeAsync<List>("GetListFromNameAsync", input);

        if (list != null)
        {
            Console.WriteLine($"List already exists: {input}");
            return false;
        }

        Value = input;
        return true;
    }
}

public record ScheduleArgument(string Name) : Argument<Schedule>(Name)
{
    public override async Task<bool> Validate(string input)
    {
        Value = await ConnectionManager.Connection!.InvokeAsync<Schedule>("GetScheduleFromNameAsync", input);

        if (Value == null)
        {
            Console.WriteLine($"Schedule not found: {input}");
            return false;
        }

        return true;
    }
}

public record AddScheduleArgument(string Name) : Argument<string>(Name)
{
    public override async Task<bool> Validate(string input)
    {
        var list = await ConnectionManager.Connection!.InvokeAsync<Schedule>("GetScheduleFromNameAsync", input);

        if (list != null)
        {
            Console.WriteLine($"Schedule already exists: {input}");
            return false;
        }

        Value = input;
        return true;
    }
}

public record TimeArgument(string Name) : Argument<TimeOnly>(Name)
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

public record DaysArgument(string Name) : Argument<DayOfWeek[]>(Name)
{
    public override async Task<bool> Validate(string input)
    {
        if (input.Equals("all", StringComparison.InvariantCultureIgnoreCase))
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
                Console.WriteLine($"[{Name}] must be days of the week: MTWHFSU/weekdays/weekends/all");
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

        Value = days.ToArray();
        return true;
    }
}

#endregion
