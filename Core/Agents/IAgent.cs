namespace Core;

public interface IAgent<TOptions> : IName where TOptions : AgentOptions
{
    TOptions Options { get; set; }
}
