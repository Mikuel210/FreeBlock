using Microsoft.AspNetCore.SignalR;
using SDK;

namespace Daemon;

public class CommunicationHub : Hub
{

    public async Task AddListAsync(BlockList list)
    {
        Config.BlockLists.Add(list);
        Config.Save();
    }

    public async Task EditListAsync(BlockList list)
    {
        var localList = GetLocalList(list);
        localList.UrlList.Clear();
        localList.UrlList.AddRange(list.UrlList);

        ApplyChanges();
    }

    public async Task RenameListAsync(BlockList list, string newName)
    {
        var localList = GetLocalList(list);
        localList.Name = newName;

        Config.Save();
    }

    public async Task RemoveListAsync(BlockList list)
    {
        var localList = GetLocalList(list);
        Config.BlockLists.Remove(localList);

        ApplyChanges();
    }

    public async Task BlockAsync(BlockList list)
    {
        var localList = GetLocalList(list);
        localList!.ManuallyBlocked = true;

        ApplyChanges();
    }

    public async Task UnblockAsync(BlockList list)
    {
        var localList = GetLocalList(list);
        localList!.ManuallyBlocked = false;

        ApplyChanges();
    }

    public async Task LockAsync(BlockList list, DateTime unlockTime)
    {
        var localList = GetLocalList(list);
        localList.UnlockTime = unlockTime;

        ApplyChanges();
    }

    public async Task AddScheduleAsync(Schedule schedule)
    {
        Config.Schedules.Add(schedule);
        Config.Save();
    }

    public async Task RemoveScheduleAsync(Schedule schedule)
    {
        var localSchedule = GetLocalSchedule(schedule);
        Config.Schedules.Remove(localSchedule);
        Config.Save();
    }

    private static void ApplyChanges()
    {
        Blocker.UpdateBlock();
        Config.Save();
    }

    public async Task<BlockList[]> GetBlockListsAsync()
        => Config.BlockLists.ToArray();

    public async Task<Schedule[]> GetSchedulesAsync()
        => Config.Schedules.ToArray();

    public async Task<BlockList?> GetListFromNameAsync(string name)
        => GetFromName(Config.BlockLists, name);

    public async Task<Schedule?> GetScheduleFromNameAsync(string name)
        => GetFromName(Config.Schedules, name);

    private BlockList GetLocalList(BlockList clientList)
        => GetLocal(Config.BlockLists, clientList);

    private Schedule GetLocalSchedule(Schedule clientSchedule)
        => GetLocal(Config.Schedules, clientSchedule);

    private T? GetFromName<T>(List<T> list, string name) where T : IName
    => list.FirstOrDefault(e => e.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

    private T GetLocal<T>(List<T> list, T client) where T : IName
        => list.First(e => e.Name.Equals(client.Name, StringComparison.InvariantCultureIgnoreCase));

}
