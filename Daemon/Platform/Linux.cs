namespace Daemon;

public class Linux : IPlatform
{
    public string ConfigDirectory { get; } = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/freeblock");
    public string HostsPath { get; } = "/etc/hosts";
    public Dictionary<string, string> FlushDnsCommands { get; } = new() { { "resolvectl", "flush-caches" } };
}
