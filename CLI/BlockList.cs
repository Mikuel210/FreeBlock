using Newtonsoft.Json;

namespace CLI;

public class BlockList
{

    public string Name { get; init; } = string.Empty;
    public List<string> UrlList { get; init; } = [];
    public bool Enabled { get; set; }

    [JsonIgnore] public bool Locked => UnlockTime != null && UnlockTime > DateTime.Now;
    public DateTime? UnlockTime { get; set; }

    public List<Schedule> Schedules { get; init; } = [];

    public static BlockList? FromName(string name) =>
        Config.BlockLists.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.InvariantCultureIgnoreCase));

}

public class Schedule
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DayOfWeek[] Days { get; set; } = [];
}
