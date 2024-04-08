namespace Mewdeko.Modules.Xp.Common;

/// <summary>
/// Represents the full set of XP-related statistics for a user within a specific guild.
/// </summary>
public class FullUserStats
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullUserStats"/> class.
    /// </summary>
    /// <param name="usr">The user these statistics belong to.</param>
    /// <param name="fullGuildStats">The complete set of guild-specific XP statistics for the user.</param>
    /// <param name="guild">The level statistics in the context of the guild.</param>
    /// <param name="guildRanking">The user's rank within the guild based on XP.</param>
    public FullUserStats(DiscordUser usr, UserXpStats fullGuildStats, LevelStats guild, int guildRanking)
    {
        User = usr;
        Guild = guild;
        GuildRanking = guildRanking;
        FullGuildStats = fullGuildStats;
    }

    /// <summary>
    /// Gets the user.
    /// </summary>
    public DiscordUser User { get; }

    /// <summary>
    /// Gets the full guild statistics for the user.
    /// </summary>
    public UserXpStats FullGuildStats { get; }

    /// <summary>
    /// Gets the level statistics in the guild context.
    /// </summary>
    public LevelStats Guild { get; }

    /// <summary>
    /// Gets the user's guild ranking based on XP.
    /// </summary>
    public int GuildRanking { get; }
}