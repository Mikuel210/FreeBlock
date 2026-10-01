namespace Core;

public static class Globals
{
    public const string VERSION = "v0.7.2";
    
    // D13: Pin the "all days" array so GetDaysString and tests use the same definition
    public static readonly DayOfWeek[] AllDays = [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, 
        DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday];
}
