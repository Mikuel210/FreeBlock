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
    ["list", "edit"],
    [new ListArgument("name")],
    EditList
));

CommandSystem.Register(new Command(
    ["list", "rename"],
    [new ListArgument("old"), new AddListArgument("new")],
    RenameList
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
                      freeblock list edit [name]         Edit a block list
                      freeblock list rename [old] [new]  Rename a block list
                      freeblock list remove [name]       Delete a block list
                      freeblock block [list]             Enable a block list
                      freeblock unblock [list]           Disable a block list
                      freeblock lock [list] [minutes]    Block a list for the provided amount of minutes
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
    var list = new BlockList
    {
        Name = argument.Value!
    };

    Console.WriteLine("Waiting for your editor to close the file...");
    await ConsoleUtils.EditList(list);

    await ConnectionManager.Connection!.InvokeAsync("AddListAsync", list);
    Console.WriteLine("List created successfully");
}

async Task EditList(ListArgument argument)
{
    var list = argument.Value!;
    var previousUrls = list.UrlList.ToList();

    Console.WriteLine("Waiting for your editor to close the file...");
    await ConsoleUtils.EditList(list);

    bool closeBrowsers = false;

    if (list.Enabled)
    {
        // Close browsers
        foreach (string url in list.UrlList)
        {
            if (previousUrls.Contains(url)) continue;

            Console.WriteLine();
            if (!ConsoleUtils.PromptYesNo("This will close all browser windows to refresh blocking. Okay to continue?", true, true)) return;

            closeBrowsers = true;
            break;
        }

        // Revert removed websites
        bool showWarning = false;

        foreach (string url in previousUrls)
        {
            if (list.UrlList.Contains(url)) continue;

            list.UrlList.Add(url);
            showWarning = true;
        }

        Console.WriteLine();
        if (showWarning) ConsoleUtils.Warning("Removing websites is not allowed while the list is enabled");
    }

    await ConnectionManager.Connection!.InvokeAsync("EditListAsync", list, closeBrowsers);
    Console.WriteLine($"Updated list: {list.Name}");
}

async Task RenameList(ListArgument listArgument, AddListArgument nameArgument)
{
    var list = listArgument.Value!;
    string oldName = list.Name;
    string newName = nameArgument.Value!;

    await ConnectionManager.Connection!.InvokeAsync("RenameListAsync", list, newName);
    Console.WriteLine($"Renamed list: {oldName} -> {newName}");
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
