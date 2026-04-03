namespace FreeBlock;

public class BlockList
{

    public string Name { get; init; } = string.Empty;
    public List<string> UrlList { get; init; } = [];
    public bool Enabled
    {
        get;

        set
        {
            if (value) Blocker.Block(this);
            else Blocker.Unblock(this);
            
            field = value;
        }
    }
    
}
