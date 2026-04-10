using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace CLI;

public static class Blocker
{

    private static string[] BlockedUrls
    {
        get
        {
            List<string> blockedUrls = [];

            foreach (var list in Config.BlockLists.Where(e => e.Enabled))
                blockedUrls.AddRange(list.UrlList);

            return blockedUrls.Distinct().ToArray();
        }
    }

    private static readonly string[] BROWSERS = [
        "chrome.exe", "Google Chrome", "google-chrome", "chrome",
        "firefox.exe", "firefox",
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
        "netsurf"
    ];

    private const string REDIRECT = "0.0.0.0";

    public static void UpdateBlock()
    {
        using StreamWriter file = new(Config.HostsPath);
        file.WriteLine(Config.Get<string>("hosts"));

        file.WriteLine("\n# FreeBlock blocked URLs");
        file.WriteLine("0.0.0.0 use-application-dns.net");
        file.WriteLine("0.0.0.0 www.use-application-dns.net");

        foreach (string url in BlockedUrls)
        {
            if (url.StartsWith("www."))
            {
                file.WriteLine($"{REDIRECT} {url.Remove(0, 4)}");
                file.WriteLine($"{REDIRECT} {url}");
            }
            else
            {
                file.WriteLine($"{REDIRECT} {url}");
                file.WriteLine($"{REDIRECT} www.{url}");
            }
        }

        RefreshDns();
    }

    public static void CloseBrowsers()
    {
        Process[] processes = Process.GetProcesses();
        Process[] browsers = processes.Where(e => BROWSERS.Contains(e.ProcessName)).ToArray();
        browsers.ToList().ForEach(e => e.Kill());
    }

    private static void RefreshDns()
    {
        if (OperatingSystem.IsLinux())
        {
            Run("resolvectl", "flush-caches");
        }
        else if (OperatingSystem.IsWindows())
        {
            Run("ipconfig", "/flushdns");
        }
        else if (OperatingSystem.IsMacOS())
        {
            Run("dscacheutil", "-flushcache");
            Run("killall", "-HUP mDNSResponder");
        }
        else throw new PlatformNotSupportedException("Only Linux, macOS and Windows are supported");
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
