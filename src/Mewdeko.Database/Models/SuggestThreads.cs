namespace Mewdeko.Database.Models;

public class SuggestThreads : DbEntity
{
    public ulong MessageId { get; set; }
    public ulong ThreadChannelId { get; set; }
}