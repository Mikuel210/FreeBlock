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

        var prompt = $"This will block the provided entries for {time} and close all browsers and all blocked apps. Okay to continue?";
        if (!ConsoleUtils.PromptYesNo(prompt)) return 1;

        (await Invoke(nameof(CommunicationHub.AddLockAsync), nameArgument.Value!, entriesArgument.Value!, time)).Unwrap();
        Console.WriteLine($"Added lock: {nameArgument.Value}");

        return 0;
    }

    public static async Task<int> EditLock(LockArgument lockArgument, List<IFlag> flags)
    {
        var state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();
        var @lock = lockArgument.Value!;

        bool changesMade = false;
        List<Entry> addedEntries = [];
        TimeSpan extension = TimeSpan.Zero;

        if (flags.OfType<LockAddEntriesFlag>().FirstOrDefault() is { } addEntriesFlag)
        {
            var entries = addEntriesFlag.Value!;

            // Prompt close
            if (!entries.All(e => e.IsActive(state)))
            {
                Console.WriteLine();
                if (!ConsoleUtils.PromptClose(true)) return 1;
            }

            addedEntries = [.. entries.Distinct()];
            changesMade = true;
        }

        if (flags.OfType<LockExtendFlag>().FirstOrDefault() is { } extendFlag)
        {
            var timespan = extendFlag.Value!;
            extension = timespan;

            changesMade = true;
        }

        // The daemon applies the delta to live state and its own clock, not a stale client expiry
        (await Invoke(nameof(CommunicationHub.ExtendLockAsync), @lock.Name, addedEntries, extension)).Unwrap();

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
