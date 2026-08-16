namespace CLI;

public static class CommandSystem
{

    private static readonly List<Command> _commands = [];
    public static List<Command> Commands => _commands;

    public static void Register(Command command) => _commands.Add(command);

    public static async Task Handle(string[] args)
    {
        // Find command
        args = [.. args.Select(e => e.Trim())];
        var command = GetMatchingCommand(args);

        if (command == null)
        {
            Console.WriteLine($"Command not found: freeblock {string.Join(' ', args)}");
            Console.WriteLine("See: freeblock --help");
            return;
        }

        args = [.. args.Skip(command.Route.Length)];

        // Find positional and flag tokens
        int flagStart = Array.FindIndex(args, a => a.StartsWith('-'));
        if (flagStart < command.Arguments.Count) flagStart = -1;

        var positional = flagStart == -1 ? args : args[.. flagStart];
        var flagTokens = flagStart == -1 ? [] : args[flagStart ..];

        // Validate arguments
        var positionalList = positional.ToList();

        if (command.Arguments.LastOrDefault()?.Params == true && positionalList.Count > command.Arguments.Count)
        {
            var lastArgument = string.Join(' ', positionalList.Skip(command.Arguments.Count - 1));
            positionalList = [.. positionalList.GetRange(0, command.Arguments.Count - 1), lastArgument];
        }

        // Trigger help
        if ((!command.Executable && !command.IsRoot) || args.Any(e => e is "-?" or "-h" or "--help")) 
        {
            HelpSystem.ShowHelp(command);
            return;
        }

        await ValidateArguments(command, [.. positionalList]);

        // Validate flags
        object?[] parameters = [.. command.Arguments];
        var flags = await ValidateFlags(command, [.. flagTokens]); // Run validation for every command to catch unexpected arguments

        if (command.Flags.Count > 0)
            parameters = [.. parameters, flags];

        // Run command
        if (command.Run.DynamicInvoke(parameters) is Task task)
            await task;
    }

    private static async Task ValidateArguments(Command command, List<string> argsList) 
    {
        bool hasRead = false;
        int i = 0;

        while (true)
        {
            if (i >= command.Arguments.Count) break;

            // Read if no argument provided
            var argument = command.Arguments[i];
            if (argsList.Count <= i) goto Read;

            Validate:
            var result = await argument.Validate(argsList[i]);
            if (hasRead || (!hasRead && !result)) Console.WriteLine();

            // Remove incorrect argument from array
            if (result) goto Continue;
            argsList.RemoveRange(i, argsList.Count - i);

            Read:
            string space = (argsList.Count == 0 || argsList[0] == string.Empty) ? "" : " ";
            string defaultArg = command.GetDefault != null ? await command.GetDefault(command, i) : "";

            Console.Write($"freeblock {string.Join(" ", command.Route)}{space}{string.Join(" ", argsList)} [{argument.Name}]"
                + $"{(command.Edit && i != 0 ? $" ({defaultArg})" : "")}: ");

            var input = Console.ReadLine()!.Trim();
            hasRead = true;

            // Check empty argument
            if (input == string.Empty)
            {
                if (!command.Edit || i == 0) 
                {
                    Console.WriteLine($"[{argument.Name}] can't be empty");
                    Console.WriteLine();
                    goto Read;
                }

                input = defaultArg;
            }

            // Add argument to array
            argsList.Add(input);
            goto Validate;

            Continue:
            i++;
        }
    }

    private static Command? GetMatchingCommand(string[] args)
    {
        List<Command> matches = [];

        foreach (var command in _commands)
        {
            if (args.Length < command.Route.Length) continue;
            if (command.IsRoot && args.Length != 0) continue;

            for (int i = 0; i < command.Route.Length; i++)
                if (command.Route[i] != args[i]) goto End;

            matches.Add(command);
        End:;
        }

        if (matches.Count == 0)
        {
            if (args[0].StartsWith('-'))
                return _commands.Single(e => e.IsRoot);
            
            return null;
        }

        return matches.MaxBy(e => e.Route.Length);
    }

    private static async Task<List<IFlag>> ValidateFlags(Command command, List<string> tokensList)
    {
        bool warnings = false;
        List<IFlag> flags = [];
        int i = 0;

        while (true)
        {
            if (i >= tokensList.Count) break;

            string token = tokensList[i];
            
            foreach (var flag in command.Flags)
            {
                if (token != flag.LongName && token != flag.ShortName) continue;

                if (flag.IsSwitch)
                {
                    flags.Add(flag);
                    goto Continue;
                }

                i++;
                token = tokensList[i];
                
                if (await flag.Validate(token))
                {
                    flags.Add(flag);
                    goto Continue;
                }
            }

            // Unrecognized flag
            ConsoleUtils.Warning($"Unexpected {(token.StartsWith('-') ? "flag" : "argument")}: {token}");
            warnings = true;

            Continue:
            i++;
        }

        if (warnings) Console.WriteLine();
        return flags;
    }

}
