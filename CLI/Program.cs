using Microsoft.AspNetCore.SignalR.Client;
using Core;
using CLI;
using Lock = Core.Lock;

#region Command System

CommandSystem.Register(new Command(
    [],
    Category.Blocking,
    "The FOSS website and app blocker",
    [],
    [],
    ShowUsage,
    Executable: false,
    IsRoot: true
));

CommandSystem.Register(new Command(
    ["status"],
    Category.Blocking,
    "Show the current status of blocking",
    [],
    [],
    ShowStatus
));


// Blocking

CommandSystem.Register(new Command(
    ["block"],
    Category.Blocking,
    "Enable manual block for one or more entries",
    [new EntriesArgument("entries", "The entries to block")],
    [],
    Block
));

CommandSystem.Register(new Command(
    ["unblock"],
    Category.Blocking,
    "Disable manual block for one or more entries",
    [new EntriesArgument("entries", "The entries to unblock")],
    [],
    Unblock
));


// Lists

CommandSystem.Register(new Command(
    ["list"],
    Category.Lists,
    "Manage lists",
    [],
    [],
    () => {},
    Executable: false
));

CommandSystem.Register(new Command(
    ["list", "add"],
    Category.Lists,
    "Create a new block list from a set of entries",
    [new AddListArgument("name", "The name of the new list")],
    [],
    AddList
));

CommandSystem.Register(new Command(
    ["list", "edit"],
    Category.Lists,
    "Edit the entries of a block list",
    [new ListArgument("name", "The name of the list to edit")],
    [],
    EditList
));

CommandSystem.Register(new Command(
    ["list", "rename"],
    Category.Lists,
    "Rename a block list",
    [
        new ListArgument("old", "The old name of the list"),
        new AddListArgument("new", "The new name of the list")
    ],
    [],
    RenameList
));

CommandSystem.Register(new Command(
    ["list", "remove"],
    Category.Lists,
    "Remove a block list",
    [new ListArgument("name", "The name of the list to remove")],
    [],
    RemoveList
));


// Locks

CommandSystem.Register(new Command(
    ["lock"],
    Category.Locks,
    "Manage locks",
    [],
    [],
    () => {},
    Executable: false
));

CommandSystem.Register(new Command(
    ["lock", "add"],
    Category.Locks,
    "Block a set of entries until a timer runs out",
    [
        new AddLockArgument("name", "The name of the new lock"),
        new TimeSpanArgument("time", "The duration of the lock (HH:MM[:SS])"),
        new EntriesArgument("entries", "The entries to lock")
    ],
    [],
    AddLock
));

CommandSystem.Register(new Command(
    ["lock", "edit"],
    Category.Locks,
    "Edit the entries of a lock",
    [
        new LockArgument("name", "The name of the lock to edit"),
        new EntriesArgument("entries", "The new entries of the lock")
    ],
    [],
    EditLock,
    true,
    async (command, i) => {
        if (i == 0) return string.Empty;

        var name = ((LockArgument)command.Arguments[0]).Value!.Name;
        var @lock = await ConnectionManager.Connection!.InvokeAsync<Lock>("GetLockFromNameAsync", name);

        return string.Join(' ', @lock!.Entries.Select(e => e.ToEntryString()));
    }
));

CommandSystem.Register(new Command(
    ["lock", "rename"],
    Category.Locks,
    "Rename a lock",
    [
        new LockArgument("old", "The old name of the lock"),
        new AddLockArgument("new", "The new name of the lock")
    ],
    [],
    RenameLock
));


// Schedules

CommandSystem.Register(new Command(
    ["schedule"],
    Category.Schedules,
    "Manage schedules",
    [],
    [],
    () => {},
    Executable: false
));

CommandSystem.Register(new Command(
    ["schedule", "add"],
    Category.Schedules,
    "Create a schedule to enable entries automatically",
    [
        new AddScheduleArgument("name", "The name of the new schedule"),
        new TimeArgument("start", "The start time for the schedule (HH:MM[:SS])"),
        new TimeArgument("end", "The end time for the schedule (HH:MM[:SS])"),
        new DaysArgument("days", "The days of the week to apply the schedule (MTWHFSU)"),
        new EntriesArgument("entries", "The entries to be blocked by the schedule")
    ],
    [],
    AddSchedule
));

