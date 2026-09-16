namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     How a member arrived in the guild, as far as invite tracking could tell.
/// </summary>
public enum InviteJoinType
{
    /// <summary>
    ///     The join could not be attributed: missing permissions, a burst of simultaneous joins, or a code the bot had
    ///     not seen yet.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     A normal invite link created by a member.
    /// </summary>
    Invite = 1,

    /// <summary>
    ///     The guild's vanity URL.
    /// </summary>
    Vanity = 2,

    /// <summary>
    ///     A bot added through OAuth rather than an invite.
    /// </summary>
    Bot = 3
}

/// <summary>
///     Why a join was flagged as fake.
/// </summary>
public enum InviteFakeReason
{
    /// <summary>
    ///     The join was not flagged.
    /// </summary>
    None = 0,

    /// <summary>
    ///     The account was created more recently than the guild's minimum account age.
    /// </summary>
    NewAccount = 1,

    /// <summary>
    ///     The member had been in the guild before and rejoins are configured not to count.
    /// </summary>
    Rejoin = 2,

    /// <summary>
    ///     The member used their own invite.
    /// </summary>
    Self = 3,

    /// <summary>
    ///     The member has no avatar and the guild flags those.
    /// </summary>
    NoAvatar = 4
}

/// <summary>
///     The kinds of invite tracking exclusion.
/// </summary>
public enum InviteExclusionKind
{
    /// <summary>
    ///     A user who never earns invite credit.
    /// </summary>
    BlacklistedUser = 0,

    /// <summary>
    ///     A role whose holders never earn invite credit, judged at join time.
    /// </summary>
    BlacklistedRole = 1,

    /// <summary>
    ///     A user whose invites are tracked but hidden from leaderboards.
    /// </summary>
    HiddenUser = 2
}

/// <summary>
///     Time window for leaderboards and stats.
/// </summary>
public enum StatsRange
{
    /// <summary>
    ///     Everything ever recorded.
    /// </summary>
    AllTime = 0,

    /// <summary>
    ///     The last 24 hours.
    /// </summary>
    Daily = 1,

    /// <summary>
    ///     The last 7 days.
    /// </summary>
    Weekly = 2,

    /// <summary>
    ///     The last 30 days.
    /// </summary>
    Monthly = 3
}

/// <summary>
///     The scope of an invite reset.
/// </summary>
public enum InviteResetScope
{
    /// <summary>
    ///     Every inviter in the guild.
    /// </summary>
    Server = 0,

    /// <summary>
    ///     Only inviters who are no longer in the guild.
    /// </summary>
    LeftMembers = 1
}

/// <summary>
///     Helpers for <see cref="StatsRange" />.
/// </summary>
public static class StatsRangeExtensions
{
    /// <summary>
    ///     Gets the start of the window, or null for all time.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <returns>The UTC cutoff, or null.</returns>
    public static DateTime? Since(this StatsRange range)
    {
        return range switch
        {
            StatsRange.Daily => DateTime.UtcNow.AddDays(-1),
            StatsRange.Weekly => DateTime.UtcNow.AddDays(-7),
            StatsRange.Monthly => DateTime.UtcNow.AddDays(-30),
            _ => null
        };
    }

    /// <summary>
    ///     Gets a short human readable name for the range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <returns>The display name.</returns>
    public static string DisplayName(this StatsRange range)
    {
        return range switch
        {
            StatsRange.Daily => "Last 24 Hours",
            StatsRange.Weekly => "Last 7 Days",
            StatsRange.Monthly => "Last 30 Days",
            _ => "All Time"
        };
    }
}