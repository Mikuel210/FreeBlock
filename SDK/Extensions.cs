namespace SDK;

public static class Extensions
{

    public static List<Entry> ResolveLists(this List<Entry> entries, Func<string, List> listFromName, List<string>? traceback = null)
    {
        List<Entry> output = [];
        traceback ??= [];

        foreach (var entry in entries)
        {
            if (entry.Type != EntryType.List)
            {
                output.Add(entry);
                continue;
            }

            if (traceback.Contains(entry.Name)) 
                throw new StackOverflowException();

            traceback.Add(entry.Name);
            output.AddRange(ResolveLists(listFromName(entry.Name).Entries, listFromName, traceback));
        }

        return output;
    }

    public static bool IsRecursive(this Entry entry, Func<string, List> listFromName, List<string> traceback)
    {
        if (entry.Type != EntryType.List) return false;
        if (traceback.Contains(entry.Name)) return true;

        traceback.Add(entry.Name);
        var list = listFromName(entry.Name);

        return list.Entries.Any(e => e.IsRecursive(listFromName, traceback));
    }

    public static string SanitizeUrl(this string url)
    {
        url = url.Trim();
        var line = url;

        if (line.StartsWith("https://")) url = line[8..];
        if (line.StartsWith("http://")) url = line[7..];

        line = url;
        if (line.StartsWith("www.")) url = line[4..];

        return url;
    }

    public static string GetDaysString(this DayOfWeek[] days) 
    {
        string daysString;

        if (days.SequenceEqual(Enum.GetValues<DayOfWeek>())) 
            daysString = "all";
        else if (days.SequenceEqual([DayOfWeek.Saturday, DayOfWeek.Sunday]))
            daysString = "weekends";
        else if (days.SequenceEqual([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]))
            daysString = "weekdays";

        else daysString = string.Join("", days.Select(e => e switch
        {
            DayOfWeek.Monday => "M",
            DayOfWeek.Tuesday => "T",
            DayOfWeek.Wednesday => "W",
            DayOfWeek.Thursday => "H",
            DayOfWeek.Friday => "F",
            DayOfWeek.Saturday => "S",
            DayOfWeek.Sunday => "U",
            _ => throw new NotImplementedException(),
        }));

        return daysString;
    }

}
