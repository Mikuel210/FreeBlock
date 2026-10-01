using Microsoft.Extensions.DependencyInjection;

namespace CLI;

public static class CommandSystem
{

    private static readonly List<Command> _commands = [];
    public static List<Command> Commands => _commands;

    public static void Register(Command command) => _commands.Add(command);

    public static async Task<int> Handle(string[] args)
    {
        // Find command
        args = [.. args.Select(e => e.Trim())];
        var command = GetMatchingCommand(args);

        if (command != null)
            args = [.. args.Skip(command.Route.Length)];

        if (command == null)
        {
            // Checked before the flag detection below, which would otherwise dereference a null command (C4).
            Console.WriteLine($"Command not found: freeblock {string.Join(' ', args)}");
            Console.WriteLine("See: freeblock --help");
            return 1;
        }

        // Find positional and flag tokens
        int flagStart = Array.FindIndex(args, a => a.StartsWith('-'));
        if (flagStart < command.Arguments.Count) flagStart = -1;

        var positional = flagStart == -1 ? args : args[.. flagStart];
        var flagTokens = flagStart == -1 ? [] : args[flagStart ..];

        // Trigger help
        if (args.Any(e => e is "-?" or "-h" or "--help"))
        {
            HelpSystem.ShowHelp(command);
            return 0;
        }

        // Trigger usage
        if (!command.Executable && !command.IsRoot)
        {
            HelpSystem.ShowUsage(command);
            return 0;
        }

        // Validate arguments
        var positionalList = positional.ToList();

        if (command.Arguments.LastOrDefault()?.Params == true && positionalList.Count > command.Arguments.Count)
        {
            var lastArgument = string.Join(' ', positionalList.Skip(command.Arguments.Count - 1));
            positionalList = [.. positionalList.GetRange(0, command.Arguments.Count - 1), lastArgument];
        }

        int validateResult = await ValidateArguments(command, [.. positionalList]);
        if (validateResult != 0) return validateResult;

        // Validate flags
        object?[] parameters = [.. command.Arguments];

        if (command.Flags.Count > 0)
        {
            (validateResult, var flags) = await ValidateFlags(command, [.. flagTokens]);
            if (validateResult != 0) return validateResult;

            parameters = [.. parameters, flags];
        }

        // Run command
        if (command.Run.DynamicInvoke(parameters) is Task<int> task)
            return await task;
        else
            throw new NotImplementedException();
    }

    private static async Task<int> ValidateArguments(Command command, List<string> argsList)
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

            // Remove incorrect argument from array
            if (result) goto Continue;
            argsList.RemoveRange(i, argsList.Count - i);

            Read:
            string space = (argsList.Count == 0 || argsList[0] == string.Empty) ? "" : " ";
            Console.Write($"freeblock {string.Join(" ", command.Route)}{space}{string.Join(" ", argsList)} <{argument.Name}>: ");

            var input = Console.ReadLine()!.Trim();
            hasRead = true;

            // Check empty argument
            if (input == string.Empty)
            {
                Console.WriteLine($"<{argument.Name}> can't be empty");
                goto Read;
            }

            // Add argument to array
            argsList.Add(input);
            goto Validate;

            Continue:
            i++;
        }

        if (argsList.Count <= command.Arguments.Count) goto End;

        argsList = [.. argsList.Skip(command.Arguments.Count)];

        if (argsList.Count > 0)
        {
            ConsoleUtils.Error($"Unexpected argument: {argsList[0]}");
            return 1;
        }

        End:
        if (hasRead) Console.WriteLine();
        return 0;
    }

    private static Command? GetMatchingCommand(string[] args)
    {
        List<Command> matches = [];

        foreach (var command in _commands)
        {
            if (args.Length < command.Route.Length) continue;
            if (command.IsRoot && args.Length != 0) continue;

            for (int i = 0; i < command.Route.Length; i++)
            {
                if (command.Route[i] != args[i])
                    goto End;
            }

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

    private static async Task<(int, List<IFlag>)> ValidateFlags(Command command, List<string> tokensList)
    {
        List<IFlag> flags = [];
        int i = 0;

        while (true)
        {
            if (i >= tokensList.Count) break;

            string token = tokensList[i];

            foreach (var flag in command.Flags)
            {
                if (token != flag.Name && token != flag.ShortName) continue;

                // Switch flags
                if (flag.IsSwitch)
                {
                    flags.Add(flag);
                    goto Continue;
                }

                // Value flags
                i++;

                if (i >= tokensList.Count || tokensList[i].StartsWith('-'))
                {
                    Console.WriteLine($"[{flag.Name}] can't be empty");
                    goto Continue;
                }

                token = tokensList[i];

                // Single value
                if (!flag.Params)
                {
                    if (!await flag.Validate(token)) goto Continue;

                    flags.Add(flag);
                    goto Continue;
                }

                // Multiple values
                List<string> tokens = [];

                while (true)
                {
                    if (token.StartsWith('-'))
                    {
                        i--;
                        break;
                    }

                    tokens.Add(token);
                    i++;

                    if (i >= tokensList.Count) break;
                    token = tokensList[i];
                }

                if (!await flag.Validate(string.Join(' ', tokens))) goto Continue;

                flags.Add(flag);
                goto Continue;
            }

            // Unknown flag
            ConsoleUtils.Error($"Unknown flag: {token}");
            return (1, []);

            Continue:
            i++;
        }

        return (0, flags);
    }

}
