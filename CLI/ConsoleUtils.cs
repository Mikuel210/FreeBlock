namespace CLI;

public static class ConsoleUtils
{

    public static bool PromptYesNo(string message, bool defaultValue)
    {
        while (true)
        {
            Console.Write($"{message} ({(defaultValue ? 'Y' : 'y')}/{(defaultValue ? 'n' : 'N')}): ");
            var input = Console.ReadLine()!.Trim().ToLowerInvariant();

            if (input is "" or "y") return true;
            if (input == "n") return false;
        }
    }

}
