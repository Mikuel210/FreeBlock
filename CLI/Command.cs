using Microsoft.AspNetCore.SignalR.Client;
using SDK;

namespace CLI;

public interface IArgument
{
    public string Name { get; }
    public object? Value { get; }
    public Task<bool> Validate(string input);
}

public abstract record Argument<T>(string Name) : IArgument
{
    public T? Value { get; protected set; }
    object? IArgument.Value => Value;

    public abstract Task<bool> Validate(string input);
}

public record Command(string[] Route, List<IArgument> Arguments, Delegate Run);

#region Arguments

public record StringArgument(string Name) : Argument<string>(Name)
{
    public override async Task<bool> Validate(string input)
    {
        Value = input;
        return true;
    }
}

public record ListArgument(string Name) : Argument<BlockList>(Name)
{
    public override async Task<bool> Validate(string input)
    {
        Value = await ConnectionManager.Connection!.InvokeAsync<BlockList>("GetListFromNameAsync", input);

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
        var list = await ConnectionManager.Connection!.InvokeAsync<BlockList>("GetListFromNameAsync", input);

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

public record ArrayArgument<TValue, TArgument>(string Name) : Argument<TValue[]>(Name) where TArgument : IArgument
{
    public override async Task<bool> Validate(string input)
    {
        var inputs = input.Split(',').Select(e => e.Trim()).ToList();
        if (input.Trim().EndsWith(',')) inputs.RemoveAt(inputs.Count - 1);

        var argument = (TArgument)Activator.CreateInstance(typeof(TArgument), string.Empty)!;
        List<TValue> values = [];

        foreach (var value in inputs)
        {
            if (!await argument.Validate(value)) return false;
            values.Add((TValue)argument.Value!);
        }

        Value = values.Distinct().ToArray();
        return true;
    }
}

#endregion
