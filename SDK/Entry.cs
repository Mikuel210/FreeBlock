namespace SDK;

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
    {
        return Type.GetHashCode() + Name.GetHashCode();
    }

}
