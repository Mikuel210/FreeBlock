using ValidateBaseDelegate = System.Func<Core.IName, string, System.Threading.Tasks.Task<(bool, object?)>>;
using Core;

namespace CLI;

public interface IFlag : IName
{
    public string? ShortName { get; }
    public string Description { get; }
    public string? ValueName { get; }

    public bool IsSwitch { get; }
    public bool Params { get; }
    public object? Value { get; protected set; }

    public ValidateBaseDelegate ValidateBase { get; }
    public Task<bool> Validate(string input);
}

public abstract record SwitchFlag(string Description, string Name, string? ShortName = null) : IFlag
{
    public string? ValueName => null;
    public bool IsSwitch => true;
    public bool Params => false;

    public object? Value
    {
        get;
        set => field = null;
    }

    public ValidateBaseDelegate ValidateBase => (_, _) => Task.FromResult<(bool, object?)>((true, null));
    public Task<bool> Validate(string input) => Task.FromResult(true);
}

public abstract record ValueFlag<T>(string Description, bool Params, ValidateBaseDelegate ValidateBase, string Name,
    string? ShortName = null, string? ValueName = null) : IFlag
{
    public bool IsSwitch => false;
    public T? Value { get; protected set; }

    object? IFlag.Value
    {
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

#region Flags

public record EntriesFlag(string Description, string Name, string? ShortName = null)
    : ValueFlag<List<Entry>>(Description, true, Validator.ValidateEntries, Name, ShortName, "entries");

public record TimeSpanFlag(string Description, string Name, string? ShortName = null)
    : ValueFlag<TimeSpan>(Description, false, Validator.ValidateTimeSpan, Name, ShortName, "timespan");

public record TimeFlag(string Description, string Name, string? ShortName = null)
    : ValueFlag<TimeOnly>(Description, false, Validator.ValidateTimeOnly, Name, ShortName, "time");

public record DaysFlag(string Description, string Name, string? ShortName = null)
    : ValueFlag<DayOfWeek[]>(Description, false, Validator.ValidateDays, Name, ShortName, "days");

public record VersionFlag() : SwitchFlag("Show the FreeBlock version", "--version", "-v");
public record UninstallFlag() : SwitchFlag("Uninstall FreeBlock", "--uninstall");

public record LockAddEntriesFlag() : EntriesFlag("Add entries to the lock", "--add-entries", "-a");
public record LockExtendFlag() : TimeSpanFlag("Extend the lock by the provided amount of time (HH:MM[:SS])", "--extend", "-e");

public record ScheduleStartTimeFlag() : TimeFlag("Change the start time of the schedule (HH:MM[:SS])", "--start-time", "-s");
public record ScheduleEndTimeFlag() : TimeFlag("Change the end time of the schedule (HH:MM[:SS])", "--end-time", "-x");
public record ScheduleDaysFlag() : DaysFlag("Change the days of the week to apply the schedule (MTWHFSU)", "--days", "-d");
public record ScheduleEntriesFlag() : EntriesFlag("Change the entries of the schedule", "--entries", "-e");
public record ScheduleWarningTimeFlag() : TimeSpanFlag("Change the warning period before the schedule starts (HH:MM[:SS])", "--warning-time", "-w");

#endregion
