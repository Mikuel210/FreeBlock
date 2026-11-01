using System.Diagnostics.CodeAnalysis;

namespace Core;

public static class Extensions
{
    const int MAX_DEPTH = 20;

    public static List<Entry> ResolveLists(this List<Entry> entries, Func<string, List?> listFromName,
        HashSet<string>? visited = null, HashSet<Entry>? resolved = null, int depth = 0)
    {
        visited ??= new(StringComparer.OrdinalIgnoreCase);
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

            if (visited.Contains(entry.Name, StringComparer.OrdinalIgnoreCase))
                throw new RecursiveListException(entry.Name);

            visited.Add(entry.Name);

            try
            {
                var list = listFromName(entry.Name);

                if (list != null)
                {
                    output.AddRange(ResolveLists(list.Entries, listFromName, visited, resolved, depth + 1));
                }
            }
            finally
            {
                visited.Remove(entry.Name);
            }
        }

        return output;
    }

    public static bool ToEntry(this string line, [NotNullWhen(true)] out Entry? entry, out string? error, List<List> lists, string? list = null)
    {
        entry = null;
        error = null;

        if (line.IsWhiteSpace() || line.StartsWith("#")) return false;
        var lineWithoutPrefix = line[1..].Trim();

        if (line.StartsWith("+"))
        {
            entry = new(EntryType.App, lineWithoutPrefix);
            return true;
        }

        if (line.StartsWith("@"))
        {
            var referencedList = lists.FirstOrDefault(e => e.Name.Equals(lineWithoutPrefix, StringComparison.OrdinalIgnoreCase));

            if (referencedList is null)
            {
                error = $"Missing list entry was removed: {lineWithoutPrefix}";
                return false;
            }

            if (string.Equals(lineWithoutPrefix, list, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Redundant list entry was removed: {lineWithoutPrefix}";
                return false;
            }

            entry = new Entry(EntryType.List, referencedList.Name);

            if (list != null)
            {
                if (entry.IsRecursive(name => lists.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)),
                    new(StringComparer.OrdinalIgnoreCase) { list }))
                {
                    error = $"Recursive list entry was removed: {lineWithoutPrefix}";
                    return false;
                }
            }

            return true;
        }

        try
        {
            entry = new(EntryType.Website, line.SanitizeUrl());
        }
        catch (ArgumentException)
        {
            error = $"Invalid website entry was removed: {line}";
            return false;
        }

        return true;
    }

    public static string SanitizeUrl(this string url)
    {
        url = url.Trim();

        // Parse the authority rather than emitting ports, credentials or fragments into hosts
        if (url.Length == 0 || url.Any(char.IsWhiteSpace) || url.Contains('\\'))
            throw new ArgumentException("Invalid website URL", nameof(url));

        var absolute = url.Contains("://", StringComparison.Ordinal) ? url : "https://" + url;

        if (!Uri.TryCreate(absolute, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            throw new ArgumentException("Invalid website URL", nameof(url));
        }

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) host = host[4..];

        // DNS labels only: reject ambiguous empty labels and hosts-file metacharacters
        if (host.Length == 0 || host.Length > 253 || host.Split('.').Any(label =>
            label.Length == 0 || label.Length > 63 || !char.IsAsciiLetterOrDigit(label[0])
            || !char.IsAsciiLetterOrDigit(label[^1])
            || label.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')))
        {
            throw new ArgumentException("Invalid website hostname", nameof(url));
        }

        return host;
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

        if (timeSpan.Days > 0)
            parts.Add($"{timeSpan.Days} {(timeSpan.Days == 1 ? "day" : "days")}");
        if (timeSpan.Hours > 0)
            parts.Add($"{timeSpan.Hours} {(timeSpan.Hours == 1 ? "hour" : "hours")}");
        if (timeSpan.Minutes > 0)
            parts.Add($"{timeSpan.Minutes} {(timeSpan.Minutes == 1 ? "minute" : "minutes")}");
        if (timeSpan.Seconds > 0)
            parts.Add($"{timeSpan.Seconds} {(timeSpan.Seconds == 1 ? "second" : "seconds")}");

        return string.Join(", ", parts);
    }

}
