namespace Mewdeko.Modules.Achievements.Common;

/// <summary>
///     How rare an achievement is. Sets its default points and its badge color.
/// </summary>
public enum AchievementGrade
{
    /// <summary>
    ///     The easiest grade.
    /// </summary>
    Bronze = 0,

    /// <summary>
    ///     A step above bronze.
    /// </summary>
    Silver = 1,

    /// <summary>
    ///     A solid milestone.
    /// </summary>
    Gold = 2,

    /// <summary>
    ///     A long term goal.
    /// </summary>
    Emerald = 3,

    /// <summary>
    ///     A rare feat.
    /// </summary>
    Amethyst = 4,

    /// <summary>
    ///     The rarest grade.
    /// </summary>
    Champion = 5
}

/// <summary>
///     A number tracked for each member that an achievement can be unlocked at.
/// </summary>
public enum AchievementMetric
{
    /// <summary>
    ///     No metric.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Messages sent in the server, all time.
    /// </summary>
    MessagesTotal = 1,

    /// <summary>
    ///     Messages sent today, UTC.
    /// </summary>
    MessagesToday = 2,

    /// <summary>
    ///     Messages sent this week, Monday to Sunday UTC.
    /// </summary>
    MessagesThisWeek = 3,

    /// <summary>
    ///     Messages sent this month, UTC.
    /// </summary>
    MessagesThisMonth = 4,

    /// <summary>
    ///     Different text channels posted in.
    /// </summary>
    TextChannels = 5,

    /// <summary>
    ///     1 once the member has posted in every text channel they can post in.
    /// </summary>
    EveryTextChannel = 6,

    /// <summary>
    ///     Hours spent in voice.
    /// </summary>
    VoiceHours = 7,

    /// <summary>
    ///     Different voice channels joined.
    /// </summary>
    VoiceChannels = 8,

    /// <summary>
    ///     1 once the member has been in every voice channel they can join.
    /// </summary>
    EveryVoiceChannel = 9,

    /// <summary>
    ///     Times the member joined voice.
    /// </summary>
    VoiceJoins = 10,

    /// <summary>
    ///     Hours spent muted in voice.
    /// </summary>
    MutedHours = 11,

    /// <summary>
    ///     Reactions added.
    /// </summary>
    Reactions = 12,

    /// <summary>
    ///     Different emojis reacted with.
    /// </summary>
    UniqueEmojis = 13,

    /// <summary>
    ///     The most times the member reacted with one emoji.
    /// </summary>
    SameEmoji = 14,

    /// <summary>
    ///     Net invites from invite tracking.
    /// </summary>
    Invites = 15,

    /// <summary>
    ///     Commands run in the server.
    /// </summary>
    Commands = 16,

    /// <summary>
    ///     Days since the member joined the server.
    /// </summary>
    TenureDays = 17,

    /// <summary>
    ///     Full months the member has been boosting, or -1 when not boosting.
    /// </summary>
    BoostMonths = 18,

    /// <summary>
    ///     XP level in the server.
    /// </summary>
    XpLevel = 19,

    /// <summary>
    ///     Reputation in the server.
    /// </summary>
    Reputation = 20,

    /// <summary>
    ///     Wallet plus bank.
    /// </summary>
    NetWorth = 21,

    /// <summary>
    ///     Achievements unlocked in the server.
    /// </summary>
    Unlocked = 22,

    /// <summary>
    ///     Achievement points earned in the server.
    /// </summary>
    Points = 23,

    /// <summary>
    ///     Achievement points earned across every server.
    /// </summary>
    GlobalPoints = 24,

    /// <summary>
    ///     Servers the member has unlocked an achievement in.
    /// </summary>
    GlobalServers = 25,

    /// <summary>
    ///     Achievements unlocked across every server.
    /// </summary>
    GlobalUnlocks = 26
}

/// <summary>
///     What unlocks an achievement.
/// </summary>
public enum AchievementTrigger
{
    /// <summary>
    ///     Reaching a value of a metric.
    /// </summary>
    Metric = 0,

    /// <summary>
    ///     Sending a message containing a phrase.
    /// </summary>
    Keyword = 1,

    /// <summary>
    ///     Reacting with a specific emoji.
    /// </summary>
    Reaction = 2,

    /// <summary>
    ///     Only staff can hand it out.
    /// </summary>
    Manual = 3,

    /// <summary>
    ///     A one time moment the bot watches for, built in only.
    /// </summary>
    Feat = 4,

    /// <summary>
    ///     Unlocking every other achievement in a category, built in only.
    /// </summary>
    Completion = 5
}

/// <summary>
///     One time moments built in achievements watch for.
/// </summary>
public enum AchievementFeat
{
    /// <summary>
    ///     A first message.
    /// </summary>
    FirstMessage,

    /// <summary>
    ///     A message sent between 3 and 4 in the morning.
    /// </summary>
    NightOwl,

    /// <summary>
    ///     A poll.
    /// </summary>
    Poll,

    /// <summary>
    ///     A sticker.
    /// </summary>
    Sticker,

    /// <summary>
    ///     A message with a custom emoji.
    /// </summary>
    CustomEmoji,

    /// <summary>
    ///     A message with a file.
    /// </summary>
    Attachment,

    /// <summary>
    ///     A forwarded message.
    /// </summary>
    Forward,

