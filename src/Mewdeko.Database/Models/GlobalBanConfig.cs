namespace Mewdeko.Database.Models;

public class GlobalBanConfig : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong GlobalBanLogChannel { get; set; }
    public GbActionType Action { get; set; }
    public long UseRecommendedAction { get; set; }
    public int Duration { get; set; }
}

public enum GbActionType
{
    Kick,
    Ban,
    Timeout,
    None
}