using Microsoft.AspNetCore.SignalR;
using SDK;

namespace Daemon;

public class CommunicationHub : Hub
{

    public async Task AddListAsync(BlockList list)
    {
        State.BlockLists.Add(list);
        State.Save();
    }

    public async Task EditListAsync(BlockList list)
    {
        var localList = GetLocalList(list);
        localList.UrlList.Clear();
        localList.UrlList.AddRange(list.UrlList);

        await ApplyChanges();
    }

    public async Task RenameListAsync(BlockList list, string newName)
    {
        var localList = GetLocalList(list);
        localList.Name = newName;

        State.Save();
    }

    public async Task RemoveListAsync(BlockList list)
    {
        var localList = GetLocalList(list);
        State.BlockLists.Remove(localList);

        await ApplyChanges();
    }

    public async Task BlockAsync(BlockList list)
    {
        var localList = GetLocalList(list);
        localList!.ManuallyBlocked = true;

        await ApplyChanges();
    }

    public async Task UnblockAsync(BlockList list)
    {
        var localList = GetLocalList(list);
        localList!.ManuallyBlocked = false;

        await ApplyChanges();
    }

    public async Task LockAsync(BlockList list, DateTime unlockTime)
    {
        var localList = GetLocalList(list);
        localList.UnlockTime = unlockTime;

        await ApplyChanges();
    }

    public async Task AddScheduleAsync(Schedule schedule)
    {
        State.Schedules.Add(schedule);
        State.Save();
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

    private static async Task ApplyChanges()
    {
        await Blocker.UpdateAsync();
        State.Save();
    }


    public async Task<BlockList[]> GetBlockListsAsync()
        => State.BlockLists.ToArray();

    public async Task<Schedule[]> GetSchedulesAsync()
        => State.Schedules.ToArray();

    public async Task<BlockList?> GetListFromNameAsync(string name)
        => GetFromName(State.BlockLists, name);

    public async Task<Schedule?> GetScheduleFromNameAsync(string name)
        => GetFromName(State.Schedules, name);


    private static BlockList GetLocalList(BlockList clientList)
        => GetLocal(State.BlockLists, clientList);

    private static Schedule GetLocalSchedule(Schedule clientSchedule)
        => GetLocal(State.Schedules, clientSchedule);

    private static T? GetFromName<T>(List<T> list, string name) where T : IStateObject
    => list.FirstOrDefault(e => e.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

    private static T GetLocal<T>(List<T> list, T client) where T : IStateObject
        => list.First(e => e.Name.Equals(client.Name, StringComparison.InvariantCultureIgnoreCase));

}
