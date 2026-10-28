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

            output = output.ResolveLists(name => State.Lists.FirstOrDefault(e => e.Name == name));
            return [.. output.Distinct()];
        }
    }

    internal static string[] PreviousWebsites { get; set; } = [];

    static Blocker()
    {
        List<string> urls = [];
        string[] lines;

        try { lines = File.ReadAllLines(Platform.HostsPath); }
        catch { lines = []; }

        foreach (var line in lines)
        {
            if (line.StartsWith(REDIRECT))
                urls.Add(line[REDIRECT.Length..].SanitizeUrl());
        }

        PreviousWebsites = [.. urls.Distinct()];
    }


    public static async Task UpdateAsync()
    {
        CloseApps();

        // Skip if no changes detected
        var blockedWebsites = BlockedEntries.Where(e => e.Type == EntryType.Website).Select(e => e.Name).ToArray();
        if (blockedWebsites.SequenceEqual(PreviousWebsites)) return;

        // Close browsers if necessary
        foreach (string entry in blockedWebsites)
        {
            if (PreviousWebsites.Contains(entry)) continue;

            CloseBrowsers();
            break;
        }

        // Write new URLs
        try
        {
            WriteHostsFile();

            // Flush DNS
            _ = Platform.FlushDns.Run();
            PreviousWebsites = blockedWebsites;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error when writing to hosts file: {e}");
        }
    }

    private static void WriteHostsFile()
    {
        var blockedWebsites = BlockedEntries.Where(e => e.Type == EntryType.Website).Select(e => e.Name).ToArray();

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

    public static void CloseBrowsers()
    {
        foreach (var process in Process.GetProcesses().Where(e => _browsers.Contains(e.ProcessName)))
        {
            try
            {
                process.Kill();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error when killing process {process.ProcessName}: {e}");
            }
        }
    }

    public static void CloseApps()
    {
        var blockedApps = BlockedEntries.Where(e => e.Type == EntryType.App).Select(e => e.Name).ToArray();
        var apps = Process.GetProcesses().Where(e => blockedApps.Contains(e.ProcessName)).ToList();

        if (apps.Count == 0) return;
        apps.ForEach(e => e.Kill());

        // Send notification
        var appNames = apps.Select(e => e.ProcessName).Distinct().ToArray();
        string title = $"App{(appNames.Length == 1 ? "" : "s")} closed by FreeBlock";
        string body = string.Join(", ", appNames);

        foreach (var user in Platform.GetCurrentUsers())
            Platform.SendNotification(user, title, body);
    }

}
