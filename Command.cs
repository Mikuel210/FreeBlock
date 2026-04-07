namespace FreeBlock;

public interface IArgument
{
    public string Name { get; }
    public bool Validate(string input);
}

public abstract record Argument<T>(string Name) : IArgument
{
    public T? Value { get; protected set; }
    public abstract bool Validate(string input);
}

public record StringArgument(string Name) : Argument<string>(Name)
{
    public override bool Validate(string input)
    {
        Value = input;
        return true;
    }
}

public enum ListArgumentType
{
    MustExist,
    MustNotExist
}

public record ListArgument(string Name) : Argument<BlockList>(Name)
{
    public override bool Validate(string input)
    {
        Value = BlockList.FromName(input);

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
    public override bool Validate(string input)
    {
        var list = BlockList.FromName(input);

        if (list != null)
        {
            Console.WriteLine($"List already exists: {input}");
            return false;
        }

        Value = input;
        return true;
    }
}

public record IntArgument(string Name) : Argument<int>(Name)
{
    public override bool Validate(string input)
    {
        var result = int.TryParse(input, out var value);

        if (!result)
        {
            Console.WriteLine($"[{Name}] must be an integer");
            return false;
        }
        
        Value = value;
        return true;
    }
}

public record Command(string[] Route, List<IArgument> Arguments, Delegate Run);