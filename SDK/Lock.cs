namespace SDK;

public class Lock(string name, List<Entry> entries, DateTime unlockTime) : IName 
{

    public string Name { get; set; } = name;
    public List<Entry> Entries { get; set; } = entries;
    public DateTime UnlockTime { get; set; } = unlockTime;

}