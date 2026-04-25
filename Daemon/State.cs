using SDK;

namespace Daemon;

public static class State
{

    public static List<BlockList> BlockLists { get; }
    public static List<Schedule> Schedules { get; }

    public struct DefaultValue()
    {
        public List<BlockList> blockLists = [];
        public List<Schedule> schedules = [];
    }

    private static readonly JsonFile _file = new(Platform.StateFile, new DefaultValue());

    static State()
    {
        BlockLists = _file.GetList<BlockList>(nameof(DefaultValue.blockLists));
        Schedules = _file.GetList<Schedule>(nameof(DefaultValue.schedules));
    }

    public static void Update()
    {
        // Update Scheduled property
        foreach (var list in BlockLists)
        {
            list.Scheduled = Schedules
                .FirstOrDefault(e =>
                {
                    var containsList = e.BlockLists.Select(e => e.Name).Contains(list.Name);
                    return containsList && e.Active;
                }) != null;
        }
    }

    public static void Save()
    {
        _file.Set(nameof(DefaultValue.blockLists), BlockLists);
        _file.Set(nameof(DefaultValue.schedules), Schedules);
        _file.Save();
    }

}
