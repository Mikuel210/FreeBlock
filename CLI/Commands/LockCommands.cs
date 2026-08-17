using Microsoft.AspNetCore.SignalR.Client;
using Core;
using Lock = Core.Lock;

namespace CLI;

public static class LockCommands
{

    public static async Task AddLock(AddLockArgument nameArgument, TimeSpanArgument timeArgument, EntriesArgument entriesArgument)
    {
        var time = timeArgument.Value!;
        var unlockTime = DateTime.Now.Add(time);
        var @lock = new Lock(nameArgument.Value!, entriesArgument.Value!, unlockTime);

        var prompt = $"This will block the provided entries for {time} and close all browsers and all blocked apps. Okay to continue?";
        if (!ConsoleUtils.PromptYesNo(prompt)) return;

        await ConnectionManager.Connection!.InvokeAsync("AddLockAsync", @lock);
        Console.WriteLine($"Added lock: {@lock.Name}");
    }

    public static async Task EditLock(LockArgument lockArgument, List<IFlag> flags)
    {
        StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");
        
        var @lock = lockArgument.Value!;
        bool changesMade = false;

        if (flags.OfType<LockAddEntriesFlag>().FirstOrDefault() is { } addEntriesFlag)
        {
            var entries = addEntriesFlag.Value!;

            // Prompt close
            if (!entries.All(e => e.IsActive(state)))
            {
                Console.WriteLine();
                if (!ConsoleUtils.PromptClose(true)) return;
            }

            @lock.Entries.AddRange(entries);
            @lock.Entries = [.. @lock.Entries.Distinct()];
            changesMade = true;
        }

        if (flags.OfType<LockExtendFlag>().FirstOrDefault() is { } extendFlag)
        {
            var timespan = extendFlag.Value!;
            @lock.UnlockTime += timespan;

            changesMade = true;
        }
        
        await ConnectionManager.Connection!.InvokeAsync("EditLockAsync", @lock);

        if (changesMade) Console.WriteLine($"Updated lock: {@lock.Name}");
        else Console.WriteLine("No changes made");
    }

    public static async Task RenameLock(LockArgument lockArgument, AddLockArgument nameArgument)
    {
        var @lock = lockArgument.Value!;
        string oldName = @lock.Name;
        string newName = nameArgument.Value!;

        await ConnectionManager.Connection!.InvokeAsync("RenameLockAsync", @lock, newName);
        Console.WriteLine($"Renamed lock: {oldName} -> {newName}");
    }

}