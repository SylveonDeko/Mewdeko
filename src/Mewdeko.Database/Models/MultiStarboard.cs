namespace Mewdeko.Database.Models;

public class MultiStarboard : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public string Emote { get; set; }
    public string BlacklistedChannels { get; set; } = "0";
    public string WhitelistChannels { get; set; } = "0";
    public string BlacklistedUsers { get; set; } = "0";
    public string WhitelistedUsers { get; set; } = "0";
    public string BlacklistedRoles { get; set; } = "0";
    public string WhitelistedRoles { get; set; } = "0";
}