namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     One row of an invite leaderboard.
/// </summary>
/// <param name="UserId">The inviter.</param>
/// <param name="Username">The inviter's display name, or a placeholder when they are gone.</param>
/// <param name="Total">The net invite total for the window.</param>
/// <param name="Regular">Joins credited that were not flagged as fake.</param>
/// <param name="Left">Credited joins whose member left again.</param>
/// <param name="Fake">Joins flagged as fake.</param>
/// <param name="Bonus">Manually granted invites. Only populated for the all time window.</param>
/// <param name="Retention">The share of real invited members still present, from 0 to 1, or null with no data.</param>
/// <param name="LatestJoinAt">When the inviter's most recent credited member joined.</param>
public record InviteLeaderboardEntry(
    ulong UserId,
    string Username,
    int Total,
    int Regular,
    int Left,
    int Fake,
    int Bonus,
    double? Retention,
    DateTime? LatestJoinAt);

/// <summary>
///     One invite code with its label and use count, used by the analytics summary.
/// </summary>
/// <param name="Code">The invite code, or "vanity" for the vanity URL.</param>
/// <param name="Label">The label attached to the code, or null.</param>
/// <param name="Joins">Joins attributed to this code in the window.</param>
public record InviteCodeSummary(string Code, string? Label, int Joins);

/// <summary>
///     Growth analytics for a window, derived from witnessed joins.
/// </summary>
/// <param name="Range">The window the numbers cover.</param>
/// <param name="Joins">Joins witnessed in the window.</param>
/// <param name="Leaves">Members who left in the window.</param>
/// <param name="FakeJoins">Joins flagged as fake.</param>
/// <param name="Stayed">Members who joined in the window and are still present.</param>
/// <param name="ViaInvite">Joins through a member's invite link.</param>
/// <param name="ViaVanity">Joins through the vanity URL.</param>
/// <param name="ViaBot">Bots added through OAuth.</param>
/// <param name="Unknown">Joins that could not be attributed.</param>
/// <param name="TopCodes">The most used invite codes.</param>
/// <param name="TopInviters">The top inviters for the window.</param>
public record InviteAnalytics(
    StatsRange Range,
    int Joins,
    int Leaves,
    int FakeJoins,
    int Stayed,
    int ViaInvite,
    int ViaVanity,
    int ViaBot,
    int Unknown,
    IReadOnlyList<InviteCodeSummary> TopCodes,
    IReadOnlyList<InviteLeaderboardEntry> TopInviters)
{
    /// <summary>
    ///     Joins minus leaves.
    /// </summary>
    public int NetGrowth
    {
        get
        {
            return Joins - Leaves;
        }
    }

    /// <summary>
    ///     The share of members who joined in the window and are still here, or null when nobody joined.
    /// </summary>
    public double? Retention
    {
        get
        {
            return Joins == 0 ? null : (double)Stayed / Joins;
        }
    }
}

/// <summary>
///     The outcome of attributing a single join, handed to greet placeholders and the log channel.
/// </summary>
/// <param name="Inviter">The credited inviter, or null.</param>
/// <param name="Code">The invite code used, or null.</param>
/// <param name="Uses">How many times the code has been used, or 0.</param>
/// <param name="Label">The label on the code, or null.</param>
/// <param name="JoinType">How the member arrived.</param>
/// <param name="FakeReason">Why the join was flagged, or <see cref="InviteFakeReason.None" />.</param>
/// <param name="JoinCount">How many times this member has joined the guild, including this time.</param>
public record InviteJoinResult(
    IUser? Inviter,
    string? Code,
    int Uses,
    string? Label,
    InviteJoinType JoinType,
    InviteFakeReason FakeReason,
    int JoinCount)
{
    /// <summary>
    ///     Whether the join was flagged as fake.
    /// </summary>
    public bool IsFake
    {
        get
        {
            return FakeReason != InviteFakeReason.None;
        }
    }
}