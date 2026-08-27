namespace Mewdeko.Modules.StatChannels.Common;

/// <summary>
///     The type of statistic displayed in a stat channel.
/// </summary>
public enum StatChannelType
{
    /// <summary>
    ///     Total member count.
    /// </summary>
    TotalMembers = 0,

    /// <summary>
    ///     Human (non-bot) member count.
    /// </summary>
    HumanMembers = 1,

    /// <summary>
    ///     Bot count.
    /// </summary>
    BotCount = 2,

    /// <summary>
    ///     Online member count.
    /// </summary>
    OnlineMembers = 3,

    /// <summary>
    ///     Members with a specific role.
    /// </summary>
    RoleMembers = 4,

    /// <summary>
    ///     Total channel count.
    /// </summary>
    ChannelCount = 5,

    /// <summary>
    ///     Total role count.
    /// </summary>
    RoleCount = 6,

    /// <summary>
    ///     Server boost count.
    /// </summary>
    BoostCount = 7,

    /// <summary>
    ///     Server boost level/tier.
    /// </summary>
    BoostLevel = 8,

    /// <summary>
    ///     Emoji count.
    /// </summary>
    EmojiCount = 9,

    /// <summary>
    ///     Countdown to a date.
    /// </summary>
    Countdown = 10,

    /// <summary>
    ///     Member goal progress.
    /// </summary>
    MemberGoal = 11,

    /// <summary>
    ///     Members currently marked idle.
    /// </summary>
    IdleMembers = 12,

    /// <summary>
    ///     Members currently marked do not disturb.
    /// </summary>
    DndMembers = 13,

    /// <summary>
    ///     Members currently streaming.
    /// </summary>
    StreamingMembers = 14,

    /// <summary>
    ///     Members currently connected to any voice channel.
    /// </summary>
    InVoiceMembers = 15,

    /// <summary>
    ///     Text channel count.
    /// </summary>
    TextChannelCount = 16,

    /// <summary>
    ///     Voice channel count.
    /// </summary>
    VoiceChannelCount = 17,

    /// <summary>
    ///     Category count.
    /// </summary>
    CategoryCount = 18,

    /// <summary>
    ///     Stage channel count.
    /// </summary>
    StageChannelCount = 19,

    /// <summary>
    ///     Forum channel count.
    /// </summary>
    ForumChannelCount = 20,

    /// <summary>
    ///     Active thread count.
    /// </summary>
    ThreadCount = 21,

    /// <summary>
    ///     Animated emoji count.
    /// </summary>
    AnimatedEmojiCount = 22,

    /// <summary>
    ///     Static emoji count.
    /// </summary>
    StaticEmojiCount = 23,

    /// <summary>
    ///     Sticker count.
    /// </summary>
    StickerCount = 24,

    /// <summary>
    ///     Emoji slots used against the tier cap.
    /// </summary>
    EmojiSlotsUsed = 25,

    /// <summary>
    ///     Days since the server was created.
    /// </summary>
    ServerAge = 26,

    /// <summary>
    ///     The most recently joined member.
    /// </summary>
    NewestMember = 27,

    /// <summary>
    ///     Members who joined in the last 24 hours.
    /// </summary>
    MembersJoinedToday = 28,

    /// <summary>
    ///     Members who joined in the last 7 days.
    /// </summary>
    MembersJoinedWeek = 29,

    /// <summary>
    ///     Scheduled event count.
    /// </summary>
    EventCount = 30,

    /// <summary>
    ///     Tracked invite count.
    /// </summary>
    InviteCount = 31,

    /// <summary>
    ///     Boosts remaining until the next boost tier.
    /// </summary>
    NextBoostGoal = 32,

    /// <summary>
    ///     Bot uptime in hours.
    /// </summary>
    BotUptime = 33,

    /// <summary>
    ///     Whether the linked Twitch channel is live.
    /// </summary>
    TwitchLiveStatus = 34,

    /// <summary>
    ///     Current Twitch viewer count.
    /// </summary>
    TwitchViewers = 35,

    /// <summary>
    ///     Current Twitch category being streamed.
    /// </summary>
    TwitchGame = 36,

    /// <summary>
    ///     Hours the current Twitch stream has been live.
    /// </summary>
    TwitchUptime = 37,

    /// <summary>
    ///     Twitch follower total.
    /// </summary>
    TwitchFollowers = 38,

    /// <summary>
    ///     Twitch subscriber total.
    /// </summary>
    TwitchSubs = 39,

    /// <summary>
    ///     A named Twitch chat counter.
    /// </summary>
    TwitchCounter = 40,

    /// <summary>
    ///     The most recent Twitch raider.
    /// </summary>
    TwitchLastRaider = 41,

    /// <summary>
    ///     Twitch stream sessions started recently. Bounded by how long Twitch event history is retained.
    /// </summary>
    TwitchRecentStreams = 42,

    /// <summary>
    ///     Players online on a watched Minecraft server.
    /// </summary>
    MinecraftPlayers = 43,

    /// <summary>
    ///     Whether a watched Minecraft server is online.
    /// </summary>
    MinecraftStatus = 44,

    /// <summary>
    ///     Current number in a counting channel.
    /// </summary>
    CountingCurrent = 45,

    /// <summary>
    ///     Highest number ever reached in a counting channel.
    /// </summary>
    CountingRecord = 46,

    /// <summary>
    ///     Open ticket count.
    /// </summary>
    OpenTickets = 47,

    /// <summary>
    ///     Tickets opened in the last 24 hours.
    /// </summary>
    TicketsToday = 48,

    /// <summary>
    ///     Suggestions still awaiting a decision.
    /// </summary>
    PendingSuggestions = 49,

    /// <summary>
    ///     Currently running giveaways.
    /// </summary>
    ActiveGiveaways = 50,

    /// <summary>
    ///     Total starboard posts.
    /// </summary>
    StarboardPosts = 51,

    /// <summary>
    ///     The name of the highest XP member.
    /// </summary>
    TopXpUser = 52,

    /// <summary>
    ///     Total XP earned across the guild.
    /// </summary>
    TotalGuildXp = 53,

    /// <summary>
    ///     Members currently marked AFK.
    /// </summary>
    AfkCount = 54,

    /// <summary>
    ///     Total guild currency in circulation.
    /// </summary>
    TotalCurrency = 55,

    /// <summary>
    ///     Currently active polls.
    /// </summary>
    ActivePolls = 56
}