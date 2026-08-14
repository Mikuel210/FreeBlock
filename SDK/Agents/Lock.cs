namespace SDK;

public class Lock(string name, List<Entry> entries, DateTime unlockTime) : IAgent<AgentOptions>
{

    public string Name { get; set; } = name;
    public AgentOptions Options { get; } = new();

    public List<Entry> Entries { get; set; } = entries;
    public DateTime UnlockTime { get; set; } = unlockTime;

}
