namespace Mewdeko.Database.Models;

public class GuildUserData : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public DateTime BirthDay { get; set; }
}