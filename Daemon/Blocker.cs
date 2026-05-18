using System.Diagnostics;
using SDK;

namespace Daemon;

public static class Blocker
{

    private const string REDIRECT = "0.0.0.0";

    private static readonly string[] BROWSERS = [
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

    private static string[] BlockedEntries
    {
        get
        {
            List<string> blockedEntries = [];

            foreach (var list in State.BlockLists.Where(e => e.Active))
                blockedEntries.AddRange(list.Entries);

            return blockedEntries.Distinct().ToArray();
        }
    }

    private static string[] _previousBlockedEntries;

    static Blocker()
    {
        List<string> urls = [];
        var lines = File.ReadAllLines(Platform.HostsPath);

        foreach (var line in lines)
        {
            if (line.StartsWith(REDIRECT))
                urls.Add(line[REDIRECT.Length..].SanitizeUrl());
        }

        _previousBlockedEntries = urls.Distinct().ToArray();
    }


    public static async Task UpdateAsync()
    {
        await CloseApps();

        // Skip if no changes detected
        if (BlockedEntries == _previousBlockedEntries) return;

        // Close browsers if necessary
        foreach (string entry in BlockedEntries)
        {
            if (_previousBlockedEntries.Contains(entry)) continue;

            CloseBrowsers();
            break;
        }

        // Write new URLs
        using StreamWriter file = new(Platform.HostsPath);
        file.WriteLine(Config.Get<string>(nameof(Config.DefaultValue.hosts)));

        file.WriteLine("\n# FreeBlock blocked URLs");
        file.WriteLine($"{REDIRECT} use-application-dns.net");

        foreach (string url in BlockedEntries)
        {
            file.WriteLine($"{REDIRECT} {url}");
            file.WriteLine($"{REDIRECT} www.{url}");
        }

        // Flush DNS
        _ = Platform.FlushDns.Run();
        _previousBlockedEntries = BlockedEntries;
    }

    public static void CloseBrowsers()
    {
        Process.GetProcesses()
            .Where(e => BROWSERS.Contains(e.ProcessName))
            .ToList().ForEach(e => e.Kill());
    }

    public static async Task CloseApps()
    {
        var apps = Process.GetProcesses().Where(e => BlockedEntries.Contains(e.ProcessName)).ToList();
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
