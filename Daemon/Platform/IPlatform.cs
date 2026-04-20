namespace Daemon;

public interface IPlatform
{
    string ConfigDirectory { get; }
    string ConfigFile => Path.Join(ConfigDirectory, "config.json");
    string HostsPath { get; }
    Dictionary<string, string> FlushDnsCommands { get; }
}
