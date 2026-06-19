using SDK;
using Lock = SDK.Lock;

namespace Daemon;

public static class State
{

    public static List<Entry> Block { get; set; } = [];
    public static List<Lock> Locks { get; set; } = [];
    public static List<List> Lists { get; set; } = [];
    public static List<Schedule> Schedules { get; set; } = [];

    private static readonly JsonFile _file = new(Platform.StateFile, GetSnapshot());

    static State()
    {
        Block = _file.GetList<Entry>(nameof(StateSnapshot.Block));
        Locks = _file.GetList<Lock>(nameof(StateSnapshot.Locks));
        Lists = _file.GetList<List>(nameof(StateSnapshot.Lists));
        Schedules = _file.GetList<Schedule>(nameof(StateSnapshot.Schedules));
    }

    public static StateSnapshot GetSnapshot()
        => new(Block, Locks, Lists, Schedules);

    public static void Update() 
        => Locks = [.. Locks.Where(e => DateTime.Now < e.UnlockTime)];

    public static void Save()
    {
        _file.Set(nameof(StateSnapshot.Block), Block);
        _file.Set(nameof(StateSnapshot.Locks), Locks);
        _file.Set(nameof(StateSnapshot.Lists), Lists);
        _file.Set(nameof(StateSnapshot.Schedules), Schedules);

        _file.Save();
    }

}
