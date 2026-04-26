namespace Daemon;

public static class Platform
{

    // Paths
    private static readonly string _configDirectoryRelative;
    private static readonly string _userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string HostsPath { get; }
    public static string ConfigDirectory => Path.Join(_userDirectory, _configDirectoryRelative);
    public static string ConfigFile => Path.Join(ConfigDirectory, "config.json");
    public static string StateFile => Path.Join(ConfigDirectory, "state.json");

    // Commands
    public static CommandAction FlushDns { get; }
    public static CommandAction SendNotification { get; }
    public static Func<string[]> GetCurrentUsers { get; }

    static Platform()
    {
        if (OperatingSystem.IsLinux())
        {
            // Paths
            HostsPath = "/etc/hosts";
            _configDirectoryRelative = ".config/freeblock";

            // Commands
            FlushDns = new(new() { { "resolvectl", "flush-caches" }, { "systemd-resolve", "--flush-caches" } });
            GetCurrentUsers = () => Directory.GetDirectories("/run/user").Select(Path.GetFileName).ToArray()!;

            SendNotification = new(
                new() {
                    {
                        "runuser",
                        "-l {0} -c \"dbus-send " +
                        "--session --print-reply " +
                        "--dest=org.freedesktop.Notifications " +
                        "/org/freedesktop/Notifications " +
                        "org.freedesktop.Notifications.Notify " +
                        "string:\\\"FreeBlock\\\" " +
                        "uint32:0 " +
                        "string:\\\"dialog-information\\\" " +
                        "string:\\\"{1}\\\" " +
                        "string:\\\"{2}\\\" " +
                        "array:string:\\\"\\\" " +
                        "dict:string:variant:string:\\\"urgency\\\",variant:byte:2 " +
                        "int32:-1\""
                    }
                }
            );
        }

        else if (OperatingSystem.IsMacOS())
        {
            // Paths
            HostsPath = "/private/etc/hosts";
            _configDirectoryRelative = "Library/Preferences/FreeBlock";

            // Commands
            FlushDns = new(new() { { "dscacheutil", "-flushcache" }, { "killall", "-HUP mDNSResponder" } });
            GetCurrentUsers = () => [];
            SendNotification = new([]);
        }

        else if (OperatingSystem.IsWindows())
        {
            // Paths
            HostsPath = "C:/Windows/System32/drivers/etc/hosts";
            _configDirectoryRelative = "AppData/Roaming/FreeBlock";

            // Commands
            FlushDns = new(new() { { "ipconfig", "/flushdns" } });
            GetCurrentUsers = () => [];
            SendNotification = new([]);
        }

        else throw new PlatformNotSupportedException();
    }

}
