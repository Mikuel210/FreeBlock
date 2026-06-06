using Microsoft.AspNetCore.SignalR;
using SDK;
using Lock = SDK.Lock;

namespace Daemon;

public class CommunicationHub : Hub
{

    #region Actions

    public async Task AddListAsync(List list)
    {
        State.Lists.Add(list);
        State.Save();
    }

    public async Task EditListAsync(List list)
    {
        var localList = GetLocalList(list);
        localList.Entries = list.Entries;

        await ApplyChanges();
    }

    public async Task RenameListAsync(List list, string newName)
    {
        var localList = GetLocalList(list);
        localList.Name = newName;

        State.Save();
    }

    public async Task RemoveListAsync(List list)
    {
        var localList = GetLocalList(list);
        State.Lists.Remove(localList);

        await ApplyChanges();
    }

    public async Task BlockAsync(List<Entry> entries)
    {
        State.Block.AddRange(entries);
        await ApplyChanges();
    }

    public async Task UnblockAsync(List<Entry> entries)
    {
        State.Block = [.. State.Block.Except(entries)];
        await ApplyChanges();
    }

    public async Task LockAsync(Lock @lock)
    {
        State.Locks.Add(@lock);
        await ApplyChanges();
    }

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

    public async Task Uninstall() => await Platform.Uninstall.Run();

    public async Task RemovePreferences() => await Platform.RemovePreferences.Run();

    #endregion

    #region Get State

    public async Task<(List<Entry>, List<Lock>, List<List>, List<Schedule>)> GetStateAsync()
        => (State.Block, State.Locks, State.Lists, State.Schedules);

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

    public async Task<Schedule?> GetScheduleFromNameAsync(string name)
        => GetFromName(State.Schedules, name);

    private static T GetLocal<T>(List<T> list, T client) where T : IName
        => list.First(e => e.Name.Equals(client.Name, StringComparison.InvariantCultureIgnoreCase));

    private static List GetLocalList(List clientList)
        => GetLocal(State.Lists, clientList);

    private static Schedule GetLocalSchedule(Schedule clientSchedule)
        => GetLocal(State.Schedules, clientSchedule);

    #endregion

    private static async Task ApplyChanges()
    {
        await Blocker.UpdateAsync();
        State.Save();
    }

}
