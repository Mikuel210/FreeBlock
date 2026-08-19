using Microsoft.AspNetCore.SignalR.Client;
using Core;

namespace CLI;

public static class ListCommands
{

    public static async Task ShowStatus()
    {
        StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");
        var lists = state.Lists.ToArray();

        if (lists.Length == 0)
        {
            Console.WriteLine("No lists found");
            return;
        }

        foreach (var list in lists)
        {
            Entry entry = new(EntryType.List, list.Name);
            string[] blockReasons = ConsoleUtils.GetBlockReasons(state, entry);

            string reasonsString = blockReasons.Length == 0 ? "" : $" ({string.Join(", ", blockReasons)})";
            Console.WriteLine($"📋{(blockReasons.Length > 0 ? "🟢" : "🔴")} {list.Name}{reasonsString}");
        }
    }

    public static async Task AddList(AddListArgument argument)
    {
        var list = new List { Name = argument.Value! };

        (List<Entry> entries, string contents) = await ConsoleUtils.EditEntries(list: list.Name);
        list.Entries = entries;

        await ConnectionManager.Connection!.InvokeAsync("AddListAsync", list, contents);
        Console.WriteLine($"Added list: {list.Name}");
    }

    public static async Task EditList(ListArgument argument)
    {
        var list = argument.Value!;
        var previousEntries = list.Entries;
        string previousContents = await ConnectionManager.Connection!.InvokeAsync<string>("GetListContentsAsync", list);

        (List<Entry> entries, string contents) = await ConsoleUtils.EditEntries(previousContents, list.Name);
        StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");

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
                if (!ConsoleUtils.PromptClose(true)) return;
            }
        }

        list.Entries = entries;

        await ConnectionManager.Connection!.InvokeAsync("EditListAsync", list, contents);
        Console.WriteLine($"Updated list: {list.Name}");
    }

    public static async Task RenameList(ListArgument listArgument, AddListArgument nameArgument)
    {
        var list = listArgument.Value!;
        string oldName = list.Name;
        string newName = nameArgument.Value!;

        await ConnectionManager.Connection!.InvokeAsync("RenameListAsync", list, newName);
        Console.WriteLine($"Renamed list: {oldName} -> {newName}");
    }

    public static async Task RemoveList(ListArgument argument)
    {
        var list = argument.Value!;
        var entry = new Entry(EntryType.List, list.Name);

        StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");
        string[] blockReasons = ConsoleUtils.GetBlockReasons(state, entry);

        // Check if list is active
        if (blockReasons.Length > 0) 
        {
            ConsoleUtils.Error($"Removing lists while they're active is not allowed: {list.Name}");
            return;
        }

        // Check usage
        bool used = false;

        foreach (var other in state.Lists)
        {
            if (!other.Entries.Contains(entry)) continue;

            ConsoleUtils.Error($"The list is being used by another: {other.Name}");
            used = true;
        }

        foreach (var schedule in state.Schedules)
        {
            if (!schedule.Entries.Contains(entry)) continue;

            ConsoleUtils.Error($"The list is being used by a schedule: {schedule.Name}");
            used = true;
        }

        if (used) return;

        await ConnectionManager.Connection!.InvokeAsync("RemoveListAsync", list);
        Console.WriteLine($"Removed list: {list.Name}");
    }

}