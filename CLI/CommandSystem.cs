namespace CLI;

public static class CommandSystem
{

    private static readonly List<Command> _commands = [];
    public static void Register(Command command) => _commands.Add(command);

    public static async Task Handle(string[] args)
    {
        // Find command
        args = args.Select(e => e.Trim()).ToArray();
        var command = GetMatchingCommand(args);

        if (command == null)
        {
            Console.WriteLine($"Command not found: freeblock {string.Join(" ", args)}");
            Console.WriteLine("See: freeblock --help");
            return;
        }

        // Validate arguments
        args = [.. args.Skip(command.Route.Length)];
        var argsList = args.ToList();
        var writeLine = false;

        for (int i = 0; i < command.Arguments.Count; i++)
        {
            // Read if no argument provided
            var argument = command.Arguments[i];
            if (argsList.Count <= i) goto Read;

            // Validate argument
        Validate:
            var result = await argument.Validate(argsList[i]);
            if (writeLine) Console.WriteLine();
            if (result) continue;

            // Remove incorrect argument from array
            argsList.RemoveAt(argsList.Count - 1);

            // Read argument
        Read:
            string space = (argsList.Count == 0 || argsList[0] == string.Empty) ? "" : " ";
            Console.Write($"freeblock {string.Join(" ", command.Route)}{space}{string.Join(" ", argsList)} [{argument.Name}]: ");

            var input = Console.ReadLine()!.Trim();
            writeLine = true;

            // Check empty argument
            if (input == string.Empty)
            {
                Console.WriteLine($"[{argument.Name}] can't be empty");
                Console.WriteLine();
                goto Read;
            }

            // Add argument to array
            argsList.Add(input);
            goto Validate;
        }

        // Run command
        if (command.Run.DynamicInvoke(command.Arguments.ToArray()) is Task task)
            await task;
    }

    private static Command? GetMatchingCommand(string[] args)
    {
        foreach (var command in _commands)
        {
            if (args.Length < command.Route.Length) continue;
            if (command.Route.Length == 0 && args.Length != 0) continue;

            for (int i = 0; i < command.Route.Length; i++)
                if (command.Route[i] != args[i]) goto End;

            return command;
        End:;
        }

        return null;
    }

}
