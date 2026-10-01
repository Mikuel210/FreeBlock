using System.Diagnostics.CodeAnalysis;

namespace Core;

public static class Extensions
{
    const int MAX_DEPTH = 20;

    /// <param name="visited">Lists on the current resolution path. A repeat visit means a cycle, which is
    /// a client error, not a reason to kill the process (C3).</param>
    /// <param name="resolved">Entries already produced. Two lists pointing at the same entry yield it once
    /// (B3).</param>
    public static List<Entry> ResolveLists(this List<Entry> entries, Func<string, List?> listFromName,
        HashSet<string>? visited = null, HashSet<Entry>? resolved = null, int depth = 0)
    {
        visited ??= [];
        resolved ??= [];

        if (depth > MAX_DEPTH)
            throw new RecursiveListException($"Max recursion depth reached: {MAX_DEPTH}");

        List<Entry> output = [];

        foreach (var entry in entries)
        {
            if (entry.Type != EntryType.List)
            {
                if (resolved.Add(entry)) output.Add(entry);
                continue;
            }

            if (!visited.Add(entry.Name))
                throw new RecursiveListException(entry.Name);

            var list = listFromName(entry.Name);

            if (list != null)
                output.AddRange(ResolveLists(list.Entries, listFromName, visited, resolved, depth + 1));

            visited.Remove(entry.Name);
        }

        return output;
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

            if (list != null)
            {
                // Only cycle-check entries reachable from the list being edited. The traceback starts at
                // this list so a reference back to it is the cycle (C3).
                if (entry.IsRecursive(name => lists.FirstOrDefault(e => e.Name == name), [list]))
                {
                    error = $"Recursive list entry was removed: {lineWithoutPrefix}";
                    return false;
                }

                // A reference to a list that does not exist cannot be resolved later: drop it now (B6).
                if (!lists.Any(e => e.Name == lineWithoutPrefix))
                {
                    error = $"Missing list entry was removed: {lineWithoutPrefix}";
                    return false;
                }
            }

            return true;
        }

        entry = new(EntryType.Website, line.SanitizeUrl());
        return true;
    }

    public static string SanitizeUrl(this string url)
    {
        url = url.Trim();

        // Schemes are case-insensitive (RFC 3986 §3.1) — HTTPS://example.com is the same site.
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) url = url[8..];
        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) url = url[7..];

        // Path and query are irrelevant to hosts-file blocking, but a trailing slash is common.
        var noPath = url.IndexOfAny(['/', '?']);
        if (noPath > 0) url = url[..noPath];

        if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) url = url[4..];

        return url;
    }

    public static string GetDaysString(this DayOfWeek[] days)
    {
        string daysString;

        if (days.SequenceEqual(Globals.AllDays))
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

    public static string ToNaturalLanguage(this TimeSpan timeSpan)
    {
        List<string> parts = [];

        if (timeSpan.Hours > 0)
            parts.Add($"{timeSpan.Hours} {(timeSpan.Hours == 1 ? "hour" : "hours")}");

        if (timeSpan.Minutes > 0)
            parts.Add($"{timeSpan.Minutes} {(timeSpan.Minutes == 1 ? "minute" : "minutes")}");

        if (timeSpan.Seconds > 0)
            parts.Add($"{timeSpan.Seconds} {(timeSpan.Seconds == 1 ? "second" : "seconds")}");

        return string.Join(", ", parts);
    }

}
