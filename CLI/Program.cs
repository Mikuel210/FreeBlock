using Microsoft.AspNetCore.SignalR.Client;
using SDK;
using CLI;

#region Command System

CommandSystem.Register(new Command(
    [],
    [],
    ShowUsage
));

CommandSystem.Register(new Command(
    ["help"],
    [],
    ShowHelp
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

/*
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
*/

await CommandSystem.Handle(args);

#endregion

#region Commands

void ShowUsage()
{
    Console.WriteLine("""
                      Usage: freeblock [command]
                      See: freeblock help
                      """);
}

void ShowHelp()
{
    Console.WriteLine("""
                      Usage: freeblock [command]

                      Available commands:
                      freeblock help          Show the help dialog
                      freeblock status        Show the current status of block lists
                      freeblock list add      Create a new block list
                      freeblock list edit     Edit a block list
                      freeblock list rename   Rename a block list
                      freeblock list remove   Delete a block list
                      freeblock block         Manually block a list
                      freeblock unblock       Manually unblock list
                      freeblock lock          Block a list for the provided amount of time
                      """);

    // freeblock schedule add [name] [lists] [start] [end] [MTWHFSU/weekdays/weekends/all]
    // freeblock schedule remove [name]

}

async Task ShowStatus()
{
    var lists = await ConnectionManager.Connection!.InvokeAsync<BlockList[]>("GetBlockListsAsync");
    var schedules = await ConnectionManager.Connection!.InvokeAsync<Schedule[]>("GetSchedulesAsync");

    // Lists
    //Console.WriteLine("Lists:");

    if (lists.Length == 0)
        Console.WriteLine("No lists found");

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
        Console.WriteLine($"{(list.Active ? "🟢" : "🔴")} {list.Name}{reasonsString}");
    }
    /*
    Console.WriteLine();

    // Schedules
    Console.WriteLine("Schedules:");

    if (schedules.Length == 0)
        Console.WriteLine("No schedules found");

    foreach (var schedule in schedules)
    {
        string daysString;

        // Get days strings
        if (schedule.Days.SequenceEqual(Enum.GetValues<DayOfWeek>())) daysString = "all";
        else if (schedule.Days.SequenceEqual([DayOfWeek.Saturday, DayOfWeek.Sunday])) daysString = "weekdays";

        else if (schedule.Days.SequenceEqual([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]))
            daysString = "weekdays";

        else daysString = string.Join("", schedule.Days.Select(e => e switch
        {
            DayOfWeek.Monday => "M",
            DayOfWeek.Tuesday => "T",
            DayOfWeek.Wednesday => "W",
            DayOfWeek.Thursday => "H",
            DayOfWeek.Friday => "F",
            DayOfWeek.Saturday => "S",
            DayOfWeek.Sunday => "U",
            _ => throw new NotImplementedException(),
        }));

        string timeString = $"({schedule.StartTime} - {schedule.EndTime}, {daysString})";
        Console.WriteLine($"{(schedule.Active ? "🟢" : "🔴")} {schedule.Name} {timeString}");
    }
    */
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
    var previousUrls = list.UrlList.ToList();

    Console.WriteLine("Waiting for your editor to close the file...");
    await ConsoleUtils.EditList(list);

    if (list.Active)
    {
        // Close browsers
        foreach (string url in list.UrlList)
        {
            if (previousUrls.Contains(url)) continue;

            Console.WriteLine();
            if (!ConsoleUtils.PromptYesNo("This will close all browser windows to refresh blocking. Okay to continue?", true, true)) return;

            break;
        }

        // Revert removed websites
        bool showWarning = false;

        foreach (string url in previousUrls)
        {
            if (list.UrlList.Contains(url)) continue;

            list.UrlList.Add(url);
            showWarning = true;
        }

        Console.WriteLine();
        if (showWarning) ConsoleUtils.Warning("Removing websites is not allowed while the list is enabled");
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

    if (list.Locked)
    {
        ConsoleUtils.Error($"The list is locked until {list.UnlockTime}");
        return;
    }

    if (list.Scheduled)
    {
        ConsoleUtils.Error("The list is blocked by a schedule");
        return;
    }

    await ConnectionManager.Connection!.InvokeAsync("RemoveListAsync", list);
    Console.WriteLine($"Removed list: {list.Name}");
}

async Task Block(ListArgument argument)
{
    var list = argument.Value!;

    if (list.Locked) ConsoleUtils.Note($"The list was already active as it's locked until {list.UnlockTime}");
    if (list.Scheduled) ConsoleUtils.Note("The list was already blocked by an active schedule");

    if (list.ManuallyBlocked)
    {
        Console.WriteLine($"Manual block is already enabled: {list.Name}");
        return;
    }

    if (!list.Active && !ConsoleUtils.PromptYesNo("This will close all browser windows to refresh blocking. Okay to continue?", true)) return;

    await ConnectionManager.Connection!.InvokeAsync("BlockAsync", list);
    Console.WriteLine($"Enabled manual block: {list.Name}");
}

async Task Unblock(ListArgument argument)
{
    var list = argument.Value!;

    if (list.Locked) ConsoleUtils.Warning($"The list remains active as it's locked until {list.UnlockTime}");
    if (list.Scheduled) ConsoleUtils.Warning("The list remains blocked by an active schedule");

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

    var prompt = $"This will block {list.Name} for {time} ";
    prompt += "and close all browser windows to refresh blocking. Okay to continue?";

    if (!ConsoleUtils.PromptYesNo(prompt, true)) return;
    await ConnectionManager.Connection!.InvokeAsync("LockAsync", list, unlockTime);

    Console.WriteLine($"Locked list for {time}: {list.Name}");
}

/*async Task AddSchedule(AddScheduleArgument name, ArrayArgument<BlockList, ListArgument> lists, TimeArgument start, TimeArgument end, DaysArgument days)
{
    var schedule = new Schedule
    {
        Name = name.Value!,
        BlockLists = lists.Value!.ToList(),
        StartTime = start.Value,
        EndTime = end.Value,
        Days = days.Value!
    };

    await ConnectionManager.Connection!.InvokeAsync("AddScheduleAsync", schedule);
    Console.WriteLine($"Added schedule: {schedule.Name}");
}*/

#endregion
