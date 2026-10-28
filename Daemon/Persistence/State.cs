using Core;
using Lock = Core.Lock;

namespace Daemon;

public static class State
{

    public static List<Entry> Block { get; set; } = [];
    public static List<Lock> Locks { get; set; } = [];
    public static List<List> Lists { get; set; } = [];
    public static List<Schedule> Schedules { get; set; } = [];
    private static JsonFile? _file;
    private static bool _initialized;

    public static void Initialize()
    {
        _file = new JsonFile(Platform.StateFile, new StateSnapshot(Block, Locks, Lists, Schedules));
        Block = _file?.GetList<Entry>(nameof(StateSnapshot.Block)) ?? [];
        Locks = _file?.GetList<Lock>(nameof(StateSnapshot.Locks)) ?? [];
        Lists = _file?.GetList<List>(nameof(StateSnapshot.Lists)) ?? [];
        Schedules = _file?.GetList<Schedule>(nameof(StateSnapshot.Schedules)) ?? [];
        _initialized = true;
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        Initialize();
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
