using Microsoft.AspNetCore.SignalR.Client;
using Core;
using Microsoft.AspNetCore.Mvc.Diagnostics;

namespace CLI;

public static class ScheduleCommands
{

    public static async Task ShowStatus()
    {
        StateSnapshot state = await ConnectionManager.Connection!.InvokeAsync<StateSnapshot>("GetSnapshotAsync");
        List<Schedule> schedules = [.. state.Schedules];

        if (schedules.Count == 0)
        {
            Console.WriteLine("No schedules found");
            return;
        }

        schedules.Sort((a, b) => {
            int aPriority = a.Active ? 0 : (a.InWarningPeriod ? 1 : 2);
            int bPriority = b.Active ? 0 : (b.InWarningPeriod ? 1 : 2);
            return aPriority.CompareTo(bPriority);
        });

        foreach (var schedule in schedules)
        {
            string timeString = $"({schedule.StartTime} - {schedule.EndTime}, {schedule.Days.GetDaysString()})";
            string icon = schedule.Active ? "🟢" : (schedule.InWarningPeriod ? "🟠" : "🔴");
            Console.WriteLine($"⏰{icon} {schedule.Name} {timeString}");
        }
    }

    public static async Task ShowSchedule(ScheduleArgument argument)
    {
        var schedule = argument.Value!;
        var state = schedule.Active ? "Active" : (schedule.InWarningPeriod ? "Warning" : "Inactive");

        Dictionary<string, object?> values = new() {
            { "Name", schedule.Name },
            { "Start time", schedule.StartTime },
            { "End time", schedule.EndTime },
            { "Days", schedule.Days.GetDaysString() },
            { "Entries", string.Join(", ", schedule.Entries.Select(e => e.ToEntryString())) },
            { "Warning time", schedule.Options.WarningTime },
            { "State", state }
        };

        int spaces = values.Keys.Max(e => e.Length) + 3;

        foreach (var value in values)
        {
            var spaceString = new string(' ', spaces - value.Key.Length);
            Console.WriteLine($"{value.Key}{spaceString}{value.Value}");
        }
    }

    public static async Task AddSchedule(AddScheduleArgument name, TimeArgument start, TimeArgument end, DaysArgument days, EntriesArgument entriesArgument)
    {
        var entries = entriesArgument.Value!;

        var schedule = new Schedule
        {
            Name = name.Value!,
            Entries = entries,
            StartTime = start.Value,
            EndTime = end.Value,
            Days = days.Value!,
            Options = {
                WarningTime = await ConfigSystem.Get<TimeSpan>("schedule.defaultWarningTime")
            }
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
        var options = schedule.Options;

        bool changesMade = false;
        bool showedWarnings = false;
        bool revertTime = false;

        if (flags.OfType<ScheduleStartTimeFlag>().FirstOrDefault() is { } startTimeFlag)
        {
            bool revertStart = schedule.Active && startTimeFlag.Value > schedule.StartTime;
            if (!revertStart) start = startTimeFlag.Value!;

            revertTime = revertStart;
            changesMade = true;
        }

        if (flags.OfType<ScheduleEndTimeFlag>().FirstOrDefault() is { } endTimeFlag)
        {
            bool revertEnd = schedule.Active && endTimeFlag.Value < schedule.EndTime;
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

        if (flags.OfType<ScheduleWarningTimeFlag>().FirstOrDefault() is { } warningTimeFlag)
        {
            var newTime = warningTimeFlag.Value!;
            bool inWarningPeriod = schedule.InWarningPeriod;

            if (newTime < options.WarningTime && (schedule.Active || inWarningPeriod))
            {
                if (inWarningPeriod) ConsoleUtils.Warning("Decreasing the warning time while in the warning period is not allowed");
                else ConsoleUtils.Warning("Decreasing the warning time while the schedule is active is not allowed");

                showedWarnings = true;
            }
            else
            {
                options.WarningTime = newTime;
                changesMade = true;
            }
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
            Days = [.. days],
            Options = options
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
        bool inWarningPeriod = schedule.InWarningPeriod;

        if (schedule.Active || inWarningPeriod)
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
                if (!inWarningPeriod) ConsoleUtils.Error($"Removing schedules while they're active is not allowed: {schedule.Name}");
                else ConsoleUtils.Error($"Removing schedules while in the warning period is not allowed: {schedule.Name}");

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
                if (!inWarningPeriod) ConsoleUtils.Error($"Removing schedules while they're active is not allowed: {schedule.Name}");
                else ConsoleUtils.Error($"Removing schedules while in the warning period is not allowed: {schedule.Name}");

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