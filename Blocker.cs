using System.Diagnostics.CodeAnalysis;

namespace FreeBlock;

public static class Blocker
{
    
    [field: MaybeNull] private static string HostsPath
    {
        get
        {
            if (field != null) return field;
            
            if (OperatingSystem.IsLinux()) field = "/etc/hosts";
            else if (OperatingSystem.IsMacOS()) field = "/private/etc/hosts";
            else if (OperatingSystem.IsWindows()) field = "C:/Windows/System32/drivers/etc/hosts";
            else throw new PlatformNotSupportedException("Only Linux, macOS and Windows are supported");

            return field;
        }
    }

    public static void Block(BlockList blockList)
    {
        using StreamWriter file = File.AppendText(HostsPath);

        foreach (string url in blockList.urlList)
            file.WriteLine($"0.0.0.0 {url}");

        RefreshDns();
    }

    public static void Unblock(BlockList blockList)
    {
        string[] toMatch = blockList.urlList.Select(e => $"0.0.0.0 {e}").ToArray();
        var lines = File.ReadLines(HostsPath).ToArray();
        using StreamWriter file = new(HostsPath);
        
        foreach (var line in lines)
        {
            if (toMatch.Contains(line)) continue;
            file.WriteLine(line);
        }
        
        RefreshDns();
    }
    
    private static void RefreshDns()
    {
        if (OperatingSystem.IsLinux()) {
            Run("resolvectl", "flush-caches");    
        }
        else if (OperatingSystem.IsWindows()) {
            Run("ipconfig", "/flushdns");
        }
        else if (OperatingSystem.IsMacOS()) {
            Run("dscacheutil", "-flushcache");
            Run("killall", "-HUP mDNSResponder");
        }
        else throw new PlatformNotSupportedException("Only Linux, macOS and Windows are supported");
    }

    private static void Run(string command, string arguments) =>
        Task.Run(() => System.Diagnostics.Process.Start(command, arguments));

}