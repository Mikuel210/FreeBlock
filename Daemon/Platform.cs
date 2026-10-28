using System.Diagnostics;

namespace Daemon;

public static class Platform
{

    // Paths
    private static readonly string _userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static string _configDirectoryRelative = null!;

    public static string HostsPath { get; private set; } = null!;
    public static string ConfigDirectory => Path.Join(_userDirectory, _configDirectoryRelative);
    public static string ConfigFile => Path.Join(ConfigDirectory, "config.json");
    public static string StateFile => Path.Join(ConfigDirectory, "state.json");

    public static string BlockPath => Path.Join(ConfigDirectory, "block.freeblock");
    public static string ListsDirectory => Path.Join(ConfigDirectory, "lists");

    // Commands
    public static CommandAction FlushDns { get; private set; } = null!;
    public static CommandAction Uninstall { get; private set; } = null!;
    public static CommandAction RemovePreferences { get; private set; } = null!;
    public static Func<string[]> GetCurrentUsers { get; private set; } = null!;

    public static Action<string, string, string> SendNotification = null!;

    // Windows notifications
    // TODO: This doesn't work at all
#if OS_WINDOWS
        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSSendMessage(
            IntPtr hServer,
            int SessionId,
            string pTitle,
            int TitleLength,
            string pMessage,
            int MessageLength,
            int Style,
            int Timeout,
            out int pResponse,
            bool bWait
        );
#endif

    static Platform()
    {
        if (OperatingSystem.IsLinux()) ConfigureLinux();
        else if (OperatingSystem.IsMacOS()) ConfigureMacOS();
        else if (OperatingSystem.IsWindows()) ConfigureWindows();
        else throw new PlatformNotSupportedException();
    }

    private static void ConfigureLinux()
    {
        // Paths
        HostsPath = "/etc/hosts";
        _configDirectoryRelative = ".config/freeblock";

        // Commands
        FlushDns = new([new("resolvectl", "flush-caches"), new("systemd-resolve", "--flush-caches")]);
        GetCurrentUsers = GetLinuxUsers;

        static string[] GetLinuxUsers()
        {
            List<string> users = [];

            foreach (var directory in Directory.GetDirectories("/run/user"))
            {
                string uid = Path.GetFileName(directory);

                try
                {
                    users.Add(GetUserName(uid));
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"Could not resolve user for UID {uid}: {e.Message}");
                }
            }

            return [.. users];
        }

        static string GetUserName(string uid)
        {
            string name = GetUserNameFromPasswd(uid);
            if (!string.IsNullOrEmpty(name)) return name;

            throw new InvalidOperationException($"No user name for UID {uid}");
        }

        static string GetUserNameFromPasswd(string uid)
        {
            if (!int.TryParse(uid, out int uidValue)) return string.Empty;

            foreach (var line in File.ReadAllLines("/etc/passwd"))
            {
                var fields = line.Split(':');
                if (fields.Length < 3) continue;
                if (fields[2] == uid) return fields[0];
            }

            return string.Empty;
        }

        var sendNotification = new CommandAction([
            new (
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
            )
        ]);

        SendNotification = async (user, title, body) =>
        {
            var (exitCodes, output) = await sendNotification.Run(user, title, body);

            if (exitCodes.Any(code => code != 0))
                Console.Error.WriteLine($"Linux notification failed for user '{user}': {output.Trim()}");
        };

        Uninstall = new([
            new (
                "systemctl",
                "stop freeblock"
            ),
            new (
                "rm",
                "/usr/bin/freeblock " +
                "/usr/bin/freeblockd " +
                $"/etc/systemd/system/freeblock.service"
            )
        ]);

        RemovePreferences = new([
            new (
                "rm",
                $"-r \"{ConfigDirectory}\""
            )
        ]);
    }

    private static void ConfigureMacOS()
    {
        // Paths
        HostsPath = "/private/etc/hosts";
        _configDirectoryRelative = "Library/Preferences/FreeBlock";

        // Commands
        FlushDns = new([new("dscacheutil", "-flushcache"), new("killall", "-HUP mDNSResponder")]);
        var serviceFile = Path.Join(_userDirectory, "Library/LaunchDaemons/com.freeblock.daemon.plist");

        GetCurrentUsers = () =>
        {
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.FileName = "users";

            process.Start();
            process.WaitForExit();
            return process.StandardOutput.ReadToEnd().Split(" ", StringSplitOptions.RemoveEmptyEntries);
        };

        var sendNotification = new CommandAction([
            new(
                "sudo",
                "-u {0} \"osascript -e '" +
                "display notification \\\"{2}\\\" " +
                "with title \\\"FreeBlock\\\" " +
                "subtitle \\\"{1}\\\"'\""
            )
        ]);

        SendNotification = async (user, title, body) =>
        {
            var (exitCodes, output) = await sendNotification.Run(user, title, body);

            if (exitCodes.Any(code => code != 0))
                Console.Error.WriteLine($"macOS notification failed for user '{user}': {output.Trim()}");
        };

        Uninstall = new([
            new (
                "launchctl",
                $"bootout system \"{serviceFile}\" 2>/dev/null"
            ),
            new (
                "rm",
                "/usr/local/bin/freeblock " +
                "/usr/local/bin/freeblockd " +
                $"\"{serviceFile}\""
            )
        ]);

        RemovePreferences = new([
            new (
                "rm",
                $"-r \"{ConfigDirectory}\""
            )
        ]);
    }

    private static void ConfigureWindows()
    {
        // Paths
        HostsPath = "C:/Windows/System32/drivers/etc/hosts";
        _configDirectoryRelative = "AppData/Roaming/FreeBlock";

        // Commands
        FlushDns = new([new("ipconfig", "/flushdns")]);
        GetCurrentUsers = () => [];

        SendNotification = (_, title, body) =>
        {
#if OS_WINDOWS
                WTSSendMessage(
                    IntPtr.Zero, 1,
                    title, title.Length * 2,
                    body, body.Length * 2,
                    0, 0, out int _, false
                );
#endif
        };

        Uninstall = new([
            new (
                "del",
                "\"C:\\Program Files\\FreeBlock\\freeblock.exe\" " +
                "\"C:\\Program Files\\FreeBlock\\freeblockd.exe\" /f"
            ),
            new(
                "sc",
                "delete freeblockd"
            )
        ]);

        RemovePreferences = new([
            new (
                "rmdir",
                $"\"{ConfigDirectory}\" /s /q"
            )
        ]);
    }

}
