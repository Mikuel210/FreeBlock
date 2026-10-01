using System.Diagnostics;
namespace Daemon;

// Side-effecting surfaces the daemon depends on, so they can be replaced in tests.
// The real implementations are selected in Platform at startup

public interface IPlatform
{
    string HostsPath { get; }
    string ConfigDirectory { get; }
    string ConfigFile { get; }
    string StateFile { get; }
    string ListsDirectory { get; }
    CommandAction FlushDns { get; }
    CommandAction Uninstall { get; }
    CommandAction RemovePreferences { get; }
    Func<string[]> GetCurrentUsers { get; }
    Action<string, string, string> SendNotification { get; }
}

public interface IProcessService
{
    Process[] GetProcesses();
    void Kill(Process process);
    string GetName(Process process);
}

// The production process service: real Process enumeration and kills
public sealed class RealProcessService : IProcessService
{
    public static readonly RealProcessService Instance = new();

    public Process[] GetProcesses() => Process.GetProcesses();

    public void Kill(Process process) => process.Kill();

    public string GetName(Process process) => process.ProcessName;
}
