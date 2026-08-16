namespace Core;

public class List : IName
{

    public string Name { get; set; } = string.Empty;
    public List<Entry> Entries { get; set; } = [];

    public bool IsActive(StateSnapshot state, string[]? traceback = null)
    {
        traceback ??= [];
        traceback = [.. traceback, Name];

        var entry = new Entry(EntryType.List, Name);
        var lists = state.Lists.Where(e => !traceback.Contains(e.Name) && e.IsActive(state, [.. traceback])).ToList();

        return state.Block.Contains(entry) ||
            state.Locks.SelectMany(e => e.Entries).Contains(entry) ||
            state.Schedules.Where(e => e.Active).SelectMany(e => e.Entries).Contains(entry) ||
            lists.SelectMany(e => e.Entries).Contains(entry);
    }

}
