namespace SDK;

public class Schedule
{
    public string Name { get; set; } = string.Empty;
    public List<BlockList> BlockLists { get; init; } = [];

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DayOfWeek[] Days { get; set; } = [];
}
