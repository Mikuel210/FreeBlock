namespace SDK;

public class Schedule : IName
{
    public string Name { get; set; } = string.Empty;
    public List<BlockList> BlockLists { get; init; } = [];

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DayOfWeek[] Days { get; set; } = [];

    public bool Active
    {
        get
        {
            var now = DateTime.Now;
            bool isDay;

            // Check day
            if (EndTime >= StartTime || now.TimeOfDay >= StartTime.ToTimeSpan()) isDay = Days.Contains(now.DayOfWeek);
            else isDay = Days.Contains(now.AddDays(1).DayOfWeek);

            // Check time
            bool afterStartTime = now.TimeOfDay >= StartTime.ToTimeSpan();
            bool beforeEndTime = now.TimeOfDay < EndTime.ToTimeSpan();
            bool isTime = EndTime >= StartTime ? afterStartTime && beforeEndTime : afterStartTime || beforeEndTime;

            return isDay && isTime;
        }
    }
}
