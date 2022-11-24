namespace Mewdeko.Database.Models;

public class StatusRoles : DbEntity
{
    public ulong GuildId { get; set; }
    public string Status { get; set; }
    public string ToAdd { get; set; }
    public string ToRemove { get; set; }
    public string StatusEmbed { get; set; }
    public bool ReaddRemoved { get; set; } = false;
}