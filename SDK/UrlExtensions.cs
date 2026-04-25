namespace SDK;

public static class UrlExtensions
{
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
}
