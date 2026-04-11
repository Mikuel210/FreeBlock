namespace CLI;

public static class ConsoleUtils
{

    public static bool PromptYesNo(string message, bool defaultValue)
    {
        bool writeLine = false;

        while (true)
        {
            Console.Write($"{message} ({(defaultValue ? 'Y' : 'y')}/{(defaultValue ? 'n' : 'N')}): ");
            var input = Console.ReadLine()!.Trim().ToLowerInvariant();

            if (input is "" or "y" or "yes")
            {
                if (writeLine) Console.WriteLine();
                return true;
            }

            if (input is "n" or "no") return false;
            writeLine = true;
        }
    }

    public static void Warning(string message) => Console.WriteLine($"warning: {message}");
    public static void Error(string message) => Console.WriteLine($"error: {message}");

}
