using Microsoft.AspNetCore.SignalR.Client;
using Daemon;
using SDK;
using CLI;

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
    [new ListArgument("list"), new TimeArgument("time")],
    Lock
));

CommandSystem.Register(new Command(
    ["schedule", "add"],
    [
        new AddScheduleArgument("name"),
        new ArrayArgument<BlockList, ListArgument>("lists"),
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
        new ArrayArgument<BlockList, ListArgument>("lists"),
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
            1 => string.Join(", ", schedule!.BlockLists.Select(e => e.Name)),
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
    var lists = await ConnectionManager.Connection!.InvokeAsync<BlockList[]>(nameof(CommunicationHub.GetBlockListsAsync));
    var schedules = await ConnectionManager.Connection!.InvokeAsync<Schedule[]>(nameof(CommunicationHub.GetSchedulesAsync));

    if (lists.Length == 0 && schedules.Length == 0)
    {
        Console.WriteLine("No lists or schedules found");
        return;
    }

    // Lists
    foreach (var list in lists)
    {
        List<string> blockReasons = [];
        if (list.ManuallyBlocked) blockReasons.Add("manual");
        if (list.Locked) blockReasons.Add($"🔒 {list.UnlockTime}");

        if (list.Scheduled)
        {
            var blockingSchedules = schedules.Where(e => e.BlockLists.Select(e => e.Name).Contains(list.Name) && e.Active);
            blockReasons.Add($"⏰ {string.Join(", ", blockingSchedules.Select(e => e.Name))}");
        }

        string reasonsString = blockReasons.Count == 0 ? "" : $" ({string.Join(", ", blockReasons)})";
        Console.WriteLine($"📋{(list.Active ? "🟢" : "🔴")} {list.Name}{reasonsString}");
    }

    // Schedules
    foreach (var schedule in schedules)
    {
        string timeString = $"({schedule.StartTime} - {schedule.EndTime}, {schedule.Days.GetDaysString()})";
        Console.WriteLine($"⏰{(schedule.Active ? "🟢" : "🔴")} {schedule.Name} {timeString}");
    }
}

async Task Uninstall() 
{
    var lists = (await ConnectionManager.Connection!.InvokeAsync<BlockList[]>(nameof(CommunicationHub.GetBlockListsAsync))).ToList();
    
    if (lists.Any(e => e.Active)) 
    {
        ConsoleUtils.Error("To prevent impulsive choices, FreeBlock can't be uninstalled while any block lists are active");
        return;
    }

    if (ConsoleUtils.PromptYesNo("Remove user data and preferences?", false)) 
        await ConnectionManager.Connection!.InvokeAsync("RemovePreferences");

    await ConnectionManager.Connection!.InvokeAsync("Uninstall");
    Console.WriteLine("Uninstalled successfully");
}

async Task AddList(AddListArgument argument)
{
    var list = new BlockList
    {
        Name = argument.Value!
    };

    Console.WriteLine("Waiting for your editor to close the file...");
    await ConsoleUtils.EditList(list);

    await ConnectionManager.Connection!.InvokeAsync("AddListAsync", list);
    Console.WriteLine("List created successfully");
}

async Task EditList(ListArgument argument)
{
    var list = argument.Value!;
    var previousEntries = list.Entries.ToList();

    Console.WriteLine("Waiting for your editor to close the file...");
    await ConsoleUtils.EditList(list);

    if (list.Active)
    {
        // Close browsers
        foreach (string entry in list.Entries)
        {
            if (previousEntries.Contains(entry)) continue;

            Console.WriteLine();
            if (!ConsoleUtils.PromptClose(true)) return;

            break;
        }

        // Revert removed websites
        bool showWarning = false;

        foreach (string entry in previousEntries)
        {
            if (list.Entries.Contains(entry)) continue;

            list.Entries.Add(entry);
            showWarning = true;
        }

        Console.WriteLine();
        if (showWarning) ConsoleUtils.Warning("Removing websites is not allowed while the list is active");
    }

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
    var list = argument.Value!;

    var schedules = (await ConnectionManager.Connection!
        .InvokeAsync<Schedule[]>(nameof(CommunicationHub.GetSchedulesAsync)))
        .Where(e => e.BlockLists
            .Select(e => e.Name)
            .Contains(list.Name));

    if (list.Active)
    {
        ConsoleUtils.Error($"Removing lists while they're active is not allowed: {list.Name}");
        return;
    }

    if (schedules.Any())
    {
        ConsoleUtils.Error($"The list is being used by {(schedules.Count() == 1 ? "an schedule" : "schedules")}: {string.Join(", ", schedules.Select(e => e.Name))}");
        return;
    }

    await ConnectionManager.Connection!.InvokeAsync("RemoveListAsync", list);
    Console.WriteLine($"Removed list: {list.Name}");
}

async Task Block(ListArgument argument)
{
    var list = argument.Value!;

    if (list.Locked) ConsoleUtils.Note($"The list was already active as it's locked until {list.UnlockTime}");

    if (list.Scheduled)
    {
        var schedules = (await ConnectionManager.Connection!
            .InvokeAsync<Schedule[]>(nameof(CommunicationHub.GetSchedulesAsync)))
            .Where(e => e.BlockLists
                .Select(e => e.Name)
                .Contains(list.Name) && e.Active)
            .Select(e => e.Name)
            .ToList();

        if (schedules.Count == 1) ConsoleUtils.Warning($"The list remains blocked by an active schedule: {schedules.First()}");
        else if (schedules.Count != 0) ConsoleUtils.Warning($"The list remains blocked by active schedules: {string.Join(", ", schedules)}");
    }

    if (list.ManuallyBlocked)
    {
        Console.WriteLine($"Manual block is already enabled: {list.Name}");
        return;
    }

    if (!list.Active && !ConsoleUtils.PromptClose()) return;

    await ConnectionManager.Connection!.InvokeAsync("BlockAsync", list);
    Console.WriteLine($"Enabled manual block: {list.Name}");
}

async Task Unblock(ListArgument argument)
{
    var list = argument.Value!;
    if (list.Locked) ConsoleUtils.Warning($"The list remains active as it's locked until {list.UnlockTime}");

    if (list.Scheduled)
    {
        var schedules = (await ConnectionManager.Connection!
            .InvokeAsync<Schedule[]>(nameof(CommunicationHub.GetSchedulesAsync)))
            .Where(e => e.BlockLists
                .Select(e => e.Name)
                .Contains(list.Name) && e.Active)
            .Select(e => e.Name)
            .ToList();

        if (schedules.Count == 1) ConsoleUtils.Warning($"The list remains blocked by an active schedule: {schedules.First()}");
        else if (schedules.Count != 0) ConsoleUtils.Warning($"The list remains blocked by active schedules: {string.Join(", ", schedules)}");
    }

    if (!list.ManuallyBlocked)
    {
        Console.WriteLine($"Manual block is already disabled: {list.Name}");
        return;
    }

    await ConnectionManager.Connection!.InvokeAsync("UnblockAsync", list);
    Console.WriteLine($"Disabled manual block: {list.Name}");
}

async Task Lock(ListArgument listArgument, TimeArgument timeArgument)
{
    var list = listArgument.Value!;
    var time = timeArgument.Value!.ToTimeSpan();
    var unlockTime = DateTime.Now.Add(time);

    if (list.UnlockTime != null && list.UnlockTime > unlockTime)
    {
        ConsoleUtils.Error($"List is already locked until {list.UnlockTime}: {list.Name}");
        return;
    }

    var prompt = $"This will block {list.Name} for {time}";
    if (!list.Active) prompt += " and close all browsers and all blocked apps";
    prompt += ". Okay to continue?";

    if (!ConsoleUtils.PromptYesNo(prompt)) return;
    await ConnectionManager.Connection!.InvokeAsync("LockAsync", list, unlockTime);

    Console.WriteLine($"Locked list for {time}: {list.Name}");
}

async Task AddSchedule(AddScheduleArgument name, ArrayArgument<BlockList, ListArgument> lists, TimeArgument start, TimeArgument end, DaysArgument days)
{
    var schedule = new Schedule
    {
        Name = name.Value!,
        BlockLists = [.. lists.Value!],
        StartTime = start.Value,
        EndTime = end.Value,
        Days = days.Value!
    };

    if (schedule.Active && !ConsoleUtils.PromptClose()) return;

    await ConnectionManager.Connection!.InvokeAsync("AddScheduleAsync", schedule);
    Console.WriteLine($"Added schedule: {schedule.Name}");
}

async Task EditSchedule(ScheduleArgument schedule, ArrayArgument<BlockList, ListArgument> lists, TimeArgument start, TimeArgument end, DaysArgument days) {
    var updatedSchedule = new Schedule
    {
        Name = schedule.Value!.Name,
        BlockLists = [.. lists.Value!],
        StartTime = start.Value,
        EndTime = end.Value,
        Days = days.Value!
    };

    if (updatedSchedule.Active && !ConsoleUtils.PromptClose()) return;

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
