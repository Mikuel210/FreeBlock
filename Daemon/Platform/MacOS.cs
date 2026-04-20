namespace Daemon;

public class MacOS : IPlatform
{
    public string ConfigDirectory { get; } = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Preferences/FreeBlock");
    public string HostsPath { get; } = "/private/etc/hosts";
    public Dictionary<string, string> FlushDnsCommands { get; } = new() { { "dscacheutil", "-flushcache" }, { "killall", "-HUP mDNSResponder" } };
}
