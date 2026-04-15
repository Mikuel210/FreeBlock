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

        Blocker.UpdateBlock();
        Config.Save();
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

        Blocker.UpdateBlock();
        Config.Save();
    }

    public async Task BlockAsync(BlockList list)
    {
        var localList = GetLocalList(list);
        var enabled = localList!.Active;
        localList!.ManuallyBlocked = true;
        Config.Save();

        if (enabled) return;
        Blocker.UpdateBlock();
    }

    public async Task UnblockAsync(BlockList list)
    {
        var localList = GetLocalList(list);
        localList!.ManuallyBlocked = false;

        Blocker.UpdateBlock();
        Config.Save();
    }

    public async Task LockAsync(BlockList list, DateTime unlockTime)
    {
        var localList = GetLocalList(list);
        localList.UnlockTime = unlockTime;

        Blocker.UpdateBlock();
        Config.Save();
    }

    public async Task AddScheduleAsync(Schedule schedule)
    {
        Config.Schedules.Add(schedule); // TODO
        Config.Save();
    }

    public async Task<BlockList[]> GetBlockListsAsync()
        => Config.BlockLists.ToArray();

    public async Task<Schedule[]> GetSchedulesAsync()
        => Config.Schedules.ToArray();

    public async Task<BlockList?> GetListFromNameAsync(string name)
        => Config.BlockLists.FirstOrDefault(e => e.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

    public async Task<Schedule?> GetScheduleFromNameAsync(string name)
        => Config.Schedules.FirstOrDefault(e => e.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

    private BlockList GetLocalList(BlockList clientList)
        => Config.BlockLists.First(e => e.Name.Equals(clientList.Name, StringComparison.InvariantCultureIgnoreCase));

}
