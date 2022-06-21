namespace Mewdeko.Database.Models;

public class Highlights : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public string Word { get; set; }
}