namespace Mewdeko.Database.Models;

public class Currency : DbEntity
{
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public ulong Amount { get; set; }
}