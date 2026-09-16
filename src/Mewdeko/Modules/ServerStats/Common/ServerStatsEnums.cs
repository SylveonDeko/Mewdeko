namespace Mewdeko.Modules.ServerStats.Common;

/// <summary>
///     The voice state a member was in for the length of a voice segment. Combined as flags.
/// </summary>
[Flags]
public enum VoiceStateFlags
{
    /// <summary>
    ///     Unmuted, undeafened, in a normal channel with company.
    /// </summary>
    Normal = 0,

    /// <summary>
    ///     The member muted themselves.
    /// </summary>
    SelfMuted = 1,

    /// <summary>
    ///     The member deafened themselves.
    /// </summary>
    SelfDeafened = 2,

    /// <summary>
    ///     A moderator muted the member.
    /// </summary>
    ServerMuted = 4,

    /// <summary>
    ///     A moderator deafened the member.
    /// </summary>
    ServerDeafened = 8,

    /// <summary>
    ///     The member sat in the guild's AFK channel.
    /// </summary>
    Afk = 16,

    /// <summary>
    ///     The member was the only human in the channel.
    /// </summary>
    Alone = 32,

    /// <summary>
    ///     The member was streaming.
    /// </summary>
    Streaming = 64,

    /// <summary>
    ///     The member had their camera on.
    /// </summary>
    Video = 128
}

/// <summary>
///     What a server stats exclusion targets.
/// </summary>
public enum StatsExclusionKind
{
    /// <summary>
    ///     A channel whose messages and voice time are not counted.
    /// </summary>
    Channel = 0,

    /// <summary>
    ///     A role whose holders are not counted.
    /// </summary>
    Role = 1,

    /// <summary>
    ///     A member who is not counted in this guild.
    /// </summary>
    User = 2
}

/// <summary>
///     Which activity a leaderboard or export ranks.
/// </summary>
public enum StatKind
{
    /// <summary>
    ///     Messages sent.
    /// </summary>
    Messages = 0,

    /// <summary>
    ///     Time spent in voice.
    /// </summary>
    Voice = 1,

    /// <summary>
    ///     Time spent in presence activities such as games.
    /// </summary>
    Activity = 2
}

/// <summary>
///     How the activity name filter list is applied.
/// </summary>
public enum ActivityFilterMode
{
    /// <summary>
    ///     Listed activities are ignored; everything else is tracked.
    /// </summary>
    Blacklist = 0,

    /// <summary>
    ///     Only listed activities are tracked.
    /// </summary>
    Whitelist = 1
}

/// <summary>
///     What a live board shows.
/// </summary>
public enum LiveBoardKind
{
    /// <summary>
    ///     Top inviters.
    /// </summary>
    InviteLeaderboard = 0,

    /// <summary>
    ///     Top chatters.
    /// </summary>
    MessageLeaderboard = 1,

    /// <summary>
    ///     Top voice members.
    /// </summary>
    VoiceLeaderboard = 2,

    /// <summary>
    ///     Joins per day chart.
    /// </summary>
    JoinsChart = 3,

    /// <summary>
    ///     Leaves per day chart.
    /// </summary>
    LeavesChart = 4,

    /// <summary>
    ///     Joins and leaves per day chart.
    /// </summary>
    GrowthChart = 5,

    /// <summary>
    ///     Member count chart.
    /// </summary>
    MembersChart = 6,

    /// <summary>
    ///     Messages per day chart.
    /// </summary>
    MessagesChart = 7,

    /// <summary>
    ///     The server activity overview embed.
    /// </summary>
    ServerOverview = 8,

    /// <summary>
    ///     The invite growth analytics embed.
    /// </summary>
    InviteStats = 9,

    /// <summary>
    ///     Most played games and apps.
    /// </summary>
    ActivityLeaderboard = 10
}

/// <summary>
///     How often a server report is posted.
/// </summary>
public enum ReportFrequency
{
    /// <summary>
    ///     Every day at 00:00 UTC.
    /// </summary>
    Daily = 0,

    /// <summary>
    ///     Every Monday at 00:00 UTC.
    /// </summary>
    Weekly = 1,

    /// <summary>
    ///     The first of every month at 00:00 UTC.
    /// </summary>
    Monthly = 2
}

/// <summary>
///     The charts the stats module can draw.
/// </summary>
public enum StatChartKind
{
    /// <summary>
    ///     Messages per day.
    /// </summary>
    Messages = 0,

    /// <summary>
    ///     Voice hours per day.
    /// </summary>
    Voice = 1,

    /// <summary>
    ///     Member count over time.
    /// </summary>
    Members = 2,

    /// <summary>
    ///     Online, idle, do not disturb and offline members over time.
    /// </summary>
    Status = 3,

    /// <summary>
    ///     Joins per day.
    /// </summary>
    Joins = 4,

    /// <summary>
    ///     Leaves per day.
    /// </summary>
    Leaves = 5,

    /// <summary>
    ///     Joins and leaves per day on one chart.
    /// </summary>
    Growth = 6,

    /// <summary>
    ///     Members in voice over time.
    /// </summary>
    InVoice = 7,

    /// <summary>
    ///     Hours per game or app, as bars.
    /// </summary>
    Activities = 8
}