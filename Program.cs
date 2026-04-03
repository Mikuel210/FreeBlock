using FreeBlock;

if (args.Length < 1) return;

switch (args[0])
{
    case "list":
        if (args.Length < 2) return;
        if (args[1] == "add") AddList();
        
        if (args.Length < 3) return;
        if (args[1] == "remove") RemoveList(args[2]);
        
        break;
    
    case "status":
        ShowStatus();
        break;
        
    case "block":
        if (args.Length < 2) return;
        Block(args[1]);

        break;
    
    case "unblock":
        if (args.Length < 2) return;
        Unblock(args[1]);

        break;
}

void AddList()
{
    // List name
    Start:
    Console.Write("List name: ");
    string name = Console.ReadLine()!.Trim();
    
    if (ConfigProvider.BlockLists.Select(e => e.Name.ToLower()).Contains(name.ToLower())) {
        Console.WriteLine("Name already in use");
        Console.WriteLine();

        goto Start;
    }
    
    // Websites
    List<string> urlList = [];

    while (true) {
        Console.Write("Add website (empty to end): ");
        string input = Console.ReadLine()!;
        
        if (input.Trim() == string.Empty) break;
        urlList.Add(input);   
    }
    
    // Add list
    BlockList list = new() {
        Name = name,
        UrlList = urlList
    };
    
    ConfigProvider.BlockLists.Add(list);
    ConfigProvider.Save();
    Console.WriteLine("List created successfully");
}

void RemoveList(string name)
{
    var list = ConfigProvider.BlockLists.FirstOrDefault(e => e.Name.ToLower() == name.Trim().ToLower());
    
    if (list == null) {
        Console.WriteLine($"List {name.Trim()} not found");
        return;
    }

    list.Enabled = false;
    ConfigProvider.BlockLists.Remove(list);
    ConfigProvider.Save();
    
    Console.WriteLine($"Removed list: {list.Name}");
}

void ShowStatus()
{
    if (ConfigProvider.BlockLists.Count == 0)
        Console.WriteLine("No lists found");
    
    foreach (var list in ConfigProvider.BlockLists)
        Console.WriteLine((list.Enabled ? "🟢" : "🔴") + $" {list.Name}");
}

void Block(string name)
{
    var list = ConfigProvider.BlockLists.FirstOrDefault(e => e.Name.ToLower() == name.Trim().ToLower());
    
    if (list == null) {
        Console.WriteLine($"List {name.Trim()} not found");
        return;
    }
    
    list.Enabled = true;
    ConfigProvider.Save();
    
    Console.WriteLine($"Blocked list: {list.Name}");
}

void Unblock(string name)
{
    var list = ConfigProvider.BlockLists.FirstOrDefault(e => e.Name.ToLower() == name.Trim().ToLower());
    
    if (list == null) {
        Console.WriteLine($"List {name.Trim()} not found");
        return;
    }
    
    list.Enabled = false;
    ConfigProvider.Save();
    
    Console.WriteLine($"Unblocked list: {list.Name}");
}