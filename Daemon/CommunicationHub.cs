using Microsoft.AspNetCore.SignalR;
using Core;
using Lock = Core.Lock;
using System.Text.Json;
using System.Reflection;

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
        // B5: Update in-memory state first, then persist both files (state is source of truth)
        State.Lists.Add(list);
        State.Save();
        
        WriteFile(Path.Join(Platform.ListsDirectory, $"{list.Name}.freeblock"), contents);
    }

    public async Task EditListAsync(List list, string contents)
    {
        // B5: Update in-memory state first, then persist both files
        var localList = GetLocalList(list);
        localList.Entries = list.Entries;
        
        await ApplyChanges();  // This calls State.Save()
        
        WriteFile(Path.Join(Platform.ListsDirectory, $"{list.Name}.freeblock"), contents);
    }

    public async Task RenameListAsync(List list, string newName)
    {
        // Move list file
        string oldPath = Path.Join(Platform.ListsDirectory, $"{list.Name}.freeblock");
        string newPath = Path.Join(Platform.ListsDirectory, $"{newName}.freeblock");

        if (Path.Exists(oldPath))
            File.Move(oldPath, newPath);

        // Change all references
        var entries = State.Block
            .Concat(State.Locks.SelectMany(e => e.Entries))
            .Concat(State.Lists.SelectMany(e => e.Entries))
            .Concat(State.Schedules.SelectMany(e => e.Entries));

        foreach (var entry in entries)
        {
            if (entry.Type == EntryType.List && entry.Name == list.Name)
                entry.Name = newName;
        }

        var paths = Directory.GetFiles(Platform.ListsDirectory);

        foreach (var path in paths)
        {
            var lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == $"@{list.Name}")
                    lines[i] = $"@{newName}";
            }

            WriteFile(path, string.Join('\n', lines));  // C2: was string.Join('\n', path)
        }

        // Change name
        var localList = GetLocalList(list);
        localList.Name = newName;

        State.Save();
    }

    public async Task RemoveListAsync(List list)
    {
        var entry = new Entry(EntryType.List, list.Name);

        // A lock referencing this list would resolve to a dangling entry on the next tick (B6).
        var referencingLock = State.Locks.FirstOrDefault(e => e.Entries.Contains(entry));
        if (referencingLock != null)
            throw new InvalidOperationException($"The list is being used by a lock: {referencingLock.Name}");

        File.Delete(Path.Join(Platform.ListsDirectory, $"{list.Name}.freeblock"));

        var localList = GetLocalList(list);
        State.Lists.Remove(localList);

        await ApplyChanges();
    }

    public Task<string> GetListContentsAsync(List list)
        => File.ReadAllTextAsync(Path.Join(Platform.ListsDirectory, $"{list.Name}.freeblock"));

    // Locks

    public async Task AddLockAsync(Lock @lock)
    {
        State.Locks.Add(@lock);
        await ApplyChanges();
    }

    public async Task EditLockAsync(Lock @lock)
    {
        var localLock = GetLocalLock(@lock);
        localLock.Entries = @lock.Entries;
        localLock.UnlockTime = @lock.UnlockTime;

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
        State.Schedules.Add(schedule);
        await ApplyChanges();
    }

    public async Task EditScheduleAsync(Schedule schedule)
    {
        var localSchedule = GetLocalSchedule(schedule);

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

    public Task<object?> GetConfig(string fieldName)
    {
        var field = typeof(Config.DefaultValue).GetField(fieldName)
            ?? throw new ArgumentException($"Unknown configuration key: {fieldName}", nameof(fieldName));

        var type = field.FieldType;
        return Task.FromResult(Config.Get(fieldName, type));
    }

    public async Task<Dictionary<string, object?>> GetBatchConfig(string[] fieldName)
    {
        Dictionary<string, object?> output = [];

        foreach (var key in fieldName)
            output.Add(key, await GetConfig(key));

        return output;
    }

    public Task SetConfig(string fieldName, JsonElement value)
    {
        var field = typeof(Config.DefaultValue).GetField(fieldName)
            ?? throw new ArgumentException($"Unknown configuration key: {fieldName}", nameof(fieldName));

        var typedValue = value.Deserialize(field.FieldType);
        if (typedValue == null)
            throw new ArgumentException($"Invalid value for {fieldName}", nameof(value));

        // B7: Validate config values before applying them
        if (typedValue is TimeSpan ts)
        {
            if (ts < TimeSpan.Zero)
                throw new ArgumentException($"{fieldName} cannot be negative", nameof(value));
            
            if (fieldName == nameof(Config.DefaultValue.defaultWarningTime) && ts > TimeSpan.FromHours(24))
                throw new ArgumentException($"{fieldName} cannot exceed 24 hours", nameof(value));
        }

        Config.Set(fieldName, typedValue);
        Config.Save();

        return Task.CompletedTask;
    }


    // Uninstall

    public async Task UninstallAsync()
    {
        // B9: Use atomic write to avoid leaving a corrupted hosts file if process dies mid-write
        var hostsBackup = Config.Get<string>(nameof(Config.DefaultValue.hosts));
        if (hostsBackup is null)
        {
            throw new HubException("Uninstall failed: no hosts backup found in config. Daemon remains running.");
        }
        
        try
        {
            Blocker.WriteHostsFile(hostsBackup);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to restore hosts file during uninstall: {e}");
            throw new HubException("Uninstall failed: could not restore hosts file. Daemon remains running.");
        }
        
        await Platform.Uninstall.Run();
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

    private static void WriteFile(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

}
