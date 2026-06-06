using SDK;
using Lock = SDK.Lock;

namespace Daemon;

public static class State
{

    public static List<Entry> Block { get; set; } = [];
    public static List<Lock> Locks { get; } = [];
    public static List<List> Lists { get; } = [];
    public static List<Schedule> Schedules { get; } = [];

    public struct DefaultValue()
    {
        public List<Entry> block = [];
        public List<Lock> locks = [];
        public List<List> lists = [];
        public List<Schedule> schedules = [];
    }

    private static readonly JsonFile _file = new(Platform.StateFile, new DefaultValue());

    static State()
    {
        Block = _file.GetList<Entry>(nameof(DefaultValue.block));
        Locks = _file.GetList<Lock>(nameof(DefaultValue.locks));
        Lists = _file.GetList<List>(nameof(DefaultValue.lists));
        Schedules = _file.GetList<Schedule>(nameof(DefaultValue.schedules));
    }

    public static void Update() 
    {
        foreach (var @lock in Locks) 
        {
            if (DateTime.Now >= @lock.UnlockTime)
                Locks.Remove(@lock);
        }
    }

    public static void Save()
    {
        _file.Set(nameof(DefaultValue.block), Block);
        _file.Set(nameof(DefaultValue.locks), Locks);
        _file.Set(nameof(DefaultValue.lists), Lists);
        _file.Set(nameof(DefaultValue.schedules), Schedules);
        _file.Save();
    }

}
