#nullable enable

namespace Mewdeko.Database.Models;

public class CommandStats : DbEntity
{
    public string NameOrId { get; set; }
    public string Module { get; set; } = null;
    public long IsSlash { get; set; } = 0;
    public long Trigger { get; set; } = 0;
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong UserId { get; set; }
}