using Microsoft.AspNetCore.SignalR.Client;
using static CLI.InvokeHelpers;
using Lock = Core.Lock;
using Core;
using Daemon;

namespace CLI;

public static class LockCommands
{

    public static async Task<int> ShowStatus()
    {
        var state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();
        var locks = state.Locks.ToArray();

        if (locks.Length == 0)
        {
            Console.WriteLine("No locks found");
            return 0;
        }

        foreach (var @lock in locks)
            @lock.PrintStatus();

        return 0;
    }

    public static async Task<int> ShowLock(LockArgument argument)
    {
        var @lock = argument.Value!;

        Dictionary<string, object?> values = new() {
            { "name", @lock.Name },
            { "unlock time", @lock.UnlockTime },
            { "entries", string.Join(", ", @lock.Entries.Select(e => e.ToEntryString())) }
        };

        ConsoleUtils.PrintDictionary(values);
        return 0;
    }

    public static async Task<int> AddLock(AddLockArgument nameArgument, TimeSpanArgument timeArgument, EntriesArgument entriesArgument)
    {
        var time = timeArgument.Value!;
        var unlockTime = DateTime.Now.Add(time);
        var @lock = new Lock(nameArgument.Value!, entriesArgument.Value!, unlockTime);

        var prompt = $"This will block the provided entries for {time} and close all browsers and all blocked apps. Okay to continue?";
        if (!ConsoleUtils.PromptYesNo(prompt)) return 1;

        (await Invoke(nameof(CommunicationHub.AddLockAsync), @lock)).Unwrap();
        Console.WriteLine($"Added lock: {@lock.Name}");

        return 0;
    }

    public static async Task<int> EditLock(LockArgument lockArgument, List<IFlag> flags)
    {
        var state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();
        var @lock = lockArgument.Value!;
        bool changesMade = false;

        if (flags.OfType<LockAddEntriesFlag>().FirstOrDefault() is { } addEntriesFlag)
        {
            var entries = addEntriesFlag.Value!;

            // Prompt close
            if (!entries.All(e => e.IsActive(state)))
            {
                Console.WriteLine();
                if (!ConsoleUtils.PromptClose(true)) return 1;
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

        (await Invoke(nameof(CommunicationHub.EditLockAsync), @lock)).Unwrap();
        if (changesMade) Console.WriteLine($"Updated lock: {@lock.Name}");
        else Console.WriteLine("No changes made");

        return 0;
    }

    public static async Task<int> RenameLock(LockArgument lockArgument, AddLockArgument nameArgument)
    {
        var @lock = lockArgument.Value!;
        string oldName = @lock.Name;
        string newName = nameArgument.Value!;

        (await Invoke(nameof(CommunicationHub.RenameLockAsync), @lock, newName)).Unwrap();
        Console.WriteLine($"Renamed lock: {oldName} -> {newName}");

        return 0;
    }

}
