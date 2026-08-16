namespace Core;

public enum EntryType 
{
    Website,
    App,
    List
}

public class Entry(EntryType type, string name)
{

    public EntryType Type { get; set; } = type;
    public string Name { get; set; } = name;

    public override bool Equals(object? other)
    {
        if (other is not Entry entry) return false;
        return entry.Type == Type && entry.Name == Name;
    }

    public override int GetHashCode()
        => unchecked(Type.GetHashCode() + Name.GetHashCode());

    public bool IsActive(StateSnapshot state)
    {
        if (state.Block.Contains(this)) return true;

        foreach (var @lock in state.Locks)
        {
            if (@lock.Entries.Contains(this))
                return true;
        }

        foreach (var list in state.Lists.Where(e => e.IsActive(state)))
        {
            if (list.Entries.Contains(this))
                return true;
        }
            
        foreach (var schedule in state.Schedules.Where(e => e.Active))
        {  
            if (schedule.Entries.Contains(this))
                return true;
        }

        return false;
    }

}
