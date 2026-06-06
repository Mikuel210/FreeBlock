namespace SDK;

public enum EntryType 
{
    Website,
    App,
    List
}

public record Entry(EntryType Type, string Name);
