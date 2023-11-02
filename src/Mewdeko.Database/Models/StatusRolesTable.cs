namespace Mewdeko.Database.Models;

public class StatusRolesTable : DbEntity
{
    public ulong GuildId { get; set; }
    public string Status { get; set; }
    public string ToAdd { get; set; }
    public string ToRemove { get; set; }
    public string StatusEmbed { get; set; } = null;
    public long ReaddRemoved { get; set; } = 0;
    public long RemoveAdded { get; set; } = 1;
    public ulong StatusChannelId { get; set; }
}