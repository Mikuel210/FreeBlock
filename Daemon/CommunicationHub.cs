using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using Core;
using Lock = Core.Lock;

namespace Daemon;

public class CommunicationHub : Hub
{

    #region Actions

    // Blocking

    public async Task BlockAsync(List<Entry> entries)
    {
        State.Block.AddRange(entries.Where(e => !State.Block.Contains(e)));
        await ApplyChanges();
    }

    public async Task UnblockAsync(List<Entry> entries)
    {
        State.Block = [.. State.Block.Where(e => !entries.Contains(e))];
        await ApplyChanges();
    }


    // Lists

    public async Task AddListAsync(List list, string contents)
    {
        var path = GetListPath(list.Name);

        if (GetFromName(State.Lists, list.Name) != null)
            throw new HubException("A list with this name already exists.");

        WriteFile(path, contents);
        State.Lists.Add(list);
        State.Save();
    }

    public async Task EditListAsync(List list, string contents)
    {
        var localList = GetLocalList(list);
        var path = GetListPath(localList.Name);
        localList.Entries = list.Entries;

        await ApplyChanges();
        WriteFile(path, contents);
    }

    public async Task RenameListAsync(List list, string newName)
    {
        // Move list file
        var localList = GetLocalList(list);
        string oldPath = GetListPath(localList.Name);
        string newPath = GetListPath(newName);

        if (GetFromName(State.Lists, newName) is { } existing && existing != localList)
            throw new HubException("A list with this name already exists.");

        if (Path.Exists(oldPath))
            File.Move(oldPath, newPath);

        // Change all references
        var entries = State.Block
            .Concat(State.Locks.SelectMany(e => e.Entries))
            .Concat(State.Lists.SelectMany(e => e.Entries))
            .Concat(State.Schedules.SelectMany(e => e.Entries));

        foreach (var entry in entries)
        {
            if (entry.Type == EntryType.List && string.Equals(entry.Name, localList.Name, StringComparison.OrdinalIgnoreCase))
                entry.Name = newName;
        }

        var paths = Directory.GetFiles(Platform.ListsDirectory, "*.freeblock");

        foreach (var path in paths)
        {
            var lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.Equals(lines[i].Trim(), $"@{localList.Name}", StringComparison.OrdinalIgnoreCase))
                    lines[i] = $"@{newName}";
            }

