namespace CLI;

public enum Category 
{
    Blocking,
    Lists,
    Locks,
    Schedules,
    Configuration
}

public record Command(string[] Route, Category Category, string Description, List<IArgument> Arguments,
                      List<IFlag> Flags, Delegate Run, bool Executable = true, bool IsRoot = false);

