using System.Diagnostics;
using System.Text;
using Core;

namespace Daemon;

public static class Blocker
{

    private const string REDIRECT = "0.0.0.0";

    private static readonly HashSet<string> _browsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe", "Google Chrome", "google-chrome", "chrome",
        "firefox.exe", "firefox", "firefox-bin",
        "msedge.exe", "Microsoft Edge", "microsoft-edge",
        "Safari",
        "opera.exe", "Opera", "opera",
        "brave.exe", "Brave Browser", "brave-browser", "brave",
        "vivaldi.exe", "Vivaldi", "vivaldi-bin",
        "Arc.exe", "Arc", "arc",
        "DuckDuckGo.exe", "DuckDuckGo",
        "tor-browser",
        "mullvadbrowser.exe", "Mullvad Browser", "mullvad-browser",
        "librewolf.exe", "LibreWolf", "librewolf",
        "floorp.exe", "Floorp", "floorp",
        "waterfox.exe", "Waterfox", "waterfox",
        "palemoon.exe", "Pale Moon", "palemoon",
        "Chromium", "chromium",
        "epiphany", "falkon", "konqueror", "midori", "qutebrowser", "Ladybird",
        "Min", "Min.exe",
        "seamonkey.exe", "seamonkey",
        "k-meleon.exe",
        "netsurf",
        "Zen", "zen.exe", "zen-browser", "zen"
    };

    private static List<Entry> BlockedEntries
    {
        get
        {
            List<Entry> output = [];

            output.AddRange(State.Block);
            output.AddRange(State.Locks.SelectMany(e => e.Entries));
            output.AddRange(State.Schedules.Where(e => e.Active).SelectMany(e => e.Entries));

            output = output.ResolveLists(name => State.Lists.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

            return [.. output.Distinct()];
        }
    }

    // Replace process access in tests; never enumerate or kill real processes there
    internal static Func<(Func<string> Name, Action Kill, Action Dispose)[]> ProcessSnapshot = () =>
        [.. Process.GetProcesses().Select(process => ((Func<string>)(() => process.ProcessName),
            (Action)(() => process.Kill()), (Action)process.Dispose))];

    internal static string[] PreviousWebsites { get; set; } = [];

    static Blocker()
    {
        List<string> urls = [];
        string[] lines;

        try
        {
            lines = File.ReadAllLines(Platform.HostsPath);
        }
        catch
        {
            lines = [];
        }

        foreach (var line in lines)
        {
            if (line.StartsWith(REDIRECT))
                urls.Add(line[REDIRECT.Length..].SanitizeUrl());
        }

        PreviousWebsites = [.. urls.Distinct()];
    }


    public static Task UpdateAsync()
    {
        // Snapshot blocking state and processes
        var entries = BlockedEntries;
        var processes = ProcessSnapshot();

        // Apply blocking
        try
        {
            CloseApps(processes, entries);
            UpdateWebsites(entries, processes);
        }
        finally
        {
            foreach (var (Name, Kill, Dispose) in processes)
            {
                try
                {
                    Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void UpdateWebsites(List<Entry> entries,
        (Func<string> Name, Action Kill, Action Dispose)[] processes)
    {
        // Skip if no changes detected
        var blockedWebsites = entries.Where(e => e.Type == EntryType.Website).Select(e => e.Name).ToArray();
        if (blockedWebsites.SequenceEqual(PreviousWebsites)) return;

        // Close browsers if necessary
        foreach (string entry in blockedWebsites)
        {
            if (PreviousWebsites.Contains(entry)) continue;

            KillProcesses(processes, _browsers);
            break;
        }

        // Write new URLs
        try
        {
            WriteHostsFile(blockedWebsites);

            // Flush DNS
            _ = Platform.FlushDns.Run();
            PreviousWebsites = blockedWebsites;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error when writing to hosts file: {e}");
        }
    }

    private static void WriteHostsFile(string[] blockedWebsites)
    {
        var contents = new StringBuilder();
        contents.AppendLine(Config.Get<string>(nameof(ConfigSchema.hosts)));
        contents.AppendLine();
        contents.AppendLine("# FreeBlock blocked URLs");
        contents.AppendLine($"{REDIRECT} use-application-dns.net");

        foreach (string url in blockedWebsites)
        {
            contents.AppendLine($"{REDIRECT} {url}");
            contents.AppendLine($"{REDIRECT} www.{url}");
        }

        WriteHostsFile(contents.ToString());
    }

    internal static void WriteHostsFile(string contents)
    {
        var hostsPath = Platform.HostsPath;
        var directory = Path.GetDirectoryName(hostsPath) ?? throw new InvalidOperationException($"Cannot resolve directory of {hostsPath}");
        var tempPath = Path.Join(directory, $".freeblock-hosts-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, contents);
            File.Move(tempPath, hostsPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);

            throw;
        }
    }

    public static void CloseBrowsers() => WithProcesses(processes => KillProcesses(processes, _browsers));

    public static void CloseApps() => WithProcesses(processes => CloseApps(processes, BlockedEntries));

    private static void WithProcesses(Action<(Func<string> Name, Action Kill, Action Dispose)[]> action)
    {
        // Run against one process snapshot
        var processes = ProcessSnapshot();

        try
        {
            action(processes);
        }
        finally
        {
            foreach (var (Name, Kill, Dispose) in processes)
            {
                try
                {
                    Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                }
            }
        }
    }

    private static void CloseApps((Func<string> Name, Action Kill, Action Dispose)[] processes, List<Entry> entries)
    {
        // Find blocked apps
        var blockedApps = new HashSet<string>(entries
            .Where(e => e.Type == EntryType.App)
            .Select(e => e.Name),
            StringComparer.OrdinalIgnoreCase);

        // Close apps and notify
        var names = KillProcesses(processes, blockedApps);
        if (names.Length == 0) return;

        NotificationManager.Notify($"App{(names.Length == 1 ? "" : "s")} closed by FreeBlock", string.Join(", ", names));
    }

    private static string[] KillProcesses((Func<string> Name, Action Kill, Action Dispose)[] processes, HashSet<string> names)
    {
        HashSet<string> closed = new(StringComparer.OrdinalIgnoreCase);

        // Close matching processes
        foreach (var (Name, Kill, Dispose) in processes)
        {
            string? name = null;

            try
            {
                name = Name();
                if (!names.Contains(name)) continue;

                Kill();
                closed.Add(name);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Could not inspect/kill process '{name ?? "unknown"}': {e.Message}");
            }
        }

        return [.. closed];
    }
}
