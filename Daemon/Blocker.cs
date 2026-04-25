using System.Diagnostics;
using SDK;

namespace Daemon;

public static class Blocker
{

    private const string REDIRECT = "0.0.0.0";

    // TODO: Firefox
    private static readonly string[] BROWSERS = [
        "chrome.exe", "Google Chrome", "google-chrome", "chrome",
        //"firefox.exe", "firefox", "firefox-bin",
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
            List<string> blockedUrls = [];

            foreach (var list in State.BlockLists.Where(e => e.Active))
                blockedUrls.AddRange(list.UrlList);

            return blockedUrls.Distinct().ToArray();
        }
    }

    private static string[] _previousBlockedUrls;

    static Blocker()
    {
        List<string> urls = [];
        var lines = File.ReadAllLines(Platform.HostsPath);

        foreach (var line in lines)
        {
            if (line.StartsWith(REDIRECT))
                urls.Add(line[REDIRECT.Length..].SanitizeUrl());
        }

        _previousBlockedUrls = urls.Distinct().ToArray();
    }


    public static async Task UpdateAsync()
    {
        // Skip if no changes detected
        if (BlockedUrls == _previousBlockedUrls) return;

        // Close browsers if necessary
        foreach (string url in BlockedUrls)
        {
            if (_previousBlockedUrls.Contains(url)) continue;

            CloseBrowsers();
            break;
        }

        // Write new URLs
        using StreamWriter file = new(Platform.HostsPath);
        file.WriteLine(Config.Get<string>(nameof(Config.DefaultValue.hosts)));

        file.WriteLine("\n# FreeBlock blocked URLs");
        file.WriteLine($"{REDIRECT} use-application-dns.net");

        foreach (string url in BlockedUrls)
        {
            file.WriteLine($"{REDIRECT} {url}");
            file.WriteLine($"{REDIRECT} www.{url}");
        }

        // Flush DNS
        _ = Platform.FlushDns.Run();
        _previousBlockedUrls = BlockedUrls;
    }

    public static void CloseBrowsers()
    {
        Process[] processes = Process.GetProcesses();
        Process[] browsers = processes.Where(e => BROWSERS.Contains(e.ProcessName)).ToArray();
        browsers.ToList().ForEach(e => e.Kill());
    }

}
