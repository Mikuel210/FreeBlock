namespace Daemon;

public class Windows : IPlatform
{
    public string ConfigDirectory { get; } = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData/Roaming/FreeBlock");
    public string HostsPath { get; } = "C:/Windows/System32/drivers/etc/hosts";
    public Dictionary<string, string> FlushDnsCommands { get; } = new() { { "ipconfig", "/flushdns" } };
}
