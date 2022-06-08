namespace Mewdeko.Database.Models;

public class Nicknames : DbEntity
{
    public ulong GuildId { get; set; }
    public string Nickname { get; set; }
    public ulong UserId { get; set; }
}