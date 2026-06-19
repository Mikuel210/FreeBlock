using Microsoft.AspNetCore.SignalR.Client;
using Daemon;
using SDK;
using CLI;
using Lock = SDK.Lock;

#region Command System

CommandSystem.Register(new Command(
    [],
    [],
    ShowUsage
));

CommandSystem.Register(new Command(
    ["-h"],
    [],
    ShowHelp
));

CommandSystem.Register(new Command(
    ["--help"],
    [],
    ShowHelp
));

CommandSystem.Register(new Command(
    ["-v"],
    [],
    ShowVersion
));

CommandSystem.Register(new Command(
    ["--version"],
    [],
    ShowVersion
));

CommandSystem.Register(new Command(
    ["--uninstall"],
    [],
    Uninstall
));

CommandSystem.Register(new Command(
    ["status"],
    [],
    ShowStatus
));

CommandSystem.Register(new Command(
    ["list", "add"],
    [new AddListArgument("name")],
    AddList
));

CommandSystem.Register(new Command(
    ["list", "edit"],
    [new ListArgument("name")],
    EditList
));

CommandSystem.Register(new Command(
    ["list", "rename"],
    [new ListArgument("old"), new AddListArgument("new")],
    RenameList
));

CommandSystem.Register(new Command(
    ["list", "remove"],
    [new ListArgument("name")],
    RemoveList
));

CommandSystem.Register(new Command(
    ["block"],
    [new EntriesArgument("entries")],
    Block
));

CommandSystem.Register(new Command(
    ["unblock"],
    [new EntriesArgument("entries")],
    Unblock
));

CommandSystem.Register(new Command(
    ["lock"],
    [new TimeArgument("time")],
    Lock
));

CommandSystem.Register(new Command(
    ["schedule", "add"],
    [
        new AddScheduleArgument("name"),
        new TimeArgument("start"),
        new TimeArgument("end"),
        new DaysArgument("days"),
        new EntriesArgument("entries")
    ],
    AddSchedule
));

CommandSystem.Register(new Command(
    ["schedule", "edit"],
    [
        new ScheduleArgument("name"),
        new TimeArgument("start"),
        new TimeArgument("end"),
        new DaysArgument("days"),
        new EntriesArgument("entries")
    ],
    EditSchedule,
    true,
    async (command, i) => {
        if (i == 0) return string.Empty;

        var name = ((ScheduleArgument)command.Arguments[0]).Value!.Name;
        var schedule = await ConnectionManager.Connection!.InvokeAsync<Schedule>("GetScheduleFromNameAsync", name);

        return i switch {
            1 => schedule!.StartTime.ToString(),
            2 => schedule!.EndTime.ToString(),
            3 => schedule!.Days.GetDaysString(),
            4 => string.Join(' ', schedule!.Entries.Select(e => e.ToEntryString())),
            _ => throw new NotImplementedException()
        };
    }
));

CommandSystem.Register(new Command(
    ["schedule", "rename"],
    [new ScheduleArgument("old"), new AddScheduleArgument("new")],
    RenameSchedule
));

CommandSystem.Register(new Command(
    ["schedule", "remove"],
    [new ScheduleArgument("name")],
    RemoveSchedule
));

await CommandSystem.Handle(args);

#endregion

#region Commands

void ShowUsage()
{
    Console.WriteLine("""
                      Usage: freeblock [command]
                      See: freeblock --help
                      """);
}

// todo update help
void ShowHelp()
{
    Console.WriteLine("""
                      Usage: freeblock [command]

                      Available commands:
                      freeblock -h, --help       Show all available commands.
                      freeblock -v, --version    Show the FreeBlock version.
                      freeblock status           Show the current status of block lists and schedules, where green means active.
                      freeblock list add         Create a new block list. Type one app or website to block per line.
                      freeblock list edit        Edit the websites of a block list. Removing websites while the list is active is not allowed.
                      freeblock list rename      Rename a block list.
                      freeblock list remove      Remove a block list. Removing lists while they're active is not allowed.
                      freeblock block            Enable manual block for a list.
                      freeblock unblock          Disable manual block for a list.
                      freeblock lock             Lock a list for the provided amount of time. You won't be able to disable it until the timer ends.
                      freeblock schedule add     Create a new schedule to enable lists automatically on certain time periods.
                      freeblock schedule edit    Edit the properties of a schedule.
                      freeblock schedule rename  Rename a schedule.
                      freeblock schedule remove  Remove a schedule. Removing schedules while they're active is not allowed.
                      freeblock --uninstall      Uninstall FreeBlock.
                      """);
}