    /// <summary>
    ///     A message mentioning someone else.
    /// </summary>
    Mention,

    /// <summary>
    ///     A reply to one's own message.
    /// </summary>
    SelfReply,

    /// <summary>
    ///     A mention of oneself.
    /// </summary>
    SelfMention,

    /// <summary>
    ///     Five messages within ten seconds.
    /// </summary>
    TypingStorm,

    /// <summary>
    ///     An edit within five seconds of sending.
    /// </summary>
    QuickEdit,

    /// <summary>
    ///     Deleting one's own message within five seconds of sending.
    /// </summary>
    QuickDelete,

    /// <summary>
    ///     A message of exactly 200 characters.
    /// </summary>
    Exact200,

    /// <summary>
    ///     A message of 1,000 characters or more.
    /// </summary>
    WallOfText,

    /// <summary>
    ///     A first voice channel join.
    /// </summary>
    FirstVoice,

    /// <summary>
    ///     Joining an empty voice channel.
    /// </summary>
    EmptyRoom,

    /// <summary>
    ///     Leaving voice within two seconds of joining.
    /// </summary>
    QuickExit,

    /// <summary>
    ///     Three different voice channels within thirty seconds.
    /// </summary>
    ChannelHopper,

    /// <summary>
    ///     A first reaction.
    /// </summary>
    FirstReaction,

    /// <summary>
    ///     Reacting to one's own message.
    /// </summary>
    SelfReact,

    /// <summary>
    ///     Reacting to a bot's message.
    /// </summary>
    BotReact,

    /// <summary>
    ///     Boosting the server.
    /// </summary>
    Boosted
}

/// <summary>
///     Where unlock announcements go.
/// </summary>
public enum AchievementAnnounceMode
{
    /// <summary>
    ///     The log channel when one is set, otherwise the channel it happened in.
    /// </summary>
    Auto = 0,

    /// <summary>
    ///     The channel it happened in.
    /// </summary>
    Here = 1,

    /// <summary>
    ///     The log channel only.
    /// </summary>
    LogChannel = 2,

    /// <summary>
    ///     Direct messages only.
    /// </summary>
    DmOnly = 3,

    /// <summary>
    ///     No announcements.
    /// </summary>
    Silent = 4
}

/// <summary>
///     Who can see a part of a member's achievements.
/// </summary>
public enum AchievementVisibility
{
    /// <summary>
    ///     Everyone.
    /// </summary>
    Everyone = 0,

    /// <summary>
    ///     Only the member.
    /// </summary>
    OnlyMe = 1
}

/// <summary>
///     Whether a member wants unlocks sent to their DMs.
/// </summary>
public enum AchievementDmPreference
{
    /// <summary>
    ///     Whatever the server chose.
    /// </summary>
    ServerDefault = 0,

    /// <summary>
    ///     Always.
    /// </summary>
    Always = 1,

    /// <summary>
    ///     Never.
    /// </summary>
    Never = 2
}

/// <summary>
///     What a privacy setting covers.
/// </summary>
public enum AchievementPrivacyArea
{
    /// <summary>
    ///     The profile card and stats.
    /// </summary>
    Profile,

    /// <summary>
    ///     The list of unlocked achievements.
    /// </summary>
    Achievements,

    /// <summary>
    ///     The badge inventory.
    /// </summary>
    Badges,

    /// <summary>
    ///     Showing up on leaderboards.
    /// </summary>
    Leaderboard
}

/// <summary>
///     A notification choice a member can turn on or off.
/// </summary>
public enum AchievementNotifyOption
{
    /// <summary>
    ///     DMs for unlocks.
    /// </summary>
    Dm,

    /// <summary>
    ///     Unlock messages in the server.
    /// </summary>
    Message,

    /// <summary>
    ///     Being mentioned in unlock messages.
    /// </summary>
    Mention
}

/// <summary>
///     How a leaderboard is ordered.
/// </summary>
public enum AchievementLeaderboardSort
{
    /// <summary>
    ///     Most points first.
    /// </summary>
    Points = 0,

    /// <summary>
    ///     Most achievements first.
    /// </summary>
    Unlocked = 1,

    /// <summary>
    ///     Most recent unlock first.
    /// </summary>
    Recent = 2
}

/// <summary>
///     Which metrics need checking again for a member.
/// </summary>
[Flags]
public enum AchievementDirty
{
    /// <summary>
    ///     Nothing.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Message metrics.
    /// </summary>
    Messages = 1,

    /// <summary>
    ///     Voice metrics.
    /// </summary>
    Voice = 2,

    /// <summary>
    ///     Reaction metrics.
    /// </summary>
    Reactions = 4,

    /// <summary>
    ///     Invites.
    /// </summary>
    Invites = 8,

    /// <summary>
    ///     Commands.
    /// </summary>
    Commands = 16,

    /// <summary>
    ///     Tenure and boosting.
    /// </summary>
    Membership = 32,

    /// <summary>
    ///     XP level.
    /// </summary>
    Xp = 64,

    /// <summary>
    ///     Reputation.
    /// </summary>
    Reputation = 128,

    /// <summary>
    ///     Net worth.
    /// </summary>
    Currency = 256,

    /// <summary>
    ///     Every metric.
    /// </summary>
    All = Messages | Voice | Reactions | Invites | Commands | Membership | Xp | Reputation | Currency
}