CommandSystem.Register(new Command(
    ["schedule", "edit"],
    Category.Schedules,
    "Edit the properties of a schedule",
    [
        new ScheduleArgument("name", "The name of the schedule to edit"),
        new TimeArgument("start", "The new start time for the schedule (HH:MM[:SS])"),
        new TimeArgument("end", "The new end time for the schedule (HH:MM[:SS])"),
        new DaysArgument("days", "The new days of the week to apply the schedule (MTWHFSU)"),
        new EntriesArgument("entries", "The new entries to be blocked by the schedule")
    ],
    [new WarningTimeFlag()],
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
    Category.Schedules,
    "Rename a schedule",
    [
        new ScheduleArgument("old", "The old name of the schedule"),
        new AddScheduleArgument("new", "The new name of the schedule")
    ],
    [],
    RenameSchedule
));

CommandSystem.Register(new Command(
    ["schedule", "remove"],
    Category.Schedules,
    "Remove a schedule",
    [
        new ScheduleArgument("name", "The name of the schedule to remove")
    ],
    [],
    RemoveSchedule
));

await CommandSystem.Handle(args);

#endregion

#region Commands

// General

void ShowUsage()
{
    Console.WriteLine("""
                      Usage: freeblock <command> [<args>]
                      See: freeblock --help
                      """);
}

void ShowHelp()
{
    Console.WriteLine("""
                      Usage: freeblock [-v | --version] [-h | --help] [--uninstall] <command> [<args>]
                      Below is a list of all available commands.

                      Manage blocking:
                        freeblock status           Show the current status of blocking, where green means active.
                        freeblock block            Enable manual block for one or more entries.
                        freeblock unblock          Disable manual block for one or more entries.

                      Manage block lists:
                        freeblock list add         Create a new block list from a set of entries.
                        freeblock list edit        Edit the entries of a block list.
                        freeblock list rename      Rename a block list.
                        freeblock list remove      Remove a block list.

                      Manage locks:
                        freeblock lock add         Block one or more entries for the provided amount of time.
                        freeblock lock edit        Edit the entries of a lock.
                        freeblock lock rename      Rename a lock.

                      Manage schedules:  
                        freeblock schedule add     Create a new schedule to enable entries automatically.
                        freeblock schedule edit    Edit the properties of a schedule.
                        freeblock schedule rename  Rename a schedule.
                        freeblock schedule remove  Remove a schedule.
                      """);
}

