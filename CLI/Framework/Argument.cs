using Core;
using Lock = Core.Lock;
using ValidateBaseDelegate = System.Func<Core.IName, string, System.Threading.Tasks.Task<(bool, object?)>>;

namespace CLI;

public interface IArgument : IName
{
    public string Description { get; }
    public object? Value { get; protected set; }
    public bool Params { get; }

    public ValidateBaseDelegate ValidateBase { get; };

    public async Task<bool> Validate(string input)
    {
        var (result, value) = await ValidateBase(this, input);

        Value = value;
        return result;
    }
}

public abstract record Argument<T>(string Name, string Description, ValidateBaseDelegate ValidateBase, bool Params = false) : IArgument
{
    public T? Value { get; protected set; }

    object? IArgument.Value {
        get => Value;
        set => Value = (T)value!;
    }
}

#region Arguments

public record StringArgument(string Name, string Description) : Argument<string>(Name, Description, Validator.ValidateString);

public record EntriesArgument(string Name, string Description) : Argument<List<Entry>>(Name, Description, Validator.ValidateEntries, true);

public record INameArgument<T>(string Name, string Description) : Argument<T>(Name, Description) where T : IName
{
    public override async Task<bool> Validate(string input)
    {
        var (result, value) = await Validator.ValidateIName<T>(input);

        Value = value;
        return result;
    }
}

public record AddINameArgument<T>(string Name, string Description) : Argument<string>(Name, Description) where T : IName
{
    public override async Task<bool> Validate(string input)
    {
        var (result, value) = await Validator.ValidateAddIName<T>(input);

        Value = value;
        return result;
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
        var (result, value) = await Validator.ValidateTimeSpan(input);

        Value = value;
        return result;
    }
}

// Schedules
public record ScheduleArgument(string Name, string Description) : INameArgument<Schedule>(Name, Description);
public record AddScheduleArgument(string Name, string Description) : AddINameArgument<Schedule>(Name, Description);

public record DaysArgument(string Name, string Description) : Argument<DayOfWeek[]>(Name, Description)
{
    public override async Task<bool> Validate(string input)
    {
        var (result, value) = await Validator.ValidateDays(input);

        Value = value;
        return result;
    }
}

public record TimeArgument(string Name, string Description) : Argument<TimeOnly>(Name, Description)
{
    public override async Task<bool> Validate(string input)
    {
        var (result, value) = await Validator.ValidateTimeOnly(input);

        Value = value;
        return result;
    }
}

// Configuration
public record ConfigKeyArgument(string Name, string Description) : Argument<string>(Name, Description)
{
    public override async Task<bool> Validate(string input)
    {
        var (result, value) = await Validator.ValidateConfigKey(input);

        Value = value;
        return result;
    }
}

public record ConfigValueArgument(string Name, string Description, ConfigKeyArgument KeyArgument) : Argument<object?>(Name, Description)
{
    public override async Task<bool> Validate(string input)
    {

    }
}

public record BoolArgument(string Name, string Description) : Argument<bool>(Name, Description)
{
    public override async Task<bool> Validate(string input)
    {
        input = input.ToLowerInvariant();

        if (input is "true" or "1")
        {
            Value = true;
            return true;
        }

        if (input is "false" or "0")
        {
            Value = false;
            return true;
        }

        Console.WriteLine($"[{Name}] must be a boolean (true | false)");
        return false;
    }
}

#endregion
