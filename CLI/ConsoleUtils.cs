using System.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
using Core;

namespace CLI;

public static class ConsoleUtils
{

    public static async Task<(List<Entry>, string)> EditEntries(string defaultContents = "", string? list = null)
    {
        // Initialize file
        var path = Path.GetTempFileName();
        File.WriteAllText(path, defaultContents);

        // Start editor
        Console.WriteLine("Waiting for your editor to close the file...");
        using var process = StartEditor(path);

        if (process == null) 
        {
            Error("Failed to launch text editor");
            Console.WriteLine();

            return ([], defaultContents);
        }
        else await process.WaitForExitAsync();

        // Sanitize input
        var lines = File.ReadAllLines(path).Select(e => e.Trim()).ToList();

        // Construct entries
        List<Entry> entries = [];
        Dictionary<int, string> errors = [];

        var lists = await ConnectionManager.Connection!.InvokeAsync<List<List>>("GetListsAsync");

        for (int i = 0; i < lines.Count; i++) 
        {
            string line = lines[i];
            bool result = line.ToEntry(out var entry, out var error, lists, list);

            if (!result)
            {
                if (error != null) errors.Add(i, error);
                continue;
            }

            entries.Add(entry!);
        }

        // Remove and log errors
        foreach (var error in errors)
            Warning(error.Value);

        foreach (var error in errors.Reverse())
            lines.RemoveAt(error.Key);
        
        if (errors.Count > 0) Console.WriteLine();

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

    public static string[] GetBlockReasons(StateSnapshot state, Entry entry)
    {
        List<string> blockReasons = [];
        if (state.Block.Contains(entry)) blockReasons.Add("manual");

        foreach (var @lock in state.Locks)
        {
            if (@lock.Entries.Contains(entry))
                blockReasons.Add($"🔒 {@lock.Name}");
        }

        foreach (var list in state.Lists.Where(e => e.IsActive(state)))
        {
            if (list.Entries.Contains(entry))
                blockReasons.Add($"📋 {list.Name}");
        }
            
        foreach (var schedule in state.Schedules.Where(e => e.Active))
        {  
            if (schedule.Entries.Contains(entry))
                blockReasons.Add($"⏰ {schedule.Name}");
        }

        return [.. blockReasons];
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

}
