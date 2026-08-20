using Microsoft.AspNetCore.SignalR.Client;
using Core;

namespace CLI;

public static class GeneralCommands
{

    public static Task HandleRoot(List<IFlag> flags)
    {
        if (flags.Any(e => e.GetType() == typeof(VersionFlag)))
        {
            Console.WriteLine("v0.7.0");
            return Task.CompletedTask;
        }

        if (flags.Any(e => e.GetType() == typeof(UninstallFlag)))
            return Uninstall();

        HelpSystem.ShowUsage(CommandSystem.Commands.Single(e => e.IsRoot));
        return Task.CompletedTask;
    }

    public static async Task ShowStatus()
    {
        StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");

        var entries = state.Block
            .Concat(state.Locks.SelectMany(e => e.Entries))
            .Concat(state.Schedules.Where(e => e.Active).SelectMany(e => e.Entries))
            .Distinct()
            .OrderBy(e => e.Type)
            .ToArray();

        var lists = state.Lists.Where(e => e.IsActive(state)).ToArray();
        var schedules = state.Schedules.Where(e => e.Active).ToArray();

        bool showBlockedEntries = entries.Length > 0 || lists.Length > 0;
        bool showLocks = state.Locks.Count > 0;
        bool showSchedules = schedules.Length > 0;

        if (!showBlockedEntries && !showLocks && !showSchedules)
        {
            Console.WriteLine("No blocking is taking place");
            return;
        }

        // Blocked entries
        if (showBlockedEntries) Console.WriteLine("Active entries:");

        foreach (var entry in entries)
        {
            string[] blockReasons = ConsoleUtils.GetBlockReasons(state, entry);

            string reasonsString = blockReasons.Length == 0 ? "" : $" ({string.Join(", ", blockReasons)})";
            string typeIcon = entry.Type == EntryType.Website ? "🌐" : "💻";
            Console.WriteLine($"{typeIcon}🟢 {entry.Name}{reasonsString}");
        }

        foreach (var list in lists)
        {
            Entry entry = new(EntryType.List, list.Name);
            string[] blockReasons = ConsoleUtils.GetBlockReasons(state, entry);

            string reasonsString = blockReasons.Length == 0 ? "" : $" ({string.Join(", ", blockReasons)})";
            Console.WriteLine($"📋{(blockReasons.Length > 0 ? "🟢" : "🔴")} {list.Name}{reasonsString}");
        }

        if (showBlockedEntries && (showLocks || showSchedules)) Console.WriteLine();

        // Locks
        if (showLocks) Console.WriteLine("Active locks:");
        foreach (var @lock in state.Locks) Console.WriteLine($"🔒🟢 {@lock.Name} ({@lock.UnlockTime})");

        if (showLocks && showSchedules) Console.WriteLine();

        // Schedules
        if (showSchedules) Console.WriteLine("Active schedules:");

        foreach (var schedule in schedules)
        {
            string timeString = $"({schedule.StartTime} - {schedule.EndTime}, {schedule.Days.GetDaysString()})";
            Console.WriteLine($"⏰{(schedule.Active ? "🟢" : "🔴")} {schedule.Name} {timeString}");
        }
    }

    public static async Task Uninstall() 
    {
        StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");
        
        if (state.Block.Count > 0 || 
            state.Locks.SelectMany(e => e.Entries).Count() > 0 ||
            state.Schedules.Where(e => e.Active).SelectMany(e => e.Entries).Count() > 0)
        {
            ConsoleUtils.Error("To prevent impulsive choices, FreeBlock can't be uninstalled while blocking is taking place");
            return;
        }

        if (state.Schedules.Count() > 0)
        {
            ConsoleUtils.Error("To prevent impulsive choices, FreeBlock can't be uninstalled if there are schedules in place");
            return;
        }

        if (ConsoleUtils.PromptYesNo("Remove user data and preferences?", false)) 
            await ConnectionManager.Connection!.InvokeAsync("RemovePreferencesAsync");

        await ConnectionManager.Connection!.InvokeAsync("UninstallAsync");
        Console.WriteLine("Uninstalled successfully");
    }

    public static async Task Block(EntriesArgument argument)
    {
        StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");
        var entries = argument.Value!;

        bool allAlreadyActive = true;
        bool writeLine = false;

        foreach (var entry in entries)
        {
            string[] blockReasons = ConsoleUtils.GetBlockReasons(state, entry);
            if (blockReasons.Length == 0) allAlreadyActive = false;

            if (state.Block.Contains(entry))
            {
                ConsoleUtils.Note($"Entry {entry.ToEntryString()} was already blocked manually");
                writeLine = true;
            }

            foreach (var list in state.Lists.Where(e => e.IsActive(state)))
            {
                if (!list.Entries.Contains(entry)) continue;
                var listEntry = new Entry(EntryType.List, list.Name);

                ConsoleUtils.Note($"Entry {entry.ToEntryString()} was already blocked by a list: {list.Name}");
                writeLine = true;
            }

            foreach (var @lock in state.Locks)
            {
                if (!@lock.Entries.Contains(entry)) continue;

                ConsoleUtils.Note($"Entry {entry.ToEntryString()} was already blocked by a lock: {@lock.Name}");
                writeLine = true;
            }

            foreach (var schedule in state.Schedules.Where(e => e.Active))
            {
                if (!schedule.Entries.Contains(entry)) continue;

                ConsoleUtils.Note($"Entry {entry.ToEntryString()} was already blocked by a schedule: {schedule.Name}");
                writeLine = true;
            }
        }

        if (writeLine) Console.WriteLine();
        if (!allAlreadyActive && !ConsoleUtils.PromptClose()) return;
        await ConnectionManager.Connection!.InvokeAsync("BlockAsync", entries);
        
        if (entries.Count == 1) Console.WriteLine($"Enabled manual block for entry: {entries[0].ToEntryString()}");
        else Console.WriteLine($"Enabled manual block for entries");
    }

    public static async Task Unblock(EntriesArgument argument)
    {
        StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");
        var entries = argument.Value!;

        bool writeLine = false;

        foreach (var entry in entries)
        {
            string[] blockReasons = ConsoleUtils.GetBlockReasons(state, entry);

            if (!state.Block.Contains(entry))
            {
                ConsoleUtils.Note($"Entry {entry.ToEntryString()} was already unblocked manually");
                writeLine = true;
            }

            foreach (var list in state.Lists.Where(e => e.IsActive(state)))
            {
                if (!list.Entries.Contains(entry)) continue;
                var listEntry = new Entry(EntryType.List, list.Name);
                
                ConsoleUtils.Warning($"Entry {entry.ToEntryString()} remains blocked by a list: {list.Name}"); // TODO: check if you're unblocking that list as well
                writeLine = true;
            }

            foreach (var @lock in state.Locks)
            {
                if (!@lock.Entries.Contains(entry)) continue;

                ConsoleUtils.Warning($"Entry {entry.ToEntryString()} remains blocked by a lock: {entry.ToEntryString()}");
                writeLine = true;
            }

            foreach (var schedule in state.Schedules.Where(e => e.Active))
            {
                if (!schedule.Entries.Contains(entry)) continue;

                ConsoleUtils.Warning($"Entry {entry.ToEntryString()} remains blocked by a schedule: {entry.ToEntryString()}");
                writeLine = true;
            }
        }

        if (writeLine) Console.WriteLine();
        await ConnectionManager.Connection!.InvokeAsync("UnblockAsync", entries);

        if (entries.Count == 1) Console.WriteLine($"Disabled manual block for entry: {entries[0].ToEntryString()}");
        else Console.WriteLine($"Disabled manual block for entries");
    }

}