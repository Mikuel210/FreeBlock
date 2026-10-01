using System.Diagnostics;
using System.Text;
using Core;

namespace Daemon;

public static class Blocker
{

    private const string REDIRECT = "0.0.0.0";

    // Test seam. The daemon points this at the real Platform at startup; tests supply a fake so
    // Blocker can be exercised without touching /etc/hosts or spawning processes (§6).
    internal static IPlatform Platform = Daemon.Platform.Default;
    internal static IProcessService ProcessService = RealProcessService.Instance;

    // Process names are matched with OrdinalIgnoreCase: the same browser reports as "chrome" on Linux,
    // "Google Chrome" on macOS and "chrome.exe" on Windows. Built once, not per tick (P2).
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

            // A list referenced by a lock/schedule/block may have been removed while still referenced.
            // FirstOrDefault degrades to "no entries" instead of throwing and killing the daemon tick (B6).
            output = output.ResolveLists(name => State.Lists.FirstOrDefault(e => e.Name == name));
            return [.. output.Distinct()];
        }
    }

    internal static string[] PreviousWebsites { get; set; } = [];

    static Blocker()
    {
        // Seed from whatever the hosts file already contains, so a daemon restart does not
        // re-close browsers for sites it already blocked (B1).
        List<string> urls = [];
        string[] lines;

        try { lines = File.ReadAllLines(Daemon.Platform.HostsPath); }
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
        // Enumerate processes once per tick: both kill passes need the same snapshot and
        // GetProcesses is the expensive part (P2).
        var processes = ProcessService.GetProcesses();

        CloseApps(processes);

        // Skip if no changes detected. BlockedEntries re-allocates on every access, so the old
        // reference comparison was always false and the daemon rewrote /etc/hosts every second (B1).
        var blockedWebsites = BlockedEntries.Where(e => e.Type == EntryType.Website).Select(e => e.Name).ToArray();
        if (blockedWebsites.SequenceEqual(PreviousWebsites)) return;

        // Close browsers if necessary
        foreach (string entry in blockedWebsites)
        {
            if (PreviousWebsites.Contains(entry)) continue;

            CloseBrowsers(processes);
            break;
        }

        // Write new URLs
        try
        {
            WriteHostsFile();

            // Flush DNS. A marker is harmless on a real host (the command simply does not exist) and
            // lets the tests observe that Blocker flushed at all.
            _ = Platform.FlushDns.Run();
            PreviousWebsites = blockedWebsites;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error when writing to hosts file: {e}");
        }
    }

    /// <summary>
    /// Rewrites the hosts file atomically: write to a temp file in the same directory (same filesystem)
    /// and move it over the target. A non-atomic WriteAllText leaves a truncated /etc/hosts behind for
    /// any process resolving a hostname during the write window (P3).
    /// </summary>
    private static void WriteHostsFile()
    {
        var blockedWebsites = BlockedEntries.Where(e => e.Type == EntryType.Website).Select(e => e.Name).ToArray();

        var contents = new StringBuilder();
        contents.AppendLine(Config.Get<string>(nameof(Config.DefaultValue.hosts)));
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
            // Never leave the temp file behind on a failed write.
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }

    /// <summary>Each kill is individually guarded: one dead or inaccessible process must not abort
    /// the loop and leave the other browsers running (D8).</summary>
    public static void CloseBrowsers(Process[] processes) => CloseBrowsers(processes, ProcessService);

    /// <summary>Kills browsers through <paramref name="processService"/> so tests can observe the calls
    /// instead of killing real processes (D8).</summary>
    public static void CloseBrowsers(Process[] processes, IProcessService processService)
    {
        foreach (var process in processes.Where(e => processService.GetName(e) is { } name && _browsers.Contains(name)))
        {
            try
            {
                processService.Kill(process);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error when killing process {processService.GetName(process)}: {e}");
            }
        }
    }

    /// <summary>Same per-process guard as <see cref="CloseBrowsers(Process[], IProcessService)"/> (D8).</summary>
    public static void CloseApps(Process[] processes, IProcessService? processService = null)
    {
        processService ??= ProcessService;

        var blockedApps = BlockedEntries.Where(e => e.Type == EntryType.App).Select(e => e.Name).ToArray();
        
        // Apps are matched case-insensitively (B2): macOS reports "Steam", user types "+steam"
        var blockedAppsSet = new HashSet<string>(blockedApps, StringComparer.OrdinalIgnoreCase);
        var apps = processes.Where(e => processService.GetName(e) is { } name && blockedAppsSet.Contains(name)).ToList();

        if (apps.Count == 0) return;

        foreach (var process in apps)
        {
            try
            {
                processService.Kill(process);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error when killing process {processService.GetName(process)}: {e}");
            }
        }

        // Send notification
        var appNames = apps.Select(e => processService.GetName(e)).Distinct().ToArray();
        string title = $"App{(appNames.Length == 1 ? "" : "s")} closed by FreeBlock";
        string body = string.Join(", ", appNames);

        NotificationManager.Notify(title, body);  // D12: Use shared notification helper
    }

}