string[] GetBlockReasons(StateSnapshot state, Entry entry)
{
    List<string> blockReasons = [];
    if (state.Block.Contains(entry)) blockReasons.Add("manual");

    foreach (var @lock in state.Locks)
    {
        if (@lock.Entries.Contains(entry))
            blockReasons.Add($"🔒 {@lock.UnlockTime}");
    }

    foreach (var list in state.Lists.Where(e => e.IsActive(state)))
    {
        if (list.Entries.Contains(entry))
            blockReasons.Add($"📋 {list.Name}");
    }
        
    foreach (var schedule in state.Schedules.Where(e => e.Active))
    {  
        if (schedule.Entries.Contains(entry))
            blockReasons.Add($"⏰ {schedule.Name}");
    }

    return [.. blockReasons];
}

async Task ShowStatus()
{
    StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");

    var entries = state.Block
        .Concat(state.Locks.SelectMany(e => e.Entries))
        .Concat(state.Schedules.Where(e => e.Active).SelectMany(e => e.Entries))
        .Where(e => e.Type != EntryType.List)
        .Distinct()
        .OrderBy(e => e.Type)
        .ToArray();

    if (entries.Length == 0 && state.Lists.Count == 0 && state.Schedules.Count == 0) // todo locks
    {
        Console.WriteLine("No blocking is taking place");
        return;
    }

    // Entries
    foreach (var entry in entries)
    {
        string[] blockReasons = GetBlockReasons(state, entry);

        string reasonsString = blockReasons.Length == 0 ? "" : $" ({string.Join(", ", blockReasons)})";
        string typeIcon = entry.Type == EntryType.Website ? "🌐" : "💻";
        Console.WriteLine($"{typeIcon}🟢 {entry.Name}{reasonsString}");
    }

    // Lists
    foreach (var list in state.Lists)
    {
        Entry entry = new(EntryType.List, list.Name);
        string[] blockReasons = GetBlockReasons(state, entry);

        string reasonsString = blockReasons.Length == 0 ? "" : $" ({string.Join(", ", blockReasons)})";
        Console.WriteLine($"📋{(blockReasons.Length > 0 ? "🟢" : "🔴")} {list.Name}{reasonsString}");
    }

    // Schedules
    foreach (var schedule in state.Schedules)
    {
        string timeString = $"({schedule.StartTime} - {schedule.EndTime}, {schedule.Days.GetDaysString()})";
        Console.WriteLine($"⏰{(schedule.Active ? "🟢" : "🔴")} {schedule.Name} {timeString}");
    }
}

async Task ShowVersion() => Console.WriteLine("v0.6.0");

async Task Uninstall() 
{
    StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");
    
    if (state.Block.Count > 0 || 
        state.Locks.SelectMany(e => e.Entries).Count() > 0 ||
        state.Schedules.Where(e => e.Active).SelectMany(e => e.Entries).Count() > 0)
    {
        ConsoleUtils.Error("To prevent impulsive choices, FreeBlock can't be uninstalled while blocking is taking place");
        return;
    }

    if (ConsoleUtils.PromptYesNo("Remove user data and preferences?", false)) 
        await ConnectionManager.Connection!.InvokeAsync("RemovePreferencesAsync");

    await ConnectionManager.Connection!.InvokeAsync("UninstallAsync");
    Console.WriteLine("Uninstalled successfully");
}

async Task AddList(AddListArgument argument)
{
    var list = new List { Name = argument.Value! };

    (List<Entry> entries, string contents) = await ConsoleUtils.EditEntries(list: list.Name);
    list.Entries = entries;

    await ConnectionManager.Connection!.InvokeAsync("AddListAsync", list, contents);
    Console.WriteLine("List created successfully");
}

