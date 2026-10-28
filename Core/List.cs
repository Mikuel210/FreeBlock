namespace Core;

public class List : IName
{

    public string Name { get; set; } = string.Empty;
    public List<Entry> Entries { get; set; } = [];

    public bool IsActive(StateSnapshot state, HashSet<string>? visited = null)
    {
        visited ??= [];
        if (!visited.Add(Name)) return false;

        var entry = new Entry(EntryType.List, Name);

        bool active = state.Block.Contains(entry)
            || state.Locks.SelectMany(e => e.Entries).Contains(entry)
            || state.Schedules.Where(e => e.Active).SelectMany(e => e.Entries).Contains(entry)
            || state.Lists.Any(e => e.Entries.Contains(entry) && e.IsActive(state, visited));

        visited.Remove(Name);
        return active;
    }

}
