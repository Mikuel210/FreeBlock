namespace SDK;

public record StateSnapshot(List<Entry> Block, List<Lock> Locks, List<List> Lists, List<Schedule> Schedules);