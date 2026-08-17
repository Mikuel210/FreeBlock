using CLI;

// General

CommandSystem.Register(new Command(
    Route: [],
    Category: Category.Blocking,
    Description: "The FOSS website and app blocker",
    Arguments: [],
    Flags: [
        new VersionFlag(),
        new UninstallFlag()
    ],
    Run: GeneralCommands.HandleRoot,
    Executable: false,
    IsRoot: true
));

CommandSystem.Register(new Command(
    Route: ["status"],
    Category: Category.Blocking,
    Description: "Show the current status of blocking",
    Arguments: [],
    Flags: [],
    Run: GeneralCommands.ShowStatus
));

CommandSystem.Register(new Command(
    Route: ["block"],
    Category: Category.Blocking,
    Description: "Enable manual block for one or more entries",
    Arguments: [new EntriesArgument("entries", "The entries to block")],
    Flags: [],
    Run: GeneralCommands.Block
));

CommandSystem.Register(new Command(
    Route: ["unblock"],
    Category: Category.Blocking,
    Description: "Disable manual block for one or more entries",
    Arguments: [new EntriesArgument("entries", "The entries to unblock")],
    Flags: [],
    Run: GeneralCommands.Unblock
));


// Lists

CommandSystem.Register(new Command(
    Route: ["list"],
    Category: Category.Lists,
    Description: "Manage lists",
    Arguments: [],
    Flags: [],
    Run: () => {},
    Executable: false
));

CommandSystem.Register(new Command(
    Route: ["list", "add"],
    Category: Category.Lists,
    Description: "Create a new block list from a set of entries",
    Arguments: [new AddListArgument("name", "The name of the new list")],
    Flags: [],
    Run: ListCommands.AddList
));

CommandSystem.Register(new Command(
    Route: ["list", "edit"],
    Category: Category.Lists,
    Description: "Edit the entries of a block list",
    Arguments: [new ListArgument("name", "The name of the list to edit")],
    Flags: [],
    Run: ListCommands.EditList
));

CommandSystem.Register(new Command(
    Route: ["list", "rename"],
    Category: Category.Lists,
    Description: "Rename a block list",
    Arguments: [
        new ListArgument("old", "The old name of the list"),
        new AddListArgument("new", "The new name of the list")
    ],
    Flags:[],
    Run: ListCommands.RenameList
));

CommandSystem.Register(new Command(
    Route: ["list", "remove"],
    Category: Category.Lists,
    Description: "Remove a block list",
    Arguments: [new ListArgument("name", "The name of the list to remove")],
    Flags: [],
    Run: ListCommands.RemoveList
));


// Locks

CommandSystem.Register(new Command(
    Route: ["lock"],
    Category: Category.Locks,
    Description: "Manage locks",
    Arguments: [],
    Flags: [],
    Run: () => {},
    Executable: false
));

CommandSystem.Register(new Command(
    Route: ["lock", "add"],
    Category: Category.Locks,
    Description: "Block a set of entries until a timer runs out",
    Arguments: [
        new AddLockArgument("name", "The name of the new lock"),
        new TimeSpanArgument("time", "The duration of the lock (HH:MM[:SS])"),
        new EntriesArgument("entries", "The entries to lock")
    ],
    Flags: [],
    Run: LockCommands.AddLock
));

CommandSystem.Register(new Command(
    Route: ["lock", "edit"],
    Category: Category.Locks,
    Description: "Edit the entries of a lock",
    Arguments: [new LockArgument("name", "The name of the lock to edit")],
    Flags: [
        new LockAddEntriesFlag(), 
        new LockExtendFlag()
    ],
    Run: LockCommands.EditLock
));

CommandSystem.Register(new Command(
    Route: ["lock", "rename"],
    Category: Category.Locks,
    Description: "Rename a lock",
    Arguments: [
        new LockArgument("old", "The old name of the lock"),
        new AddLockArgument("new", "The new name of the lock")
    ],
    Flags: [],
    Run: LockCommands.RenameLock
));


// Schedules

CommandSystem.Register(new Command(
    Route: ["schedule"],
    Category: Category.Schedules,
    Description: "Manage schedules",
    Arguments: [],
    Flags: [],
    Run: () => {},
    Executable: false
));

CommandSystem.Register(new Command(
    Route: ["schedule", "add"],
    Category: Category.Schedules,
    Description: "Create a schedule to enable entries automatically",
    Arguments: [
        new AddScheduleArgument("name", "The name of the new schedule"),
        new TimeArgument("start", "The start time for the schedule (HH:MM[:SS])"),
        new TimeArgument("end", "The end time for the schedule (HH:MM[:SS])"),
        new DaysArgument("days", "The days of the week to apply the schedule (MTWHFSU)"),
        new EntriesArgument("entries", "The entries to be blocked by the schedule")
    ],
    Flags: [],
    Run: ScheduleCommands.AddSchedule
));

CommandSystem.Register(new Command(
    Route: ["schedule", "edit"],
    Category: Category.Schedules,
    Description: "Edit the properties of a schedule",
    Arguments: [new ScheduleArgument("name", "The name of the schedule to edit")],
    Flags: [
        new ScheduleStartTimeFlag(),
        new ScheduleEndTimeFlag(),
        new ScheduleDaysFlag(),
        new ScheduleEntriesFlag()
    ],
    Run: ScheduleCommands.EditSchedule
));

CommandSystem.Register(new Command(
    Route: ["schedule", "rename"],
    Category: Category.Schedules,
    Description: "Rename a schedule",
    Arguments: [
        new ScheduleArgument("old", "The old name of the schedule"),
        new AddScheduleArgument("new", "The new name of the schedule")
    ],
    Flags: [],
    Run: ScheduleCommands.RenameSchedule
));

CommandSystem.Register(new Command(
    Route: ["schedule", "remove"],
    Category: Category.Schedules,
    Description: "Remove a schedule",
    Arguments: [new ScheduleArgument("name", "The name of the schedule to remove")],
    Flags: [],
    Run: ScheduleCommands.RemoveSchedule
));

await CommandSystem.Handle(args);