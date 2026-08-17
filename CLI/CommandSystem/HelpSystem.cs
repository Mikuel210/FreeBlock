namespace CLI;

public static class HelpSystem
{
    public static void ShowHelp(Command command)
    {
        Console.WriteLine($"Usage: {GetUsage(command)}");
        Console.WriteLine($"Description: {command.Description}");

        if (command.Arguments.Count > 0)
        {
            Console.WriteLine("\nArguments:");
            int spaces = command.Arguments.Select(e => e.Name).Max(e => e.Length) + 3;

            foreach (var argument in command.Arguments)
            {
                string name = argument.Name;
                string spaceString = new(' ', spaces - name.Length);
                Console.WriteLine($"  {name}{spaceString}{argument.Description}");
            }
        }

        if (command.Flags.Count > 0)
        {
            Console.WriteLine("\nFlags:");

            Dictionary<IFlag, string> flagNames = [];

            foreach (var flag in command.Flags)
            {
                string name = flag.LongName;

                if (flag.ShortName != null)
                    name = $"{flag.ShortName}, {flag.LongName}";

                flagNames.Add(flag, name);
            }

            int spaces = flagNames.Values.Max(e => e.Length) + 3;

            foreach (var flag in command.Flags)
            {
                var name = flagNames[flag];
                string spaceString = new(' ', spaces - name.Length);
                Console.WriteLine($"  {name}{spaceString}{flag.Description}");
            }
        }

        List<Command> subcommands = GetSubcommands(command);

        if (subcommands.Count > 0)
        {
            Console.WriteLine(command.IsRoot ? "\nBelow is a list of all available commands" : "\nSubcommands:");

            Dictionary<Command, string> names = [];
            Category? currentCategory = null;

            foreach (var subcommand in subcommands)
                names.Add(subcommand, $"freeblock {string.Join(' ', subcommand.Route)}");

            int spaces = names.Values.Max(e => e.Length) + 3;

            foreach (var subcommand in subcommands)
            {
                if (command.IsRoot && subcommand.Category != currentCategory)
                {
                    currentCategory = subcommand.Category;

                    Console.WriteLine('\n' + currentCategory switch
                    {
                        Category.Blocking => "Manage blocking:",
                        Category.Lists => "Manage lists:",
                        Category.Locks => "Manage locks:",
                        Category.Schedules => "Manage schedules:",
                        _ => throw new NotImplementedException()
                    });
                }

                string name = names[subcommand];
                string spaceString = new(' ', spaces - name.Length);
                Console.WriteLine($"  {name}{spaceString}{subcommand.Description}");
            }
        }
    }

    public static void ShowUsage(Command command)
    {
        string space = command.IsRoot ? "" : " ";

        Console.WriteLine($"Usage: {GetUsage(command, false)}");
        Console.WriteLine($"See: freeblock {string.Join(' ', command.Route)}{space}--help");
    }

    private static string GetUsage(Command command, bool includeFlags = true)
    {
        if (command.IsRoot)
        {
            if (includeFlags)
                return "freeblock [-v | --version] [-h | --help] [--uninstall] <command> [<args>]";
            
            return "freeblock <command> [<args>]";
        }

        string arguments = string.Join(' ', command.Arguments.Select(e => $"<{e.Name}>"));
        if (command.Arguments.Any(e => e.Params)) arguments += "...";

        string flags = includeFlags ? string.Join(' ', command.Flags.Select(e => {
            string name = e.LongName;

            if (e.ShortName != null)
                name = $"{e.ShortName} | {name}";

            if (e.IsSwitch) return $"[{name}]";
            string value = e.ValueName == null ? $"<{e.LongName[2 ..]}>" : $"<{e.ValueName}>";

            if (e.ShortName != null)
                return $"[({name}) {value}]";

            return $"[{name} {value}]";
        })) : "";

        if (command.Executable)
            return $"freeblock {string.Join(' ', command.Route)} {arguments} {flags}";
        else
            return $"freeblock {string.Join(' ', command.Route)} <subcommand>";
    }

    private static List<Command> GetSubcommands(Command parent)
    {
        List<Command> commands = [];

        foreach (var command in CommandSystem.Commands)
        {
            if (command != parent
                && command.Route.Length >= parent.Route.Length 
                && command.Route[.. parent.Route.Length].SequenceEqual(parent.Route) 
                && command.Executable)
                commands.Add(command);
        }

        return commands;
    }
}