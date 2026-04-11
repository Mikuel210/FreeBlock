using Newtonsoft.Json;

namespace SDK;

public class BlockList
{

    public string Name { get; set; } = string.Empty;
    public List<string> UrlList { get; init; } = [];
    public bool Enabled { get; set; }

    [JsonIgnore] public bool Locked => UnlockTime != null && UnlockTime > DateTime.Now;
    public DateTime? UnlockTime { get; set; }

    public List<Schedule> Schedules { get; init; } = [];

}

public class Schedule
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DayOfWeek[] Days { get; set; } = [];
}
