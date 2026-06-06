using System.Diagnostics;
using SDK;

namespace Daemon;

public static class Blocker
{

    private const string REDIRECT = "0.0.0.0";

    private static List<Entry> BlockedEntries
    {
        get
        {
            List<Entry> output = [];

            output.AddRange(State.Block);
            output.AddRange(State.Locks.SelectMany(e => e.Entries));
            output.AddRange(State.Schedules.Where(e => e.Active).SelectMany(e => e.Entries));

            output = output.ResolveLists(name => State.Lists.Single(e => e.Name == name));
            return [.. output.Distinct()];
        }
    }

    private static string[] _previousWebsites;

    static Blocker()
    {
        // Get previous blocked websites
        List<string> urls = [];
        var lines = File.ReadAllLines(Platform.HostsPath);

        foreach (var line in lines)
        {
            if (line.StartsWith(REDIRECT))
                urls.Add(line[REDIRECT.Length..].SanitizeUrl());
        }

        _previousWebsites = [.. urls.Distinct()];
    }


    public static async Task UpdateAsync()
    {
        await CloseApps();

        // Skip if no changes detected
        var blockedWebsites = BlockedEntries.Where(e => e.Type == EntryType.Website).Select(e => e.Name).ToArray();
        if (blockedWebsites == _previousWebsites) return;

        // Close browsers if necessary
        foreach (string entry in blockedWebsites)
        {
            if (_previousWebsites.Contains(entry)) continue;

            CloseBrowsers();
            break;
        }

        // Write new URLs
        using StreamWriter file = new(Platform.HostsPath);
        file.WriteLine(Config.Get<string>(nameof(Config.DefaultValue.hosts)));

        file.WriteLine("\n# FreeBlock blocked URLs");
        file.WriteLine($"{REDIRECT} use-application-dns.net");

        foreach (string url in blockedWebsites)
        {
            file.WriteLine($"{REDIRECT} {url}");
            file.WriteLine($"{REDIRECT} www.{url}");
        }

        // Flush DNS
        _ = Platform.FlushDns.Run();
        _previousWebsites = blockedWebsites;
    }

    public static void CloseBrowsers()
    {
        string[] browsers = [
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
            "epiphany",
            "falkon",
            "konqueror",
            "midori",
            "qutebrowser",
            "Ladybird",
            "Min", "Min.exe",
            "seamonkey.exe", "seamonkey",
            "k-meleon.exe",
            "netsurf",
            "Zen", "zen.exe", "zen-browser", "zen"
        ];

        Process.GetProcesses()
            .Where(e => browsers.Contains(e.ProcessName))
            .ToList().ForEach(e => e.Kill());
    }

    public static async Task CloseApps()
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
            await Platform.SendNotification.Run(user, title, body);
    }

}
