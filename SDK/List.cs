using Newtonsoft.Json;

namespace SDK;

public class List : IName
{

    public string Name { get; set; } = string.Empty;
    public List<Entry> Entries { get; set; } = [];

}
