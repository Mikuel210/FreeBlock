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

    public async Task EditListAsync(BlockList list, bool closeBrowsers)
    {
        var localList = GetLocalList(list);
        localList.UrlList.Clear();
        localList.UrlList.AddRange(list.UrlList);

        if (closeBrowsers) Blocker.CloseBrowsers();
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
        localList!.Enabled = true;

        Blocker.UpdateBlock();
        Blocker.CloseBrowsers();
        Config.Save();
    }

    public async Task UnblockAsync(BlockList list)
    {
        var localList = GetLocalList(list);
        localList!.Enabled = false;

        Blocker.UpdateBlock();
        Config.Save();
    }

    public async Task LockAsync(BlockList list, DateTime unlockTime)
    {
        var localList = GetLocalList(list);
        localList.Enabled = true;
        localList.UnlockTime = unlockTime;

        Blocker.UpdateBlock();
        Blocker.CloseBrowsers();
        Config.Save();
    }

    public async Task<BlockList[]> GetBlockListsAsync()
        => Config.BlockLists.ToArray();

    public async Task<BlockList?> GetListFromNameAsync(string name)
        => Config.BlockLists.FirstOrDefault(e => e.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

    private BlockList GetLocalList(BlockList clientList)
        => Config.BlockLists.First(e => e.Name.Equals(clientList.Name, StringComparison.InvariantCultureIgnoreCase));

}
