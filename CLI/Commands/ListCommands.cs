using Microsoft.AspNetCore.SignalR.Client;
using static CLI.InvokeHelpers;
using Core;
using Daemon;

namespace CLI;

public static class ListCommands
{

    public static async Task<int> ShowStatus()
    {
        var state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();
        var lists = state.Lists.ToArray();

        if (lists.Length == 0)
        {
            Console.WriteLine("No lists found");
            return 0;
        }

        lists.Sort((a, b) => b.IsActive(state).CompareTo(a.IsActive(state)));

        foreach (var list in lists)
        {
            Entry entry = new(EntryType.List, list.Name);
            entry.PrintStatus(state);
        }

        return 0;
    }

    public static async Task<int> ShowList(ListArgument argument)
    {
        var result = (await Invoke<string>(nameof(CommunicationHub.GetListContentsAsync), argument.Value!)).Unwrap();
        Console.WriteLine(result);

        return 0;
    }

    public static async Task<int> AddList(AddListArgument argument)
    {
        var list = new List { Name = argument.Value! };

        (List<Entry> entries, string contents) = await ConsoleUtils.EditEntries(list: list.Name);
        list.Entries = entries;

        (await Invoke(nameof(CommunicationHub.AddListAsync), list, contents)).Unwrap();
        Console.WriteLine($"Added list: {list.Name}");

        return 0;
    }

    public static async Task<int> EditList(ListArgument argument)
    {
        var list = argument.Value!;
        var previousEntries = list.Entries;
        string previousContents = (await Invoke<string>(nameof(CommunicationHub.GetListContentsAsync), list)).Unwrap();

        (List<Entry> entries, string contents) = await ConsoleUtils.EditEntries(previousContents, list.Name);
        var state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();

        if (list.IsActive(state))
        {
            // Revert removed entries
            bool showWarning = false;

            foreach (var entry in previousEntries)
            {
                if (entries.Contains(entry)) continue;

                entries.Add(entry);
                contents += $"\n{entry.ToEntryString()}";
                showWarning = true;
            }

            if (showWarning) ConsoleUtils.Warning("Removing entries is not allowed while the list is active and they have been added back");

            // Prompt close
            if (!entries.All(e => e.IsActive(state)))
            {
                Console.WriteLine();
                if (!ConsoleUtils.PromptClose(true)) return 1;
            }
        }

        list.Entries = entries;

        (await Invoke(nameof(CommunicationHub.EditListAsync), list, contents)).Unwrap();
        Console.WriteLine($"Updated list: {list.Name}");

        return 0;
    }

    public static async Task<int> RenameList(ListArgument listArgument, AddListArgument nameArgument)
    {
        var list = listArgument.Value!;
        string oldName = list.Name;
        string newName = nameArgument.Value!;

        (await Invoke(nameof(CommunicationHub.RenameListAsync), list, newName)).Unwrap();
        Console.WriteLine($"Renamed list: {oldName} -> {newName}");

        return 0;
    }

    public static async Task<int> RemoveList(ListArgument argument)
    {
        var list = argument.Value!;
        var entry = new Entry(EntryType.List, list.Name);

        var state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();
        string[] blockReasons = ConsoleUtils.GetBlockReasons(state, entry);

        // Check if list is active
        if (blockReasons.Length > 0)
        {
            ConsoleUtils.Error($"Removing lists while they're active is not allowed: {list.Name}");
            return 1;
        }

        // Check usage
        bool used = false;

        foreach (var other in state.Lists)
        {
            if (!other.Entries.Contains(entry)) continue;

            ConsoleUtils.Error($"The list is being used by another: {other.Name}");
            used = true;
        }

        foreach (var @lock in state.Locks)
        {
            if (!@lock.Entries.Contains(entry)) continue;

            ConsoleUtils.Error($"The list is being used by a lock: {@lock.Name}");
            used = true;
        }

        foreach (var schedule in state.Schedules)
        {
            if (!schedule.Entries.Contains(entry)) continue;

            ConsoleUtils.Error($"The list is being used by a schedule: {schedule.Name}");
            used = true;
        }

        if (used) return 1;

        (await Invoke(nameof(CommunicationHub.RemoveListAsync), list)).Unwrap();
        Console.WriteLine($"Removed list: {list.Name}");

        return 0;
    }

}
