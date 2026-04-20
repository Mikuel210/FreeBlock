using System.Diagnostics;

namespace Daemon;

public static class Blocker
{

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

    private static string[] BlockedUrls
    {
        get
        {
            List<string> blockedUrls = ["use-application-dns.net"];

            foreach (var list in Config.BlockLists.Where(e => e.Active))
                blockedUrls.AddRange(list.UrlList);

            return blockedUrls.Distinct().ToArray();
        }
    }

    private const string REDIRECT = "0.0.0.0";
    private static string[] _previousBlockedUrls = [];

    public static void UpdateBlock(bool force = false)
    {
        // Update schedules
        UpdateSchedules();

        // Skip if no changes detected
        if (BlockedUrls == _previousBlockedUrls && !force) return;

        // Close browsers if necessary
        foreach (string url in BlockedUrls)
        {
            if (_previousBlockedUrls.Contains(url)) continue;
            CloseBrowsers();
        }

        // Write new URLs
        using StreamWriter file = new(Config.Platform.HostsPath);
        file.WriteLine(Config.Get<string>("hosts"));
        file.WriteLine("\n# FreeBlock blocked URLs");

        foreach (string url in BlockedUrls)
        {
            file.WriteLine($"{REDIRECT} {url}");
            file.WriteLine($"{REDIRECT} www.{url}");
        }

        // Refresh DNS
        RefreshDns();
        _previousBlockedUrls = BlockedUrls;
    }

    public static void CloseBrowsers()
    {
        Process[] processes = Process.GetProcesses();
        Process[] browsers = processes.Where(e => BROWSERS.Contains(e.ProcessName)).ToArray();
        browsers.ToList().ForEach(e => e.Kill());
    }

    private static void UpdateSchedules()
    {
        foreach (var list in Config.BlockLists)
        {
            list.Scheduled = Config.Schedules
                .FirstOrDefault(e =>
                {
                    var containsList = e.BlockLists.Select(e => e.Name).Contains(list.Name);
                    return containsList && e.Active;
                }) != null;
        }
    }

    private static void RefreshDns()
    {
        foreach (var commandArguments in Config.Platform.FlushDnsCommands)
            Run(commandArguments.Key, commandArguments.Value);
    }

    private static void Run(string command, string arguments)
    {
        Task.Run(() =>
        {
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.FileName = command;
            process.StartInfo.Arguments = arguments;
            process.Start();
        });
    }

}
