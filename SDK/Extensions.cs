using System.Diagnostics.CodeAnalysis;

namespace SDK;

public static class Extensions
{

    public static List<Entry> ResolveLists(this List<Entry> entries, Func<string, List> listFromName, List<string>? traceback = null, string? name = null)
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

            List<string> newTraceback = name == null ? traceback : [.. traceback, name];
            output.AddRange(ResolveLists(listFromName(entry.Name).Entries, listFromName, newTraceback, entry.Name));
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

    public static string ToEntryString(this Entry entry)
    {
        string prefix = entry.Type switch
        {
            EntryType.Website => "",
            EntryType.App => "+",
            EntryType.List => "@",
            _ => throw new NotImplementedException(),
        };

        return prefix + entry.Name;
    }

    public static bool ToEntry(this string line, [NotNullWhen(true)] out Entry? entry, out string? error, List<List> lists, string? list = null)
    {
        entry = null;
        error = null;

        if (line.IsWhiteSpace() || line.StartsWith("#")) return false;
        var lineWithoutPrefix = line[1 ..].Trim();

        if (line.StartsWith("+")) 
        {
            entry = new(EntryType.App, lineWithoutPrefix);
            return true;
        }

        if (line.StartsWith("@"))
        {
            if (!lists.Select(e => e.Name).Contains(lineWithoutPrefix))
            {
                error = $"Missing list entry was removed: {lineWithoutPrefix}";
                return false;
            }

            if (lineWithoutPrefix == list)
            {
                error = $"Redundant list entry was removed: {lineWithoutPrefix}";
                return false;
            }

            entry = new Entry(EntryType.List, lineWithoutPrefix);

            if (list != null && entry.IsRecursive(name => lists.Single(e => e.Name == name), [list]))
            {
                error = $"Recursive list entry was removed: {lineWithoutPrefix}";
                return false;
            }

            return true;
        }
            
        entry = new(EntryType.Website, line.SanitizeUrl());
        return true;
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
            daysString = "everyday";
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
