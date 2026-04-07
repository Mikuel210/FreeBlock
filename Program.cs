using FreeBlock;

// freeblock schedule add [list] [start] [end] [days]
// freeblock schedule remove [list] -> schedule prompt
// freeblock schedule edit [list]

#region Command System

CommandSystem.Register(new Command(
    ["help"],
    [],
    ShowHelp
));

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

CommandSystem.Register(new Command(
    [],
    [],
    ShowUsage
));

CommandSystem.Handle(args);

#endregion

#region Commands

void ShowUsage()
{
    Console.WriteLine("""
                      Usage: freeblock [command]
                      See: freeblock help
                      """);
}

void ShowHelp()
{
    Console.WriteLine("""
                      Usage: freeblock [command]
                      
                      Available commands:
                      freeblock help                     Show the help dialog
                      freeblock status                   Show the current status of block lists
                      freeblock list add [name]          Create a new block list
                      freeblock list remove [name]       Delete a block list
                      freeblock block [list]             Enable a block list
                      freeblock unblock [list]           Disable a block list
                      freeblock lock [list] [minutes]    Block and prevent disabling a list for the provided amount of minutes
                      """);
}

void ShowStatus()
{
    if (Config.BlockLists.Count == 0)
        Console.WriteLine("No lists found");
    
    foreach (var list in Config.BlockLists)
        Console.WriteLine((list.Enabled ? "🟢" : "🔴") + $" {list.Name} " + (list.Locked ? $"(🔒 {list.UnlockTime})" : ""));
}

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
    
    Console.WriteLine();
    
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
    
    Prompt:
    Console.Write($"This will block {list.Value!.Name} for {minutes.Value} minutes. Okay to continue? (Y/n): ");
    var input = Console.ReadLine()!.Trim().ToLowerInvariant();

    if (input is "" or "y")
    {
        list.Value!.Enabled = true;
        list.Value!.UnlockTime = unlockTime;
        Blocker.UpdateBlock();
        Config.Save();
    
        Console.WriteLine();
        Console.WriteLine($"Locked list for {minutes.Value} minutes: {list.Value!.Name}");   
    } else if (input != "n") goto Prompt;
}

#endregion