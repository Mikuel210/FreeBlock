using ValidateBaseDelegate = System.Func<Core.IName, string, System.Threading.Tasks.Task<(bool, object?)>>;
using Core;
using Lock = Core.Lock;

namespace CLI;

public interface IArgument : IName
{
    public string Description { get; }
    public object? Value { get; protected set; }
    public bool Params { get; }

    public ValidateBaseDelegate ValidateBase { get; }
    public Task<bool> Validate(string input);
}

public abstract record Argument<T>(string Name, string Description, ValidateBaseDelegate ValidateBase, bool Params = false) : IArgument
{
    public T? Value { get; protected set; }

    object? IArgument.Value {
        get => Value;
        set => Value = (T)value!;
    }

    public virtual async Task<bool> Validate(string input)
    {
        var (result, value) = await ValidateBase(this, input);
        if (result) Value = (T?)value;

        return result;
    }
}

#region Arguments

public record StringArgument(string Name, string Description) : Argument<string>(Name, Description, Validator.ValidateString);

public record EntriesArgument(string Name, string Description) : Argument<List<Entry>>(Name, Description, Validator.ValidateEntries, true);

public record INameArgument<T>(string Name, string Description) : Argument<T>(Name, Description, Validator.ValidateIName<T>) where T : IName;

public record AddINameArgument<T>(string Name, string Description) : Argument<string>(Name, Description, Validator.ValidateAddIName<T>) where T : IName;

// Lists
public record ListArgument(string Name, string Description) : INameArgument<List>(Name, Description);
public record AddListArgument(string Name, string Description) : AddINameArgument<List>(Name, Description);

// Locks
public record LockArgument(string Name, string Description) : INameArgument<Lock>(Name, Description);
public record AddLockArgument(string Name, string Description) : AddINameArgument<Lock>(Name, Description);

// Schedules
public record ScheduleArgument(string Name, string Description) : INameArgument<Schedule>(Name, Description);
public record AddScheduleArgument(string Name, string Description) : AddINameArgument<Schedule>(Name, Description);

public record TimeSpanArgument(string Name, string Description) : Argument<TimeSpan>(Name, Description, Validator.ValidateTimeSpan);
public record DaysArgument(string Name, string Description) : Argument<DayOfWeek[]>(Name, Description, Validator.ValidateDays);
public record TimeArgument(string Name, string Description) : Argument<TimeOnly>(Name, Description, Validator.ValidateTimeOnly);

// Configuration
public record ConfigKeyArgument(string Name, string Description) : Argument<string>(Name, Description, Validator.ValidateConfigKey);

public record ConfigValueArgument(string Name, string Description, ConfigKeyArgument KeyArgument)
    : Argument<object?>(Name, Description, Validator.ValidateConfigValue)
{
    public ConfigKeyArgument KeyArgument { get; set; } = KeyArgument;
}

public record BoolArgument(string Name, string Description) : Argument<bool>(Name, Description, Validator.ValidateBool);

#endregion
