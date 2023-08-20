namespace Mewdeko.Database.Models;

public class GlobalBanConfig : DbEntity
{
    public ulong GuildId { get; set; }
    public GbType BanTypes { get; set; }
    public ulong GlobalBanLogChannel { get; set; }
    public GBActionType Action { get; set; }
    public long UseRecommendedAction { get; set; }
    public int Duration { get; set; }
}

public enum GBActionType
{
    Kick,
    Ban,
    Timeout,
    None
}