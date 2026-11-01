using Microsoft.AspNetCore.SignalR.Client;
using static CLI.InvokeHelpers;
using Core;
using Daemon;

namespace CLI;

public static class ScheduleCommands
{
    internal static Func<DateTime> Now = () => DateTime.Now;

    public static async Task<int> ShowStatus()
    {
        var state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();
        List<Schedule> schedules = [.. state.Schedules];

        if (schedules.Count == 0)
        {
            Console.WriteLine("No schedules found");
            return 0;
        }

        schedules.Sort((a, b) =>
        {
            int aPriority = a.Active ? 0 : (a.InWarningPeriod ? 1 : 2);
            int bPriority = b.Active ? 0 : (b.InWarningPeriod ? 1 : 2);
            return aPriority.CompareTo(bPriority);
        });

        foreach (var schedule in schedules)
            schedule.PrintStatus();

        return 0;
    }

    public static async Task<int> ShowSchedule(ScheduleArgument argument)
    {
        var schedule = argument.Value!;
        var state = schedule.Active ? "Active" : (schedule.InWarningPeriod ? "Warning" : "Inactive");

        var removal = schedule.RemovalRequestTime == null ? "Not requested"
            : $"{schedule.RemovalRequestTime + TimeSpan.FromDays(1)} - {schedule.RemovalRequestTime + TimeSpan.FromDays(2)}";

        Dictionary<string, object?> values = new() {
            { "name", schedule.Name },
            { "start time", schedule.StartTime },
            { "end time", schedule.EndTime },
            { "days", schedule.Days.GetDaysString() },
            { "entries", string.Join(", ", schedule.Entries.Select(e => e.ToEntryString())) },
            { "warning time", schedule.Options.WarningTime },
            { "state", state },
            { "removal window", removal}
        };

        ConsoleUtils.PrintDictionary(values);
        return 0;
    }

    public static async Task<int> AddSchedule(AddScheduleArgument name, TimeArgument start, TimeArgument end,
        DaysArgument days, EntriesArgument entriesArgument)
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

        if (schedule.Active && !ConsoleUtils.PromptClose()) return 1;

        (await Invoke(nameof(CommunicationHub.AddScheduleAsync), schedule)).Unwrap();
        Console.WriteLine($"Added schedule: {schedule.Name}");

