using System.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
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

            if (input is "y" or "yes" || (input == "" && defaultValue))
            {
                if (writeLine && !disableWriteLine) Console.WriteLine();
                return true;
            }

            if (input is "n" or "no" || (input == "" && !defaultValue)) return false;

            writeLine = true;
        }
    }

    public static bool PromptClose(bool disableWriteLine = false) 
        => PromptYesNo($"This will close all browsers and all blocked apps. Okay to continue?", true, disableWriteLine);

    public static async Task<(List<Entry>, string)> EditEntries(string defaultContents = "", string? list = null)
    {
        // Initialize file
        var path = Path.GetTempFileName();
        File.WriteAllText(path, defaultContents);

        // Start editor
        using var process = StartEditor(path);

        if (process != null)
        {
            Console.WriteLine("Waiting for your editor to close the file...");
            await process.WaitForExitAsync();
        }
        else Error("Failed to launch text editor");

        // Sanitize input
        var lines = File.ReadAllLines(path).Select(e => e.Trim()).ToList();
        lines = [.. lines.Where(e => !string.IsNullOrWhiteSpace(e) && !e.StartsWith("#"))];

        // Construct entries
        List<Entry> entries = [];
        Dictionary<int, string> errors = [];

        var lists = await ConnectionManager.Connection!.InvokeAsync<List<List>>("GetListsAsync");

        for (int i = 0; i < lines.Count; i++) 
        {
            string line = lines[i];
            var lineWithoutPrefix = line[1 ..].Trim();

            if (line.StartsWith("!"))
            {
                entries.Add(new(EntryType.App, lineWithoutPrefix));
                continue;
            }

            if (line.StartsWith("@"))
            {
                if (!lists.Select(e => e.Name).Contains(lineWithoutPrefix))
                {
                    errors.Add(i, $"Missing list was removed: {lineWithoutPrefix}");
                    continue;
                }

                var entry = new Entry(EntryType.List, lineWithoutPrefix);

                if (list != null && entry.IsRecursive(name => lists.Single(e => e.Name == name), [list]))
                {
                    errors.Add(i, $"Recursive list entry was removed: {lineWithoutPrefix}");
                    continue;
                }

                entries.Add(entry);
                continue;
            }
                
            entries.Add(new(EntryType.Website, line.SanitizeUrl()));
        }

        // Remove and log errors
        foreach (var error in errors)
            Warning(error.Value);

        if (errors.Count > 0) Console.WriteLine();

        foreach (var error in errors.Reverse())
            lines.RemoveAt(error.Key);

        // Remove file
        File.Delete(path);

        entries = [.. entries.Distinct()];
        return (entries, string.Join('\n', lines));
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

    public static void Note(string message) => TitledMessage("Note", message, ConsoleColor.Blue);
    public static void Warning(string message) => TitledMessage("Warning", message, ConsoleColor.Yellow);
    public static void Error(string message) => TitledMessage("Error", message, ConsoleColor.Red);

}
