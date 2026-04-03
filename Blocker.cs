public static class Blocker
{

    public static void Block(BlockList blockList)
    {
        using StreamWriter file = File.AppendText("/etc/hosts");

        foreach (string url in blockList.urlList)
            file.WriteLine($"0.0.0.0 {url}");

        RefreshDns();
    }

    private static void RefreshDns() => System.Diagnostics.Process.Start("resolvectl", "flush-caches");

}