async Task EditList(ListArgument argument)
{
    var list = argument.Value!;
    var previousEntries = list.Entries;
    string previousContents = await ConnectionManager.Connection!.InvokeAsync<string>("GetListContentsAsync", list);

    (List<Entry> entries, string contents) = await ConsoleUtils.EditEntries(previousContents, list.Name);
    StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");

    if (list.IsActive(state))
    {
        // Revert removed websites
        bool showWarning = false;

        foreach (var entry in previousEntries)
        {
            if (entries.Contains(entry)) continue;
            
            entries.Add(entry);
            contents += $"\n{entry.ToEntryString()}";
            showWarning = true;
        }

        if (showWarning) ConsoleUtils.Warning("Removing entries is not allowed while the list is active");

        // Prompt close
        foreach (var entry in entries)
        {
            if (previousEntries.Contains(entry)) continue;

            Console.WriteLine();
            if (!ConsoleUtils.PromptClose(true)) return;

            break;
        }
    }

    list.Entries = entries;

    await ConnectionManager.Connection!.InvokeAsync("EditListAsync", list, contents);
    Console.WriteLine($"Updated list: {list.Name}");
}

async Task RenameList(ListArgument listArgument, AddListArgument nameArgument)
{
    var list = listArgument.Value!;
    string oldName = list.Name;
    string newName = nameArgument.Value!;

    await ConnectionManager.Connection!.InvokeAsync("RenameListAsync", list, newName);
    Console.WriteLine($"Renamed list: {oldName} -> {newName}");
}// todo restrict list names
// todo redo tutorial

