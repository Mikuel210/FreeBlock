using Microsoft.AspNetCore.SignalR.Client;
using Core;

namespace CLI;

public static class ScheduleCommands
{

    public static async Task AddSchedule(AddScheduleArgument name, TimeArgument start, TimeArgument end, DaysArgument days, EntriesArgument entriesArgument)
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

    public static async Task EditSchedule(ScheduleArgument scheduleArgument, List<IFlag> flags)
    {
        StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");

        var schedule = scheduleArgument.Value!;
        var start = schedule.StartTime;
        var end = schedule.EndTime;
        var days = schedule.Days.ToList();
        var entries = schedule.Entries;

        bool changesMade = false;
        bool showedWarnings = false;
        bool revertTime = false;

        if (flags.OfType<ScheduleStartTimeFlag>().FirstOrDefault() is { } startTimeFlag)
        {
            bool revertStart = schedule.Active && start > schedule.StartTime;
            if (!revertStart) start = startTimeFlag.Value!;

            revertTime = revertStart;
            changesMade = true;
        }

        if (flags.OfType<ScheduleEndTimeFlag>().FirstOrDefault() is { } endTimeFlag)
        {
            bool revertEnd = schedule.Active && end < schedule.EndTime;
            if (!revertEnd) end = endTimeFlag.Value!;

            revertTime = revertTime || revertEnd;
            changesMade = true;
        }

        if (flags.OfType<ScheduleDaysFlag>().FirstOrDefault() is { } daysFlag)
        {
            if (!schedule.Active)
            {
                days = [.. daysFlag.Value!];
                goto End;
            }

            // Revert removed days
            foreach (var day in daysFlag.Value!)
            {
                if (!days.Contains(day))
                    days.Add(day);
            }

            foreach (var day in days)
            {
                if (daysFlag.Value!.Contains(day)) continue;

                ConsoleUtils.Warning($"Removing days of the week while a schedule is active is not allowed and they have been added back");
                showedWarnings = true;

                break;
            }

            End:
            days.Sort();
            changesMade = true;
        }

        if (flags.OfType<ScheduleEntriesFlag>().FirstOrDefault() is { } entriesFlag)
        {
            // Revert removed entries
            var previousEntries = entries;
            entries = entriesFlag.Value!;
            
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

            changesMade = true;
        }

        if (revertTime) 
        {
            ConsoleUtils.Warning("Making a schedule shorter while it's active is not allowed and changes have been reverted");
            showedWarnings = true;
        }

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

        if (changesMade) Console.WriteLine($"Updated schedule: {scheduleArgument.Value.Name}");
        else Console.WriteLine("No changes made");
    }

    public static async Task RenameSchedule(ScheduleArgument scheduleArgument, AddScheduleArgument nameArgument)
    {
        var schedule = scheduleArgument.Value!;
        string oldName = schedule.Name;
        string newName = nameArgument.Value!;

        await ConnectionManager.Connection!.InvokeAsync("RenameScheduleAsync", schedule, newName);
        Console.WriteLine($"Renamed schedule: {oldName} -> {newName}");
    }

    public static async Task RemoveSchedule(ScheduleArgument argument)
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

}