string[] GetBlockReasons(StateSnapshot state, Entry entry)
{
    List<string> blockReasons = [];
    if (state.Block.Contains(entry)) blockReasons.Add("manual");

    foreach (var @lock in state.Locks)
    {
        if (@lock.Entries.Contains(entry))
            blockReasons.Add($"🔒 {@lock.Name}");
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

    if (entries.Length == 0 && state.Lists.Count == 0 && state.Locks.Count == 0 && state.Schedules.Count == 0)
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

    // Locks
    foreach (var @lock in state.Locks)
    {
        Console.WriteLine($"🔒🟢 {@lock.Name} ({@lock.UnlockTime})");
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


// Blocking

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


// Lists

async Task AddList(AddListArgument argument)
{
    var list = new List { Name = argument.Value! };

    (List<Entry> entries, string contents) = await ConsoleUtils.EditEntries(list: list.Name);
    list.Entries = entries;

    await ConnectionManager.Connection!.InvokeAsync("AddListAsync", list, contents);
    Console.WriteLine($"Added list: {list.Name}");
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

async Task RenameList(ListArgument listArgument, AddListArgument nameArgument)
{
    var list = listArgument.Value!;
    string oldName = list.Name;
    string newName = nameArgument.Value!;

    await ConnectionManager.Connection!.InvokeAsync("RenameListAsync", list, newName);
    Console.WriteLine($"Renamed list: {oldName} -> {newName}");
}

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


// Locks

async Task AddLock(AddLockArgument nameArgument, TimeSpanArgument timeArgument, EntriesArgument entriesArgument)
{
    var time = timeArgument.Value!;
    var unlockTime = DateTime.Now.Add(time);
    var @lock = new Lock(nameArgument.Value!, entriesArgument.Value!, unlockTime);

    var prompt = $"This will block the provided entries for {time} and close all browsers and all blocked apps. Okay to continue?";
    if (!ConsoleUtils.PromptYesNo(prompt)) return;

    await ConnectionManager.Connection!.InvokeAsync("AddLockAsync", @lock);
    Console.WriteLine($"Added lock: {@lock.Name}");
}

async Task EditLock(LockArgument lockArgument, EntriesArgument entriesArgument)
{
    StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");

    var @lock = lockArgument.Value!;
    var entries = entriesArgument.Value!;
    var previousEntries = @lock.Entries;

    // Revert removed entries
    bool showWarning = false;

    foreach (var entry in previousEntries)
    {
        if (entries.Contains(entry)) continue;
        
        entries.Add(entry);
        showWarning = true;
    }

    if (showWarning) ConsoleUtils.Warning("Removing entries is not allowed and they have been added back");

    // Prompt close
    if (!entries.All(e => e.IsActive(state)))
    {
        Console.WriteLine();
        if (!ConsoleUtils.PromptClose(true)) return;
    }

    @lock.Entries = entries;

    await ConnectionManager.Connection!.InvokeAsync("EditLockAsync", @lock);
    Console.WriteLine($"Updated lock: {@lock.Name}");
}

async Task RenameLock(LockArgument lockArgument, AddLockArgument nameArgument)
{
    var @lock = lockArgument.Value!;
    string oldName = @lock.Name;
    string newName = nameArgument.Value!;

    await ConnectionManager.Connection!.InvokeAsync("RenameLockAsync", @lock, newName);
    Console.WriteLine($"Renamed lock: {oldName} -> {newName}");
}


// Schedules

async Task AddSchedule(AddScheduleArgument name, TimeArgument start, TimeArgument end, DaysArgument days, EntriesArgument entriesArgument)
{
    var entries = entriesArgument.Value!;

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

async Task EditSchedule(ScheduleArgument scheduleArgument, TimeArgument startArgument, TimeArgument endArgument, 
    DaysArgument daysArgument, EntriesArgument entriesArgument)
{
    StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");

    var schedule = scheduleArgument.Value!;
    var entries = entriesArgument.Value!;
    var previousEntries = schedule.Entries;

    // Revert time changes
    var start = startArgument.Value!;
    var end = endArgument.Value!;

    bool revertStart = schedule.Active && start > schedule.StartTime;
    bool revertEnd = schedule.Active && end < schedule.EndTime;
    bool showedWarnings = false;

    if (revertStart) start = schedule.StartTime;
    if (revertEnd) end = schedule.EndTime;

    if (revertStart || revertEnd) 
    {
        ConsoleUtils.Warning("Making a schedule shorter while it's active is not allowed and changes have been reverted");
        showedWarnings = true;
    }

    // Revert days changes
    List<DayOfWeek> days = [.. schedule.Days];

    foreach (var day in daysArgument.Value!)
    {
        if (!days.Contains(day))
            days.Add(day);
    }

    foreach (var day in days)
    {
        if (daysArgument.Value!.Contains(day)) continue;

        ConsoleUtils.Warning($"Removing days of the week while a schedule is active is not allowed and they have been added back");
        showedWarnings = true;

        break;
    }

    days.Sort();

    // Revert removed entries
    bool active = Schedule.IsActive(start, end, [.. days]);
    bool previousActive = schedule.Active;

    if (active) 
    {
        bool showWarning = false;

        foreach (var entry in previousEntries)
        {
            if (entries.Contains(entry)) continue;
            
            entries.Add(entry);
            showWarning = true;
        }

        if (showWarning) ConsoleUtils.Warning("Removing entries while the schedule is active is not allowed and they have been added back");
        showedWarnings = showedWarnings || showWarning;
    }
    
    // Prompt close
    if (showedWarnings) Console.WriteLine();
    if (active && !entries.All(e => e.IsActive(state)) && !ConsoleUtils.PromptClose(true)) return;

    // Update schedule
    var updatedSchedule = new Schedule
    {
        Name = scheduleArgument.Value!.Name,
        Entries = entries,
        StartTime = start,
        EndTime = end,
        Days = [.. days]
    };

    await ConnectionManager.Connection!.InvokeAsync("EditScheduleAsync", updatedSchedule);
    Console.WriteLine($"Updated schedule: {scheduleArgument.Value.Name}");
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
