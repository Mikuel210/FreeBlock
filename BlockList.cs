using Newtonsoft.Json;

namespace FreeBlock;

public class BlockList
{

    public string Name { get; init; } = string.Empty;
    public List<string> UrlList { get; init; } = [];
    public bool Enabled { get; set; }
    public DateTime? UnlockTime { get; set; }
    [JsonIgnore] public bool Locked => UnlockTime != null && UnlockTime > DateTime.Now;

    public static BlockList? FromName(string name) => 
        Config.BlockLists.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.InvariantCultureIgnoreCase));

}
