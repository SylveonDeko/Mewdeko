namespace Mewdeko.Modules.Xp.Common;

public class FullUserStats
{
    public FullUserStats(DiscordUser usr, UserXpStats fullGuildStats, LevelStats global,
        LevelStats guild, int globalRanking, int guildRanking)
    {
        User = usr;
        Global = global;
        Guild = guild;
        GlobalRanking = globalRanking;
        GuildRanking = guildRanking;
        FullGuildStats = fullGuildStats;
    }

    public DiscordUser User { get; }
    public UserXpStats FullGuildStats { get; }
    public LevelStats Global { get; }
    public LevelStats Guild { get; }
    public int GlobalRanking { get; }
    public int GuildRanking { get; }
}