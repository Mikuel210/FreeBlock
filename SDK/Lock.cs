namespace SDK;

public record Lock(List<Entry> Entries, DateTime UnlockTime);