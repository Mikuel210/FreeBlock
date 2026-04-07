using System.Reflection;

namespace FreeBlock;

public static class CommandSystem
{
    
    private static readonly List<Command> _commands = [];
    public static void Register(Command command) => _commands.Add(command);

    public static void Handle(string[] args)
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
        args = args.Skip(command.Route.Length).ToArray();
        for (int i = 0; i < command.Arguments.Count; i++)
        {
            // Read if no argument provided
            var argument = command.Arguments[i];
            if (args.Length <= i) goto Read;
            
            // Validate argument 
            Validate: var result = argument.Validate(args[i]);
            Console.WriteLine();
            if (result) continue;

            // Remove incorrect argument from array
            var list = args.ToList();
            list.RemoveAt(list.Count - 1);
            args = list.ToArray();

            // Read argument
            Read: string space = (args.Length == 0 || args[0] == string.Empty) ? "" : " ";
            Console.Write($"freeblock {string.Join(" ", command.Route)}{space}{string.Join(" ", args)} [{argument.Name}]: ");
            var input = Console.ReadLine()!.Trim();
            
            // Check empty argument
            if (input == string.Empty)
            {
                Console.WriteLine($"[{argument.Name}] can't be empty");
                Console.WriteLine();
                goto Read;
            }
            
            // Add argument to array
            var list1 = args.ToList();
            list1.Add(input);
            args = list1.ToArray();
                
            goto Validate;
        }

        // Run command
        command.Run.GetMethodInfo().Invoke(command, command.Arguments.ToArray());
    }

    private static Command? GetMatchingCommand(string[] args)
    {
        foreach (var command in _commands)
        {
            if (args.Length < command.Route.Length) continue;
            
            for (int i = 0; i < command.Route.Length; i++)
                if (command.Route[i] != args[i]) goto End;

            return command;
            End:;
        }

        return null;
    }

}