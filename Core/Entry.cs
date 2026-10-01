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

    public bool IsRecursive(Func<string, List?> listFromName, HashSet<string> visited)
    {
        if (Type != EntryType.List) return false;
        if (visited.Contains(Name)) return true;

        var list = listFromName(Name);
        if (list is null) return false;

        visited.Add(Name);
        return list.Entries.Any(e => e.IsRecursive(listFromName, visited));
    }

    public string[] GetBlockReasons(StateSnapshot state)
    {
        List<string> blockReasons = [];
        if (state.Block.Contains(this)) blockReasons.Add("manual");

        foreach (var @lock in state.Locks)
        {
            if (@lock.Entries.Contains(this))
                blockReasons.Add($"🔒 {@lock.Name}");
        }

        foreach (var list in state.Lists.Where(e => e.IsActive(state)))
        {
            if (list.Entries.Contains(this))
                blockReasons.Add($"📋 {list.Name}");
        }

        foreach (var schedule in state.Schedules.Where(e => e.Active))
        {
            if (schedule.Entries.Contains(this))
                blockReasons.Add($"⏰ {schedule.Name}");
        }

        return [.. blockReasons];
    }

    public string ToEntryString()
    {
        string prefix = Type switch
        {
            EntryType.Website => "",
            EntryType.App => "+",
            EntryType.List => "@",
            _ => throw new NotImplementedException(),
        };

        return prefix + Name;
    }

    public void PrintStatus(StateSnapshot state)
    {
        string[] blockReasons = GetBlockReasons(state);
        string reasonsString = blockReasons.Length == 0 ? "" : $" ({string.Join(", ", blockReasons)})";

        string typeIcon = Type switch
        {
            EntryType.Website => "🌐",
            EntryType.App => "💻",
            _ => "📋"
        };

        Console.WriteLine($"{typeIcon}{(blockReasons.Length > 0 ? "🟢" : "🔴")} {Name}{reasonsString}");
    }

}
