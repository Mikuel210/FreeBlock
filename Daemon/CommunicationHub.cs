using Microsoft.AspNetCore.SignalR;
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
        WriteFile(Path.Join(Platform.ListsDirectory, $"{list.Name}.freeblock"), contents);
        
        State.Lists.Add(list);
        State.Save();
    }

    public async Task EditListAsync(List list, string contents)
    {
        WriteFile(Path.Join(Platform.ListsDirectory, $"{list.Name}.freeblock"), contents);

        var localList = GetLocalList(list);
        localList.Entries = list.Entries;

        await ApplyChanges();
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

            WriteFile(path, string.Join('\n', path));
        }
        
        // Change name
        var localList = GetLocalList(list);
        localList.Name = newName;

        State.Save();
    }

    public async Task RemoveListAsync(List list)
    {
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


    // Uninstall

    public async Task UninstallAsync() 
    {
        File.WriteAllText(Platform.HostsPath, Config.Get<string>(nameof(Config.DefaultValue.hosts)));
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
