public static class DayOfWeekExtensions 
{
    public static string GetDaysString(this DayOfWeek[] days) 
    {
        string daysString;

        if (days.SequenceEqual(Enum.GetValues<DayOfWeek>())) 
            daysString = "all";
        else if (days.SequenceEqual([DayOfWeek.Saturday, DayOfWeek.Sunday]))
            daysString = "weekends";
        else if (days.SequenceEqual([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]))
            daysString = "weekdays";

        else daysString = string.Join("", days.Select(e => e switch
        {
            DayOfWeek.Monday => "M",
            DayOfWeek.Tuesday => "T",
            DayOfWeek.Wednesday => "W",
            DayOfWeek.Thursday => "H",
            DayOfWeek.Friday => "F",
            DayOfWeek.Saturday => "S",
            DayOfWeek.Sunday => "U",
            _ => throw new NotImplementedException(),
        }));

        return daysString;
    }
}