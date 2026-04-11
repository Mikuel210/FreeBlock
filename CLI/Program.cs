using Microsoft.AspNetCore.SignalR.Client;
using SDK;
using CLI;

#region Command System

CommandSystem.Register(new Command(
    [],
    [],
    ShowUsage
));

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

await CommandSystem.Handle(args);

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

async Task ShowStatus()
{
    var lists = await ConnectionManager.Connection!.InvokeAsync<BlockList[]>("GetBlockListsAsync");

    if (lists.Length == 0)
        Console.WriteLine("No lists found");

    foreach (var list in lists)
        Console.WriteLine((list.Enabled ? "🟢" : "🔴") + $" {list.Name} " + (list.Locked ? $"(🔒 {list.UnlockTime})" : ""));
}

async Task AddList(AddListArgument argument)
{
    // Prompt websites
    List<string> urlList = [];

    while (true)
    {
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

    await ConnectionManager.Connection!.InvokeAsync("AddListAsync", list);
    Console.WriteLine("List created successfully");
}

async Task RemoveList(ListArgument list)
{
    if (list.Value!.Locked)
    {
        Console.WriteLine($"List {list.Value!.Name} is locked until {list.Value!.UnlockTime}");
        return;
    }

    await ConnectionManager.Connection!.InvokeAsync("RemoveListAsync", list.Value!);
    Console.WriteLine($"Removed list: {list.Value.Name}");
}

async Task Block(ListArgument list)
{
    if (list.Value!.Enabled == true)
    {
        Console.WriteLine($"List is already blocked: {list.Value!.Name}");
        return;
    }

    if (!ConsoleUtils.PromptYesNo("This will close all browser windows to refresh blocking. Okay to continue?", true)) return;

    await ConnectionManager.Connection!.InvokeAsync("BlockAsync", list.Value!);
    Console.WriteLine($"Blocked list: {list.Value!.Name}");
}

async Task Unblock(ListArgument list)
{
    if (list.Value!.Locked)
    {
        Console.WriteLine($"List {list.Value!.Name} is locked until {list.Value!.UnlockTime}");
        return;
    }

    await ConnectionManager.Connection!.InvokeAsync("UnblockAsync", list.Value!);
    Console.WriteLine($"Unblocked list: {list.Value!.Name}");
}

async Task Lock(ListArgument list, IntArgument minutes)
{
    var unlockTime = DateTime.Now.AddMinutes(minutes.Value);

    if (list.Value!.UnlockTime != null && list.Value!.UnlockTime > unlockTime)
    {
        Console.WriteLine($"List is already locked until {list.Value!.UnlockTime}: {list.Value!.Name}");
        return;
    }

    var prompt = $"This will block {list.Value!.Name} for {minutes.Value} minute{(minutes.Value == 1 ? "" : "s")} ";
    prompt += "and close all browser windows to refresh blocking. Okay to continue?";

    if (!ConsoleUtils.PromptYesNo(prompt, true)) return;
    await ConnectionManager.Connection!.InvokeAsync("LockAsync", list.Value!, unlockTime);

    Console.WriteLine($"Locked list for {minutes.Value} minutes: {list.Value!.Name}");
}

#endregion
