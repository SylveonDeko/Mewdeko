namespace Mewdeko.Database.Models;

public class Votes : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public ulong BotId { get; set; }
}