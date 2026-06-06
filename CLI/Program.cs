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
    [new ListArgument("list")],
    Block
));

CommandSystem.Register(new Command(
    ["unblock"],
    [new ListArgument("list")],
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
        new ArrayArgument<List, ListArgument>("lists"),
        new TimeArgument("start"),
        new TimeArgument("end"),
        new DaysArgument("days"),
    ],
    AddSchedule
));

CommandSystem.Register(new Command(
    ["schedule", "edit"],
    [
        new ScheduleArgument("name"),
        new ArrayArgument<List, ListArgument>("lists"),
        new TimeArgument("start"),
        new TimeArgument("end"),
        new DaysArgument("days"),
    ],
    EditSchedule,
    true,
    async (command, i) => {
        if (i == 0) return string.Empty;

        var schedule = await ConnectionManager.Connection!.InvokeAsync<Schedule>("GetScheduleFromNameAsync", 
            ((ScheduleArgument)command.Arguments[0]).Value!.Name);

        return i switch {
            1 => string.Join(", ", schedule!.Entries.Select(e => e.Name)),
            2 => schedule!.StartTime.ToString(),
            3 => schedule!.EndTime.ToString(),
            4 => schedule!.Days.GetDaysString(),
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

async Task ShowStatus()
{
    (List<Entry> block, List<Lock> locks, List<List> lists, List<Schedule> schedules) = 
        await ConnectionManager.Connection!.InvokeAsync<(List<Entry>, List<Lock>, List<List>, List<Schedule>)>("GetStateAsync");

    var entries = block
        .Concat(locks.SelectMany(e => e.Entries))
        .Concat(schedules.SelectMany(e => e.Entries))
        .Where(e => e.Type != EntryType.List)
        .ToArray();

    if (entries.Length == 0 && lists.Count == 0 && schedules.Count == 0)
    {
        Console.WriteLine("No lists or schedules found");
        return;
    }

    string[] GetBlockReasons(Entry entry)
    {
        List<string> blockReasons = [];
        if (block.Contains(entry)) blockReasons.Add("manual");

        foreach (var @lock in locks)
        {
            if (@lock.Entries.Contains(entry))
                blockReasons.Add($"🔒 {@lock.UnlockTime}");
        }
            
        foreach (var schedule in schedules)
        {  
            if (schedule.Entries.Contains(entry))
                blockReasons.Add($"⏰ {schedule.Name}");
        }

        return [.. blockReasons];
    }

    // Entries
    foreach (var entry in entries)
    {
        string[] blockReasons = GetBlockReasons(entry);

        string reasonsString = blockReasons.Length == 0 ? "" : $" ({string.Join(", ", blockReasons)})";
        string typeIcon = entry.Type == EntryType.Website ? "🌐" : "💻";
        Console.WriteLine($"{typeIcon}🟢 {entry.Name}{reasonsString}");
    }

    // Lists
    foreach (var list in lists)
    {
        Entry entry = new(EntryType.List, list.Name);
        string[] blockReasons = GetBlockReasons(entry);

        string reasonsString = blockReasons.Length == 0 ? "" : $" ({string.Join(", ", blockReasons)})";
        Console.WriteLine($"📋{(blockReasons.Length > 0 ? "🟢" : "🔴")} {list.Name}{reasonsString}");
    }

    // Schedules
    foreach (var schedule in schedules)
    {
        string timeString = $"({schedule.StartTime} - {schedule.EndTime}, {schedule.Days.GetDaysString()})";
        Console.WriteLine($"⏰{(schedule.Active ? "🟢" : "🔴")} {schedule.Name} {timeString}");
    }
}

async Task ShowVersion() => Console.WriteLine("v0.6.0");

async Task Uninstall() 
{
    var lists = (await ConnectionManager.Connection!.InvokeAsync<List[]>(nameof(CommunicationHub.GetListsAsync))).ToList();
    
    // TODO NOT ALLOW IF BLOCKING IS IN PLACE

    if (ConsoleUtils.PromptYesNo("Remove user data and preferences?", false)) 
        await ConnectionManager.Connection!.InvokeAsync("RemovePreferences");

    await ConnectionManager.Connection!.InvokeAsync("Uninstall");
    Console.WriteLine("Uninstalled successfully");
}

async Task AddList(AddListArgument argument)
{
    var list = new List { Name = argument.Value! };

    (List<Entry> entries, string contents) = await ConsoleUtils.EditEntries("", list.Name);
    list.Entries = entries;

    await ConnectionManager.Connection!.InvokeAsync("AddListAsync", list);
    Console.WriteLine("List created successfully");
}

async Task EditList(ListArgument argument)
{
    var list = argument.Value!;
    (List<Entry> entries, string contents) = await ConsoleUtils.EditEntries("", list.Name); // TODO: get previous entries text

    // TODO close browsers, revert removed entries

/*
    if (list.Active)
    {
        // Close browsers
        foreach (var entry in entries)
        {
            if (list.Entries.Contains(entry)) continue;

            Console.WriteLine();
            if (!ConsoleUtils.PromptClose(true)) return;

            break;
        }

        // Revert removed websites
        bool showWarning = false;

        foreach (var entry in list.Entries)
        {
            if (entries.Contains(entry)) continue;

            entries.Add(entry);
            showWarning = true;
        }

        Console.WriteLine();
        if (showWarning) ConsoleUtils.Warning("Removing websites is not allowed while the list is active");
    }
*/

    list.Entries = entries;

    await ConnectionManager.Connection!.InvokeAsync("EditListAsync", list);
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
    (List<Entry> block, List<Lock> locks, List<List> lists, List<Schedule> schedules) = 
        await ConnectionManager.Connection!.InvokeAsync<(List<Entry>, List<Lock>, List<List>, List<Schedule>)>("GetStateAsync");

    var list = argument.Value!;

    // TODO CHECK IF ACTIVE AND CHECK USAGE!!!

    await ConnectionManager.Connection!.InvokeAsync("RemoveListAsync", list);
    Console.WriteLine($"Removed list: {list.Name}");
}

async Task Block(ListArgument argument)
{
    var list = argument.Value!;

    // TODO WARN LOCKS, SCHEDULES, ALREADY BLOCKED
    // TODO IF NOT ACTIVE, PROMPT CLOSE

    await ConnectionManager.Connection!.InvokeAsync("BlockAsync", list);
    Console.WriteLine($"Enabled manual block: {list.Name}");
}

async Task Unblock(ListArgument argument)
{
    var list = argument.Value!;

    // TODO WARN LOCKS, SCHEDULES, ALREADY UNBLOCKED

    await ConnectionManager.Connection!.InvokeAsync("UnblockAsync", list);
    Console.WriteLine($"Disabled manual block: {list.Name}");
}

async Task Lock(TimeArgument timeArgument)
{
    var time = timeArgument.Value!.ToTimeSpan();
    var unlockTime = DateTime.Now.Add(time);

    (List<Entry> entries, _) = await ConsoleUtils.EditEntries();

    var prompt = $"This will block the provided entries for {time} and close all browsers and all blocked apps. Okay to continue?";
    if (!ConsoleUtils.PromptYesNo(prompt)) return;

    await ConnectionManager.Connection!.InvokeAsync("LockAsync", entries, unlockTime);
    Console.WriteLine($"Locked entries until {unlockTime}");
}

async Task AddSchedule(AddScheduleArgument name, TimeArgument start, TimeArgument end, DaysArgument days)
{
    (List<Entry> entries, string contents) = await ConsoleUtils.EditEntries();

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

async Task EditSchedule(ScheduleArgument schedule, ArrayArgument<List, ListArgument> lists, TimeArgument start, TimeArgument end, DaysArgument days) 
{
    (List<Entry> entries, string contents) = await ConsoleUtils.EditEntries();

    // TODO if active and added entries, close browsers, revert removed entries
    // if it wasnt active before, close browsers anyways!!

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
