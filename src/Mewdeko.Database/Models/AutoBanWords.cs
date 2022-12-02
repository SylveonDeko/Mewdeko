namespace Mewdeko.Database.Models;

public class AutoBanEntry : DbEntity
{
    public string Word { get; set; }
    public ulong GuildId { get; set; }
}