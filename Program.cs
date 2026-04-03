using FreeBlock;

var blockList = new BlockList
{
    urlList = ["youtube.com", "www.youtube.com"]
};

if (args.Length < 1) return;
if (args[0] == "block") Blocker.Block(blockList);
if (args[0] == "unblock") Blocker.Unblock(blockList);