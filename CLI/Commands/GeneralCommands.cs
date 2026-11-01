using Microsoft.AspNetCore.SignalR.Client;
using static CLI.InvokeHelpers;
using Core;
using Daemon;

namespace CLI;

public static class GeneralCommands
{

    public static async Task<int> HandleRoot(List<IFlag> flags)
    {
        if (flags.Any(e => e.GetType() == typeof(VersionFlag)))
        {
            Console.WriteLine(Globals.VERSION);
            return 0;
        }

        if (flags.Any(e => e.GetType() == typeof(UninstallFlag)))
            return await Uninstall();

        HelpSystem.ShowUsage(CommandSystem.Commands.Single(e => e.IsRoot));
        return 0;
    }

    public static async Task<int> ShowStatus()
    {
        // Get state and configuration
        var state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();
        var values = await ConfigSystem.GetAll();
        var showAllLists = values.showAllLists;
        var showAllSchedules = values.showAllSchedules;
        var showWarningSchedules = values.showWarningPeriodSchedules;

        // Get blocked entries, lists and schedules
        var entries = state.Block
            .Concat(state.Locks.SelectMany(e => e.Entries))
            .Concat(state.Schedules.Where(e => e.Active).SelectMany(e => e.Entries))
            .Distinct()
            .OrderBy(e => e.Type)
            .ToArray();

        // Remove lists from entries section when showAllLists is enabled
        if (showAllLists) entries = [.. entries.Where(e => e.Type != EntryType.List)];

        var lists = state.Lists.ToArray();
        if (!showAllLists) lists = [];

        var schedules = state.Schedules.ToArray();
        if (!showAllSchedules && showWarningSchedules) schedules = [.. schedules.Where(e => e.Active || e.InWarningPeriod)];
        if (!showAllSchedules && !showWarningSchedules) schedules = [.. schedules.Where(e => e.Active)];

        // Sections
        bool showActiveEntries = entries.Length > 0;
        bool showLists = showAllLists && lists.Length > 0;
        bool showLocks = state.Locks.Count > 0;
        bool showSchedules = schedules.Length > 0;
        bool printedSection = false;

        if (!showActiveEntries && !showLists && !showLocks && !showSchedules)
        {
            Console.WriteLine("No blocking is taking place");
            return 0;
        }

        // Blocked entries
        if (showActiveEntries)
        {
            Console.WriteLine("Active entries:");
            printedSection = true;
        }

        foreach (var entry in entries)
            entry.PrintStatus(state);

        if (showLists)
        {
            if (printedSection) Console.WriteLine();

            Console.WriteLine("Block lists:");
            printedSection = true;

            lists.Sort((a, b) => b.IsActive(state).CompareTo(a.IsActive(state)));
        }

        foreach (var list in lists)
        {
            Entry entry = new(EntryType.List, list.Name);
            entry.PrintStatus(state);
        }

        // Locks
        if (showLocks)
        {
            if (printedSection) Console.WriteLine();

            Console.WriteLine("Active locks:");
            printedSection = true;
        }

        foreach (var @lock in state.Locks)
            @lock.PrintStatus();

        // Schedules
        if (showSchedules)
        {
            if (printedSection) Console.WriteLine();

            Console.WriteLine(showAllSchedules ? "Schedules:" : "Active schedules:");
            printedSection = true;

            schedules.Sort((a, b) =>
            {
                int aPriority = a.Active ? 0 : (a.InWarningPeriod ? 1 : 2);
                int bPriority = b.Active ? 0 : (b.InWarningPeriod ? 1 : 2);
                return aPriority.CompareTo(bPriority);
            });
        }

        foreach (var schedule in schedules)
            schedule.PrintStatus();

        return 0;
    }

    public static async Task<int> Uninstall()
    {
        var state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();

        if (state.Block.Count > 0 ||
            state.Locks.SelectMany(e => e.Entries).Count() > 0 ||
            state.Schedules.Where(e => e.Active).SelectMany(e => e.Entries).Count() > 0)
        {
            ConsoleUtils.Error("To prevent impulsive choices, FreeBlock can't be uninstalled while blocking is taking place");
            return 1;
        }

        if (state.Schedules.Count() > 0)
        {
            ConsoleUtils.Error("To prevent impulsive choices, FreeBlock can't be uninstalled if there are schedules in place");
            return 1;
        }

        if (ConsoleUtils.PromptYesNo("Remove user data and preferences?", false))
            (await Invoke(nameof(CommunicationHub.RemovePreferencesAsync))).Unwrap();

        (await Invoke(nameof(CommunicationHub.UninstallAsync))).Unwrap();
        Console.WriteLine("Uninstalled successfully");

        return 0;
    }

    public static async Task<int> Block(EntriesArgument argument)
    {
        StateSnapshot state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();
        bool allAlreadyActive = true;
        bool writeLine = false;
        var entries = argument.Value!;

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
        if (!allAlreadyActive && !ConsoleUtils.PromptClose()) return 1;
        (await Invoke(nameof(CommunicationHub.BlockAsync), entries)).Unwrap();

        if (entries.Count == 1) Console.WriteLine($"Enabled manual block for entry: {entries[0].ToEntryString()}");
        else Console.WriteLine($"Enabled manual block for entries");

        return 0;
    }

    public static async Task<int> Unblock(EntriesArgument argument)
    {
        StateSnapshot state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();
        bool writeLine = false;
        var entries = argument.Value!;

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

                // TODO: check if you're unblocking that list as well
                ConsoleUtils.Warning($"Entry {entry.ToEntryString()} remains blocked by a list: {list.Name}");
                writeLine = true;
            }

            foreach (var @lock in state.Locks)
            {
                if (!@lock.Entries.Contains(entry)) continue;

                ConsoleUtils.Warning($"Entry {entry.ToEntryString()} remains blocked by a lock: {@lock.Name}");
                writeLine = true;
            }

            foreach (var schedule in state.Schedules.Where(e => e.Active))
            {
                if (!schedule.Entries.Contains(entry)) continue;

                ConsoleUtils.Warning($"Entry {entry.ToEntryString()} remains blocked by a schedule: {schedule.Name}");
                writeLine = true;
            }
        }

        if (writeLine) Console.WriteLine();
        (await Invoke(nameof(CommunicationHub.UnblockAsync), entries)).Unwrap();

        if (entries.Count == 1) Console.WriteLine($"Disabled manual block for entry: {entries[0].ToEntryString()}");
        else Console.WriteLine($"Disabled manual block for entries");

        return 0;
    }

}
