namespace Mewdeko.Modules.ServerStats.Common;

/// <summary>
///     A ranked entity with its stat value.
/// </summary>
/// <param name="Id">The user or channel snowflake.</param>
/// <param name="Value">Messages, or voice seconds.</param>
public record TopEntry(ulong Id, long Value);

/// <summary>
///     A guild wide activity summary for a window.
/// </summary>
/// <param name="LookbackDays">The window in days, or 0 for all time.</param>
/// <param name="Messages">Messages sent.</param>
/// <param name="VoiceSeconds">Seconds spent in voice, summed across members.</param>
/// <param name="MessageContributors">Distinct members who sent a message.</param>
/// <param name="VoiceContributors">Distinct members who spent time in voice.</param>
/// <param name="Joins">Members who joined.</param>
/// <param name="Leaves">Members who left.</param>
/// <param name="TopMessageUser">The most active chatter, or null.</param>
/// <param name="TopVoiceUser">The member with the most voice time, or null.</param>
/// <param name="TopMessageChannel">The busiest text channel, or null.</param>
/// <param name="TopVoiceChannel">The busiest voice channel, or null.</param>
public record ServerOverview(
    int LookbackDays,
    long Messages,
    long VoiceSeconds,
    int MessageContributors,
    int VoiceContributors,
    int Joins,
    int Leaves,
    TopEntry? TopMessageUser,
    TopEntry? TopVoiceUser,
    TopEntry? TopMessageChannel,
    TopEntry? TopVoiceChannel);

/// <summary>
///     One member's activity for a window.
/// </summary>
/// <param name="UserId">The member.</param>
/// <param name="LookbackDays">The window in days, or 0 for all time.</param>
/// <param name="Messages">Messages sent in the window.</param>
/// <param name="VoiceSeconds">Voice seconds in the window.</param>
/// <param name="MessageRank">1-based rank by messages, or null when the member sent none.</param>
/// <param name="VoiceRank">1-based rank by voice time, or null when the member has none.</param>
/// <param name="TopMessageChannels">The channels the member chats in most.</param>
/// <param name="TopVoiceChannels">The voice channels the member sits in most.</param>
/// <param name="AllTimeMessages">Lifetime messages.</param>
/// <param name="AllTimeVoiceSeconds">Lifetime voice seconds.</param>
public record UserActivity(
    ulong UserId,
    int LookbackDays,
    long Messages,
    long VoiceSeconds,
    int? MessageRank,
    int? VoiceRank,
    IReadOnlyList<TopEntry> TopMessageChannels,
    IReadOnlyList<TopEntry> TopVoiceChannels,
    long AllTimeMessages,
    long AllTimeVoiceSeconds);

/// <summary>
///     One channel's activity for a window.
/// </summary>
/// <param name="ChannelId">The channel.</param>
/// <param name="LookbackDays">The window in days, or 0 for all time.</param>
/// <param name="Messages">Messages sent in the channel.</param>
/// <param name="VoiceSeconds">Voice seconds spent in the channel.</param>
/// <param name="Contributors">Distinct members who were active in the channel.</param>
/// <param name="TopMessageUsers">The most active chatters in the channel.</param>
/// <param name="TopVoiceUsers">The members with the most voice time in the channel.</param>
public record ChannelActivity(
    ulong ChannelId,
    int LookbackDays,
    long Messages,
    long VoiceSeconds,
    int Contributors,
    IReadOnlyList<TopEntry> TopMessageUsers,
    IReadOnlyList<TopEntry> TopVoiceUsers);

/// <summary>
///     One game or app with the time members spent in it.
/// </summary>
/// <param name="Name">The activity name.</param>
/// <param name="ApplicationId">The Discord application, when known.</param>
/// <param name="Type">The Discord activity type.</param>
/// <param name="Seconds">Seconds spent across all members.</param>
/// <param name="Players">Distinct members who spent time in it.</param>
public record ActivitySummary(string Name, ulong? ApplicationId, ActivityType Type, long Seconds, int Players);

/// <summary>
///     One point on a time series.
/// </summary>
/// <param name="Bucket">The start of the bucket, UTC.</param>
/// <param name="Value">The value for the bucket.</param>
public record SeriesPoint(DateTime Bucket, double Value);

/// <summary>
///     A named series for charting.
/// </summary>
/// <param name="Label">The legend label.</param>
/// <param name="Points">The points, oldest first.</param>
public record ChartSeries(string Label, IReadOnlyList<SeriesPoint> Points);