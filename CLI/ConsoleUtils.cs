using System.Diagnostics;
using SDK;

namespace CLI;

public static class ConsoleUtils
{

    public static bool PromptYesNo(string message, bool defaultValue = true, bool disableWriteLine = false)
    {
        bool writeLine = false;

        while (true)
        {
            Console.Write($"{message} ({(defaultValue ? 'Y' : 'y')}/{(defaultValue ? 'n' : 'N')}): ");
            var input = Console.ReadLine()!.Trim().ToLowerInvariant();

            if (input is "" or "y" or "yes")
            {
                if (writeLine && !disableWriteLine) Console.WriteLine();
                return true;
            }

            if (input is "n" or "no") return false;
            writeLine = true;
        }
    }

    public static void Note(string message) => TitledMessage("Note", message, ConsoleColor.Blue);
    public static void Warning(string message) => TitledMessage("Warning", message, ConsoleColor.Yellow);
    public static void Error(string message) => TitledMessage("Error", message, ConsoleColor.Red);

    public static async Task EditList(BlockList list)
    {
        // Initialize file
        var path = Path.GetTempFileName();
        File.WriteAllLines(path, list.UrlList);

        // Start editor
        using var process = StartEditor(path);
        if (process == null) return;

        await process.WaitForExitAsync();

        // Sanitize input
        var lines = File.ReadAllLines(path);
        lines = lines.Where(e => !string.IsNullOrWhiteSpace(e)).ToArray();

        for (int i = 0; i < lines.Length; i++)
            lines[i] = lines[i].SanitizeUrl();

        lines = lines.Distinct().ToArray();

        // Update list
        list.UrlList.Clear();
        list.UrlList.AddRange(lines);

        // Remove file
        File.Delete(path);
    }

    private static Process? StartEditor(string path)
    {
        var editor = Environment.GetEnvironmentVariable("EDITOR");

        if (string.IsNullOrEmpty(editor))
        {
            if (OperatingSystem.IsWindows()) editor = "notepad.exe";
            else editor = "nano";
        }

        var startInfo = new ProcessStartInfo
        {
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = editor;
            startInfo.Arguments = path;
            startInfo.UseShellExecute = true;
        }
        else
        {
            startInfo.FileName = "/bin/sh";
            startInfo.Arguments = $"-c \"{editor} '{path.Replace("'", "\\'")}'\"";
        }

        return Process.Start(startInfo);
    }

    private static void TitledMessage(string title, string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write($"{title.ToLowerInvariant()}: ");

        Console.ResetColor();
        Console.WriteLine(message);
    }

}
