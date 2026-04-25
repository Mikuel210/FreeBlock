namespace SDK;

public class Schedule : IStateObject
{

    public string Name { get; set; } = string.Empty;
    public List<BlockList> BlockLists { get; init; } = [];

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DayOfWeek[] Days { get; set; } = [];

    public bool Active => IsActive(StartTime, EndTime, Days);

    public static bool IsActive(TimeOnly startTime, TimeOnly endTime, DayOfWeek[] days)
    {
        var now = DateTime.Now;
        bool isDay;

        // Check day
        if (endTime >= startTime || now.TimeOfDay >= startTime.ToTimeSpan()) isDay = days.Contains(now.DayOfWeek);
        else isDay = days.Contains(now.AddDays(1).DayOfWeek);

        // Check time
        bool afterStartTime = now.TimeOfDay >= startTime.ToTimeSpan();
        bool beforeEndTime = now.TimeOfDay < endTime.ToTimeSpan();
        bool isTime = endTime >= startTime ? afterStartTime && beforeEndTime : afterStartTime || beforeEndTime;

        return isDay && isTime;
    }

}
