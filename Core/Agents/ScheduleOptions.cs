namespace Core;

[Serializable]
public class ScheduleOptions : AgentOptions 
{
    public TimeSpan WarningTime { get; set; }
}