        return 0;
    }

    public static async Task<int> EditSchedule(ScheduleArgument scheduleArgument, List<IFlag> flags)
    {
        var state = (await Invoke<StateSnapshot>(nameof(CommunicationHub.GetSnapshotAsync))).Unwrap();
        var schedule = scheduleArgument.Value!;
        var previousDuration = schedule.Duration;
        var now = Now();

        // Values to modify
        var start = schedule.StartTime;
        var end = schedule.EndTime;
        var entries = schedule.Entries;
        var options = new ScheduleOptions { WarningTime = schedule.Options.WarningTime };
        List<DayOfWeek> days = [.. schedule.Days];

        bool originallyActive = schedule.ActiveAt(now);
        bool originallyWarning = schedule.InWarningPeriodAt(now);
        bool activeOrWarning = originallyActive || originallyWarning;
        bool changesMade = false;

        // A new start or end time can make the window longer even when one moves in the shrinking
        // direction, because a schedule whose end is at or before its start wraps overnight
        if (flags.OfType<ScheduleStartTimeFlag>().FirstOrDefault() is { } startTimeFlag)
        {
            start = startTimeFlag.Value!;
            changesMade = true;
        }

        if (flags.OfType<ScheduleEndTimeFlag>().FirstOrDefault() is { } endTimeFlag)
        {
            end = endTimeFlag.Value!;
            changesMade = true;
        }

        if (activeOrWarning && Schedule.GetDuration(start, end) < previousDuration)
        {
            if (schedule.Active)
                ConsoleUtils.Error("Making a schedule shorter while it's active is not allowed");

            if (schedule.InWarningPeriod)
                ConsoleUtils.Error("Making a schedule shorter while in the warning period is not allowed");

            return 1;
        }

        if (flags.OfType<ScheduleDaysFlag>().FirstOrDefault() is { } daysFlag)
        {
            var previousDays = days.OrderBy(x => x).ToList();

            if (!activeOrWarning)
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

                if (schedule.Active)
                    ConsoleUtils.Error($"Removing days of the week while a schedule is active is not allowed");

                if (schedule.InWarningPeriod)
                    ConsoleUtils.Error($"Removing days of the week while in the warning period is not allowed");

                return 1;
            }

        End:
            days = [.. days.Distinct()];
            days.Sort();

            changesMade = changesMade || !days.SequenceEqual(previousDays);
        }

        if (originallyActive && !Schedule.IsActive(start, end, [.. days], now))
        {
            ConsoleUtils.Error("Moving a schedule to unblock it while it's active is not allowed");
            return 1;
        }

        if (activeOrWarning && !Schedule.PreservesProtectedWindow(schedule,
            new Schedule { StartTime = start, EndTime = end, Days = [.. days], Entries = schedule.Entries }, now))
        {
            ConsoleUtils.Error("Moving or shortening an active or warned schedule window is not allowed");
            return 1;
        }

        if (flags.OfType<ScheduleWarningTimeFlag>().FirstOrDefault() is { } warningTimeFlag)
        {
            var newTime = warningTimeFlag.Value!;

            if (newTime < options.WarningTime && activeOrWarning)
            {
                if (schedule.Active)
                    ConsoleUtils.Error("Decreasing the warning time while the schedule is active is not allowed");

                if (schedule.InWarningPeriod)
                    ConsoleUtils.Error("Decreasing the warning time while in the warning period is not allowed");

                return 1;
            }
            else
            {
                changesMade = changesMade || options.WarningTime != newTime;
                options.WarningTime = newTime;
            }
        }

        if (flags.OfType<ScheduleEntriesFlag>().FirstOrDefault() is { } entriesFlag)
        {
            // Revert removed entries
            var previousEntries = entries;
            entries = entriesFlag.Value!;

            bool active = Schedule.IsActive(start, end, [.. days], now);

            if (activeOrWarning)
            {
                bool showWarning = false;

                foreach (var entry in previousEntries)
                {
                    if (entries.Contains(entry)) continue;

                    entries.Add(entry);
                    showWarning = true;
                }

                if (showWarning)
                {
                    if (schedule.Active)
                        ConsoleUtils.Warning("Removing entries while the schedule is active is not allowed and they have been added back");

                    if (schedule.InWarningPeriod)
                        ConsoleUtils.Warning("Removing entries while in the warning period is not allowed and they have been added back");

                    Console.WriteLine();
                }
            }

            // Prompt close
            if (active && !entries.All(e => e.IsActive(state)) && !ConsoleUtils.PromptClose(true)) return 1;

            changesMade = changesMade || !entries.SequenceEqual(previousEntries);
        }

        // Update schedule
        var updatedSchedule = new Schedule
        {
            Name = scheduleArgument.Value!.Name,
            Entries = entries,
            StartTime = start,
            EndTime = end,
            Days = [.. days],
            Options = options,
            RemovalRequestTime = schedule.RemovalRequestTime,
            LastNotificationTime = schedule.LastNotificationTime
        };

        (await Invoke(nameof(CommunicationHub.EditScheduleAsync), updatedSchedule)).Unwrap();

        if (changesMade) Console.WriteLine($"Updated schedule: {scheduleArgument.Value.Name}");
        else Console.WriteLine("No changes made");

        return 0;
    }

    public static async Task<int> RenameSchedule(ScheduleArgument scheduleArgument, AddScheduleArgument nameArgument)
    {
        var schedule = scheduleArgument.Value!;
        string oldName = schedule.Name;
        string newName = nameArgument.Value!;

        (await Invoke(nameof(CommunicationHub.RenameScheduleAsync), schedule, newName)).Unwrap();
        Console.WriteLine($"Renamed schedule: {oldName} -> {newName}");

        return 0;
    }

    public static async Task<int> RemoveSchedule(ScheduleArgument argument)
    {
        var schedule = argument.Value!;
        bool inWarningPeriod = schedule.InWarningPeriod;

        if (schedule.Active || inWarningPeriod)
        {
            var now = Now();
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
                    (await Invoke(nameof(CommunicationHub.RequestScheduleRemovalAsync), schedule)).Unwrap();
                    Console.WriteLine($"Requested schedule removal: {schedule.Name}");

                    return 0;
                }

                return 1;
            }

            // Requested
            if (!inWindow && requested)
            {
                if (!inWarningPeriod) ConsoleUtils.Error($"Removing schedules while they're active is not allowed: {schedule.Name}");
                else ConsoleUtils.Error($"Removing schedules while in the warning period is not allowed: {schedule.Name}");

                var windowStart = ((DateTime)schedule.RemovalRequestTime!).AddDays(1);
                ConsoleUtils.Note($"The removal of this schedule is already requested. The window starts {windowStart}.");

                return 1;
            }

            // In window: continue
        }

        (await Invoke(nameof(CommunicationHub.RemoveScheduleAsync), schedule)).Unwrap();
        Console.WriteLine($"Removed schedule: {schedule.Name}");

        return 0;
    }

}
