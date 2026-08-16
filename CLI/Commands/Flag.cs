namespace CLI;

public interface IFlag
{
    public string LongName { get; }
    public string? ShortName { get; }
    public string Description { get; }

    public bool IsSwitch { get; }
    public object? Value { get; }

    public Task<bool> Validate(string input);
}

public abstract record SwitchFlag(string Description, string LongName, string? ShortName = null) : IFlag
{
    public bool IsSwitch => true;
    public object? Value => null;

    public async Task<bool> Validate(string input) => true;
}

public abstract record ValueFlag<T>(string Description, string LongName, string? ShortName = null) : IFlag
{
    public bool IsSwitch => false;
    public T? Value { get; protected set; }
    object? IFlag.Value => Value;

    public abstract Task<bool> Validate(string input);
}

#region Flags

public record VersionFlag() : SwitchFlag("Show the FreeBlock version", "--version", "-v");

public record UninstallFlag() : SwitchFlag("Uninstall FreeBlock", "--uninstall");

public record WarningTimeFlag() : ValueFlag<TimeSpan>("The time before a schedule to be notified the schedule is about to start", "--warning-time")
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

#endregion