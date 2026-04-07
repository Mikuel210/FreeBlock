using FreeBlock;

#region Command System

CommandSystem.Register(new Command(
    ["status"],
    [],
    ShowStatus
));

CommandSystem.Register(new Command(
    ["list", "add"], 
    [new AddListArgument("name")],
    AddList
));

CommandSystem.Register(new Command(
    ["list", "remove"],
    [new ListArgument("name")],
    RemoveList
));

CommandSystem.Register(new Command(
    ["block"],
    [new ListArgument("list")],
    Block
));

CommandSystem.Register(new Command(
    ["unblock"],
    [new ListArgument("list")],
    Unblock
));

CommandSystem.Register(new Command(
    ["lock"],
    [new ListArgument("list"), new IntArgument("minutes")],
    Lock
));

CommandSystem.Handle(args);

#endregion

#region Commands

void AddList(AddListArgument argument)
{
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
    var list = new BlockList
    {
        Name = argument.Value!,
        UrlList = urlList
    };
        
    Config.BlockLists.Add(list);
    Blocker.UpdateBlock();
    Config.Save();
    
    Console.WriteLine("List created successfully");
}

void RemoveList(ListArgument list)
{
    if (list.Value!.Locked)
    {
        Console.WriteLine($"List {list.Value!.Name} is locked until {list.Value!.UnlockTime}");
        return;
    }
    
    list.Value!.Enabled = false;
    Config.BlockLists.Remove(list.Value);
    Blocker.UpdateBlock();
    Config.Save();
    
    Console.WriteLine($"Removed list: {list.Value.Name}");
}

void ShowStatus()
{
    if (Config.BlockLists.Count == 0)
        Console.WriteLine("No lists found");
    
    foreach (var list in Config.BlockLists)
        Console.WriteLine((list.Enabled ? "🟢" : "🔴") + $" {list.Name} " + (list.Locked ? $"(🔒 {list.UnlockTime})" : ""));
}

void Block(ListArgument list)
{
    list.Value!.Enabled = true;
    Blocker.UpdateBlock();
    Config.Save();
    
    Console.WriteLine($"Blocked list: {list.Value!.Name}");
}

void Unblock(ListArgument list)
{
    if (list.Value!.Locked)
    {
        Console.WriteLine($"List {list.Value!.Name} is locked until {list.Value!.UnlockTime}");
        return;
    }
    
    list.Value.Enabled = false;
    Blocker.UpdateBlock();
    Config.Save();
    
    Console.WriteLine($"Unblocked list: {list.Value!.Name}");
}

void Lock(ListArgument list, IntArgument minutes)
{
    var unlockTime = DateTime.Now.AddMinutes(minutes.Value);

    if (list.Value!.UnlockTime != null && list.Value!.UnlockTime > unlockTime)
    {
        Console.WriteLine($"List is already locked until {list.Value!.UnlockTime}: {list.Value!.Name}");
        return;
    }
    
    list.Value!.Enabled = true;
    list.Value!.UnlockTime = unlockTime;
    Blocker.UpdateBlock();
    Config.Save();
    
    Console.WriteLine($"Locked list for {minutes.Value} minutes: {list.Value!.Name}");
}

#endregion