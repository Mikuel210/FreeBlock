using Core;
using Lock = Core.Lock;

namespace Daemon;

public static class State
{

    public static List<Entry> Block { get; set; } = [];
    public static List<Lock> Locks { get; set; } = [];
    public static List<List> Lists { get; set; } = [];
    public static List<Schedule> Schedules { get; set; } = [];

    // SignalR invokes land on thread-pool threads. Without serialization, two concurrent mutations
    // of the same list could interleave and corrupt in-memory state, then persist the corruption (P4).
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private static JsonFile? _file;
    private static bool _initialized;

    /// <summary>Loads state from <paramref name="file"/>. The daemon loads the installed state file on
    /// first use; tests call this to point State at their own file instead.</summary>
    public static void Initialize(JsonFile file)
    {
        _file = file;
        Block = _file?.GetList<Entry>(nameof(StateSnapshot.Block)) ?? [];
        Locks = _file?.GetList<Lock>(nameof(StateSnapshot.Locks)) ?? [];
        Lists = _file?.GetList<List>(nameof(StateSnapshot.Lists)) ?? [];
        Schedules = _file?.GetList<Schedule>(nameof(StateSnapshot.Schedules)) ?? [];
        _initialized = true;
    }

    /// <summary>Loads state from the installed state file.</summary>
    public static void Initialize()
    {
        _file = new JsonFile(Platform.StateFile, new StateSnapshot(Block, Locks, Lists, Schedules));
        Block = _file?.GetList<Entry>(nameof(StateSnapshot.Block)) ?? [];
        Locks = _file?.GetList<Lock>(nameof(StateSnapshot.Locks)) ?? [];
        Lists = _file?.GetList<List>(nameof(StateSnapshot.Lists)) ?? [];
        Schedules = _file?.GetList<Schedule>(nameof(StateSnapshot.Schedules)) ?? [];
        _initialized = true;
    }

    /// <summary>Loads from the installed state file the first time state is read.</summary>
    private static void EnsureInitialized()
    {
        if (_initialized) return;
        Initialize();
    }

    /// <summary>Runs <paramref name="action"/> holding the state lock. Hub invocations should go
    /// through this for anything that reads-then-writes state.</summary>
    public static async Task MutateAsync(Func<Task> action)
    {
        EnsureInitialized();
        await _lock.WaitAsync();

        try
        {
            await action();
            Save();
        }
        finally
        {
            _lock.Release();
        }
    }

    public static StateSnapshot GetSnapshot()
    {
        EnsureInitialized();
        return new StateSnapshot(Block, Locks, Lists, Schedules);
    }

    public static void Update()
    {
        EnsureInitialized();
        Locks = [.. Locks.Where(e => DateTime.Now < e.UnlockTime)];
    }

    public static void Save()
    {
        if (_file == null) return;

        _file.Set(nameof(StateSnapshot.Block), Block);
        _file.Set(nameof(StateSnapshot.Locks), Locks);
        _file.Set(nameof(StateSnapshot.Lists), Lists);
        _file.Set(nameof(StateSnapshot.Schedules), Schedules);

        _file.Save();
    }

}