async Task RemoveList(ListArgument argument)
{
    var list = argument.Value!;
    var entry = new Entry(EntryType.List, list.Name);

    StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");
    string[] blockReasons = GetBlockReasons(state, entry);

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

async Task Block(EntriesArgument argument)
{
    StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");
    var entries = argument.Value!;

    bool allAlreadyActive = true;
    bool writeLine = false;

    foreach (var entry in entries)
    {
        string[] blockReasons = GetBlockReasons(state, entry);
        if (blockReasons.Length == 0) allAlreadyActive = false;

        if (state.Block.Contains(entry))
        {
            ConsoleUtils.Note($"Entry was already blocked manually: {entry.ToEntryString()}");
            writeLine = true;
        }

        foreach (var list in state.Lists.Where(e => e.IsActive(state)))
        {
            if (!list.Entries.Contains(entry)) continue;
            var listEntry = new Entry(EntryType.List, list.Name);

            ConsoleUtils.Note($"Entry was already blocked by {listEntry.ToEntryString()}: {entry.ToEntryString()}");
            writeLine = true;
        }

        foreach (var @lock in state.Locks)
        {
            if (!@lock.Entries.Contains(entry)) continue;

            ConsoleUtils.Note($"Entry was already active as it's locked: {entry.ToEntryString()}"); // todo name
            writeLine = true;
        }

        foreach (var schedule in state.Schedules.Where(e => e.Active))
        {
            if (!schedule.Entries.Contains(entry)) continue;

            ConsoleUtils.Note($"Entry was already active as it's blocked by an schedule: {entry.ToEntryString()}");
            writeLine = true;
        }
    }

    if (writeLine) Console.WriteLine();
    if (!allAlreadyActive && !ConsoleUtils.PromptClose()) return;

    await ConnectionManager.Connection!.InvokeAsync("BlockAsync", entries);
    Console.WriteLine($"Enabled manual block for entries");
}

async Task Unblock(EntriesArgument argument)
{
    StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");
    var entries = argument.Value!;

    bool writeLine = false;

    foreach (var entry in entries)
    {
        string[] blockReasons = GetBlockReasons(state, entry);

        if (!state.Block.Contains(entry))
        {
            ConsoleUtils.Note($"Entry was already unblocked manually: {entry.ToEntryString()}");
            writeLine = true;
        }

        foreach (var list in state.Lists.Where(e => e.IsActive(state)))
        {
            if (!list.Entries.Contains(entry)) continue;
            var listEntry = new Entry(EntryType.List, list.Name);

            ConsoleUtils.Warning($"Entry remains active as it's blocked by {listEntry.ToEntryString()}: {entry.ToEntryString()}");
            writeLine = true;
        }

        foreach (var @lock in state.Locks)
        {
            if (!@lock.Entries.Contains(entry)) continue;

            ConsoleUtils.Warning($"Entry remains active as it's locked: {entry.ToEntryString()}");
            writeLine = true;
        }

        foreach (var schedule in state.Schedules.Where(e => e.Active))
        {
            if (!schedule.Entries.Contains(entry)) continue;

            ConsoleUtils.Warning($"Entry remains active as it's blocked by an schedule: {entry.ToEntryString()}");
            writeLine = true;
        }
    }

    if (writeLine) Console.WriteLine();

    await ConnectionManager.Connection!.InvokeAsync("UnblockAsync", entries);
    Console.WriteLine($"Disabled manual block for entries");
}


// TODO it'd be pretty cool if you got to name locks and you could edit them
async Task Lock(TimeArgument timeArgument)
{
    var time = timeArgument.Value!.ToTimeSpan();
    var unlockTime = DateTime.Now.Add(time);

    (List<Entry> entries, _) = await ConsoleUtils.EditEntries();
    var @lock = new Lock(entries, unlockTime);

    var prompt = $"This will block the provided entries for {time} and close all browsers and all blocked apps. Okay to continue?";
    if (!ConsoleUtils.PromptYesNo(prompt)) return;

    await ConnectionManager.Connection!.InvokeAsync("LockAsync", @lock);
    Console.WriteLine($"Locked entries until {unlockTime}");
}

async Task AddSchedule(AddScheduleArgument name, TimeArgument start, TimeArgument end, DaysArgument days, EntriesArgument argument)
{
    var entries = argument.Value!;

    var schedule = new Schedule
    {
        Name = name.Value!,
        Entries = entries,
        StartTime = start.Value,
        EndTime = end.Value,
        Days = days.Value!
    };

    if (schedule.Active && !ConsoleUtils.PromptClose()) return;

    await ConnectionManager.Connection!.InvokeAsync("AddScheduleAsync", schedule);
    Console.WriteLine($"Added schedule: {schedule.Name}");
}

async Task EditSchedule(ScheduleArgument schedule, TimeArgument start, TimeArgument end, DaysArgument days, EntriesArgument argument)
{
    var entries = argument.Value!;
    
    // TODO if active and added entries, close browsers, revert removed entries
    // if it wasnt active before, close browsers anyways!!
    // TODO you can edit the time while it's active lol

    var updatedSchedule = new Schedule
    {
        Name = schedule.Value!.Name,
        Entries = entries,
        StartTime = start.Value,
        EndTime = end.Value,
        Days = days.Value!
    };

    await ConnectionManager.Connection!.InvokeAsync("EditScheduleAsync", updatedSchedule);
    Console.WriteLine($"Edited schedule: {schedule.Value.Name}");
}

async Task RenameSchedule(ScheduleArgument scheduleArgument, AddScheduleArgument nameArgument)
{
    var schedule = scheduleArgument.Value!;
    string oldName = schedule.Name;
    string newName = nameArgument.Value!;

    await ConnectionManager.Connection!.InvokeAsync("RenameScheduleAsync", schedule, newName);
    Console.WriteLine($"Renamed schedule: {oldName} -> {newName}");
}

async Task RemoveSchedule(ScheduleArgument argument)
{
    var schedule = argument.Value!;

    if (schedule.Active)
    {
        var now = DateTime.Now;
        bool inWindow = false;
        bool requested = false;

        if (schedule.RemovalRequestTime is DateTime removalRequestTime) 
        {
            inWindow = now >= removalRequestTime.AddDays(1) 
                && now < removalRequestTime.AddDays(2);

            requested = now >= removalRequestTime
                && now < removalRequestTime.AddDays(2);
        }

        // Can request removal
        if (!inWindow && !requested) 
        {
            ConsoleUtils.Error($"Removing schedules while they're active is not allowed: {schedule.Name}");
            Console.WriteLine();

            if (ConsoleUtils.PromptYesNo("To prevent impulsive choices, you can instead request the ability to remove this schedule in a "
                                        + "window that starts 24h from now and ends 48h from now. Do you want to request it now?", false))
            {
                await ConnectionManager.Connection!.InvokeAsync("RequestScheduleRemovalAsync", schedule);
                Console.WriteLine($"Requested schedule removal: {schedule.Name}");
            }

            return;
        }
        
        // Requested
        if (!inWindow && requested) 
        {
            ConsoleUtils.Error($"Removing schedules while they're active is not allowed: {schedule.Name}");

            var windowStart = ((DateTime)schedule.RemovalRequestTime!).AddDays(1);
            ConsoleUtils.Note($"The removal of this schedule is already requested. The window starts {windowStart}");

            return;
        }
        
        // In window: continue
    }

    await ConnectionManager.Connection!.InvokeAsync("RemoveScheduleAsync", schedule);
    Console.WriteLine($"Removed schedule: {schedule.Name}");
}

#endregion
