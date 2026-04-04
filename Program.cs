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
    
    case "lock":
        if (args.Length < 3) return;
        Lock(args[1], args[2]);

        break;
}

void AddList()
{
    // List name
    Start:
    Console.Write("List name: ");
    string name = Console.ReadLine()!.Trim();
    
    if (Config.BlockLists.Select(e => e.Name.ToLower()).Contains(name.ToLower())) {
        Console.WriteLine("Name already in use");
        Console.WriteLine();

        goto Start;
    }
    
    // Websites
    List<string> urlList = [];

    while (true) {
        Console.Write("Add website (empty to end): ");
        string input = Console.ReadLine()!.Trim();
        if (input == string.Empty) break;
        
        if (input.StartsWith("https://")) input = input.Remove(0, 8);
        if (input.StartsWith("http://")) input = input.Remove(0, 7);
        urlList.Add(input);
    }
    
    // Add list
    BlockList list = new() {
        Name = name,
        UrlList = urlList
    };
    
    Config.BlockLists.Add(list);
    Blocker.UpdateBlock();
    Config.Save();
    
    Console.WriteLine("List created successfully");
}

void RemoveList(string name)
{
    var list = Config.BlockLists.FirstOrDefault(e => e.Name.ToLower() == name.Trim().ToLower());
    
    if (list == null) {
        Console.WriteLine($"List {name.Trim()} not found");
        return;
    }

    list.Enabled = false;
    Config.BlockLists.Remove(list);
    Blocker.UpdateBlock();
    Config.Save();
    
    Console.WriteLine($"Removed list: {list.Name}");
}

void ShowStatus()
{
    if (Config.BlockLists.Count == 0)
        Console.WriteLine("No lists found");
    
    foreach (var list in Config.BlockLists)
        Console.WriteLine((list.Enabled ? "🟢" : "🔴") + $" {list.Name} " + (list.Locked ? $"(🔒 {list.UnlockTime})" : ""));
}

void Block(string name)
{
    var list = Config.BlockLists.FirstOrDefault(e => e.Name.ToLower() == name.Trim().ToLower());
    
    if (list == null) {
        Console.WriteLine($"List {name.Trim()} not found");
        return;
    }
    
    list.Enabled = true;
    Blocker.UpdateBlock();
    Config.Save();
    
    Console.WriteLine($"Blocked list: {list.Name}");
}

void Unblock(string name)
{
    var list = Config.BlockLists.FirstOrDefault(e => e.Name.ToLower() == name.Trim().ToLower());
    
    if (list == null) {
        Console.WriteLine($"List {name.Trim()} not found");
        return;
    }

    if (list.Locked)
    {
        Console.WriteLine($"List {list.Name} is locked until {list.UnlockTime}");
        return;
    }
    
    list.Enabled = false;
    Blocker.UpdateBlock();
    Config.Save();
    
    Console.WriteLine($"Unblocked list: {list.Name}");
}

void Lock(string name, string minutesString)
{
    var list = Config.BlockLists.FirstOrDefault(e => e.Name.ToLower() == name.Trim().ToLower());
    
    if (list == null) {
        Console.WriteLine($"List {name.Trim()} not found");
        return;
    }

    if (!int.TryParse(minutesString.Trim(), out int minutes))
    {
        Console.WriteLine($"Minutes must be an integer");
        return;   
    }
    
    list.Enabled = true;
    list.UnlockTime = DateTime.Now.AddMinutes(minutes);
    Blocker.UpdateBlock();
    Config.Save();
    
    Console.WriteLine($"List locked for {minutes} minutes: {list.Name}");
}