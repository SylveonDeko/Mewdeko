namespace Mewdeko.Database.Models;

public class AutoPublish : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong BlacklistedUsers { get; set; }
}