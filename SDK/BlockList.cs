namespace SDK;

public class BlockList : IStateObject
{
    public string Name { get; set; } = string.Empty;
    public List<string> UrlList { get; init; } = [];
    public bool ManuallyBlocked { get; set; }
    public bool Active => ManuallyBlocked || Locked || Scheduled;

    // Locks
    public bool Locked => UnlockTime != null && UnlockTime > DateTime.Now;
    public DateTime? UnlockTime { get; set; }

    // Schedules
    public bool Scheduled { get; set; }
}