            WriteFile(GetListPath(Path.GetFileNameWithoutExtension(path)), string.Join('\n', lines));
        }

        // Change name
        localList.Name = newName;
        State.Save();
    }

    public async Task RemoveListAsync(List list)
    {
        var localList = GetLocalList(list);
        var path = GetListPath(localList.Name);

        File.Delete(path);
        State.Lists.Remove(localList);
        await ApplyChanges();
    }

    public Task<string> GetListContentsAsync(List list)
        => File.ReadAllTextAsync(GetListPath(GetFromName(State.Lists, list.Name)?.Name ?? list.Name));

    // Locks

    public async Task AddLockAsync(string name, List<Entry> entries, TimeSpan duration)
    {
        // Validate the lock
        if (duration < TimeSpan.Zero || duration > TimeSpan.FromDays(365))
            throw new HubException("Lock duration must be between zero and one year.");

        if (GetFromName(State.Locks, name) != null)
            throw new HubException("A lock with this name already exists.");

        // Apply the lock
        State.Locks.Add(new Lock(name, entries, DateTime.Now.Add(duration)));
        await ApplyChanges();
    }

    public async Task ExtendLockAsync(string name, List<Entry> addedEntries, TimeSpan extension)
    {
        // Validate the extension
        if (extension < TimeSpan.Zero || extension > TimeSpan.FromDays(365))
            throw new HubException("Lock extension must be between zero and one year.");

        var local = GetFromName(State.Locks, name) ?? throw new HubException("Lock not found.");
        var unlock = local.UnlockTime.Add(extension);

        // Apply changes to the live lock
        local.Entries = [.. local.Entries.Concat(addedEntries).Distinct()];
        local.UnlockTime = unlock;
        await ApplyChanges();
    }

    public async Task RenameLockAsync(Lock @lock, string newName)
    {
        var localLock = GetLocalLock(@lock);
        localLock.Name = newName;

        State.Save();
    }


    // Schedules

    public async Task AddScheduleAsync(Schedule schedule)
    {
        Config.ValidateWarningTime(schedule.Options.WarningTime);
        State.Schedules.Add(schedule);
        await ApplyChanges();
    }

    public async Task EditScheduleAsync(Schedule schedule)
    {
        Config.ValidateWarningTime(schedule.Options.WarningTime);
        var localSchedule = GetLocalSchedule(schedule);

        if (!Schedule.PreservesProtectedWindow(localSchedule, schedule, DateTime.Now))
            throw new HubException("Active or warned schedule entries and blocking window cannot be reduced.");

        localSchedule.Entries = schedule.Entries;
        localSchedule.StartTime = schedule.StartTime;
        localSchedule.EndTime = schedule.EndTime;
        localSchedule.Days = schedule.Days;
        localSchedule.Options = schedule.Options;

        await ApplyChanges();
    }

    public async Task RenameScheduleAsync(Schedule schedule, string newName)
    {
        var localSchedule = GetLocalSchedule(schedule);
        localSchedule.Name = newName;

        State.Save();
    }

    public async Task RemoveScheduleAsync(Schedule schedule)
    {
        var localSchedule = GetLocalSchedule(schedule);
        var requested = localSchedule.RemovalRequestTime;
        var now = DateTime.Now;

        bool removalWindow = requested.HasValue && now >= requested.Value.AddDays(1) && now < requested.Value.AddDays(2);

        if ((localSchedule.ActiveAt(now) || localSchedule.InWarningPeriodAt(now)) && !removalWindow)
            throw new HubException("Active or warned schedules require a removal request and the 24–48 hour removal window.");

        State.Schedules.Remove(localSchedule);
        State.Save();
    }

    public async Task RequestScheduleRemovalAsync(Schedule schedule)
    {
        var localSchedule = GetLocalSchedule(schedule);
        localSchedule.RemovalRequestTime = DateTime.Now;

        State.Save();
    }


    // Config

    public async Task<ConfigSchema> GetConfigAsync()
        => Config.GetAll();

    public async Task SetConfigAsync(ConfigSchema values)
    {
        lock (ConfigMutationLock)
        {
            Config.SetAll(values);
            Config.Save();
        }
    }


    public Task SetConfigValueAsync(string fieldName, JsonElement value)
    {
        lock (ConfigMutationLock) Config.SetPublicValue(fieldName, value);
        return Task.CompletedTask;
    }

    private static readonly System.Threading.Lock ConfigMutationLock = new();

    // Uninstall

    public async Task UninstallAsync()
    {
        var hostsBackup = Config.Get<string>(nameof(ConfigSchema.hosts));

        if (string.IsNullOrEmpty(hostsBackup))
            throw new HubException("Uninstall failed: no hosts backup is available");

        try
        {
            if (hostsBackup != null)
                Blocker.WriteHostsFile(hostsBackup);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to restore hosts file during uninstall: {e}");
            throw new HubException("Uninstall failed: could not restore hosts file");
        }

        var (exitCodes, output) = await Platform.Uninstall.Run();

        if (exitCodes.Any(code => code != 0))
            throw new HubException($"Uninstall failed: {output.Trim()}");
    }

    public async Task RemovePreferencesAsync() => await Platform.RemovePreferences.Run();

    #endregion

    #region Get State

    public async Task<StateSnapshot> GetSnapshotAsync()
        => State.GetSnapshot();

    public async Task<List<Entry>> GetBlockAsync()
        => State.Block;

    public async Task<List<Lock>> GetLocksAsync()
        => State.Locks;

    public async Task<List<List>> GetListsAsync()
        => State.Lists;

    public async Task<List<Schedule>> GetSchedulesAsync()
        => State.Schedules;


    private static T? GetFromName<T>(List<T> list, string name) where T : IName
        => list.FirstOrDefault(e => e.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

    public async Task<List?> GetListFromNameAsync(string name)
        => GetFromName(State.Lists, name);

    public async Task<Lock?> GetLockFromNameAsync(string name)
        => GetFromName(State.Locks, name);

    public async Task<Schedule?> GetScheduleFromNameAsync(string name)
        => GetFromName(State.Schedules, name);


    private static T GetLocal<T>(List<T> list, T client) where T : IName
        => list.First(e => e.Name.Equals(client.Name, StringComparison.InvariantCultureIgnoreCase));

    private static List GetLocalList(List clientList)
        => GetLocal(State.Lists, clientList);

    private static Lock GetLocalLock(Lock clientLock)
        => GetLocal(State.Locks, clientLock);

    private static Schedule GetLocalSchedule(Schedule clientSchedule)
        => GetLocal(State.Schedules, clientSchedule);

    #endregion

    private static async Task ApplyChanges()
    {
        await Blocker.UpdateAsync();
        State.Save();
    }

    private static string GetListPath(string name)
    {
        // Validate the list name
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128 ||
            name.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
        {
            throw new HubException("Invalid list name: use letters, digits, '-' or '_'.");
        }

        // Check the lists directory
        var directory = new DirectoryInfo(Platform.ListsDirectory);

        if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new HubException("The lists directory must not be a symbolic link.");

        // Check the list file
        var path = Path.Combine(directory.FullName, name + ".freeblock");
        var file = new FileInfo(path);

        if (file.LinkTarget != null || (file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0))
            throw new HubException("List files must not be symbolic links.");

        return path;
    }

    private static void WriteFile(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

}
