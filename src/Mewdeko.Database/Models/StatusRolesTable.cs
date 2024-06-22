namespace Mewdeko.Database.Models;

public class StatusRolesTable : DbEntity
{
    public ulong GuildId { get; set; }
    public string Status { get; set; }
    public string ToAdd { get; set; }
    public string ToRemove { get; set; }
    public string StatusEmbed { get; set; } = null;
    public bool ReaddRemoved { get; set; } = false;
    public bool RemoveAdded { get; set; } = true;
    public ulong StatusChannelId { get; set; }
}