namespace Core;

public class RecursiveListException(string name) : Exception
{
    public string ListName { get; } = name;
    public override string Message => $"List references itself recursively: {ListName}";
}
