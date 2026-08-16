namespace CLI;

public enum Category 
{
    Blocking,
    Lists,
    Locks,
    Schedules
}

public record Command(string[] Route, Category Category, string Description, List<IArgument> Arguments, List<IFlag> Flags, Delegate Run,
                      bool Edit = false, Func<Command, int, Task<string>>? GetDefault = null, bool Executable = true, bool IsRoot = false);

