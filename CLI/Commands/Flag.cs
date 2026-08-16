namespace CLI;

public interface IFlag
{
    public string LongName { get; }
    public string? ShortName { get; }
    public bool IsSwitch { get; }
    public object? Value { get; }

    public Task<bool> Validate(string input);
}

public abstract record SwitchFlag(string LongName, string? ShortName = null) : IFlag
{
    public bool IsSwitch => true;
    public object? Value => null;

    public abstract Task<bool> Validate(string input);
}

public abstract record ValueFlag<T>(string LongName, string? ShortName = null) : IFlag
{
    public bool IsSwitch => false;
    public T? Value { get; protected set; }
    object? IFlag.Value => Value;

    public abstract Task<bool> Validate(string input);
}

#region Flags

public record WarningTimeFlag() : ValueFlag<TimeSpan>("--warning-time")
{
    public override async Task<bool> Validate(string input)
    {
        var result = TimeSpan.TryParse(input, out var value);

        if (!result)
        {
            Console.WriteLine($"[{LongName}] must be a valid timespan (HH:MM(:SS))");
            return false;
        }

        Value = value;
        return true;
    }
}

#endregion