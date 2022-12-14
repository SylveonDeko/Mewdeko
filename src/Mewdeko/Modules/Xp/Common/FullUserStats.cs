namespace Mewdeko.Modules.Xp.Common;

public class FullUserStats
{
    public FullUserStats(DiscordUser usr, UserXpStats fullGuildStats,
        LevelStats guild, int guildRanking)
    {
        User = usr;
        Guild = guild;
        GuildRanking = guildRanking;
        FullGuildStats = fullGuildStats;
    }

    public DiscordUser User { get; }
    public UserXpStats FullGuildStats { get; }
    public LevelStats Guild { get; }
    public int GuildRanking { get; }
}