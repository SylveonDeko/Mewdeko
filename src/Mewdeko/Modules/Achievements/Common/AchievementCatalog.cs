using System.Text;

namespace Mewdeko.Modules.Achievements.Common;

/// <summary>
///     A category achievements are grouped under.
/// </summary>
/// <param name="Key">Stable key.</param>
/// <param name="Name">Display name.</param>
/// <param name="Icon">Stored icon value; achievements without their own icon use it.</param>
/// <param name="Description">One line saying what the category covers.</param>
/// <param name="IsBuiltIn">Whether it ships with the bot.</param>
/// <param name="HasBadges">Whether its achievements give out category badges.</param>
public sealed record AchievementCategoryInfo(
    string Key,
    string Name,
    string Icon,
    string Description,
    bool IsBuiltIn,
    bool HasBadges);

/// <summary>
///     Facts about a grade.
/// </summary>
/// <param name="Grade">The grade.</param>
/// <param name="Name">Display name.</param>
/// <param name="Points">Default points for achievements of this grade.</param>
/// <param name="Color">Badge color as 24 bit RGB.</param>
public sealed record AchievementGradeInfo(
    AchievementGrade Grade,
    string Name,
    int Points,
    uint Color);

/// <summary>
///     Facts about a metric.
/// </summary>
/// <param name="Metric">The metric.</param>
/// <param name="Label">Short display name.</param>
/// <param name="Unit">Unit for one.</param>
/// <param name="UnitPlural">Unit for several.</param>
/// <param name="Description">What it counts.</param>
/// <param name="Dirty">Which change makes it worth checking again.</param>
/// <param name="AllowCustom">Whether servers can build achievements on it.</param>
/// <param name="Source">Which feature supplies the data, for the data sources panel.</param>
public sealed record AchievementMetricInfo(
    AchievementMetric Metric,
    string Label,
    string Unit,
    string UnitPlural,
    string Description,
    AchievementDirty Dirty,
    bool AllowCustom,
    string Source);

/// <summary>
///     A rank members climb by earning points.
/// </summary>
/// <param name="Grade">The grade the rank shares its color with, or null for the starting rank.</param>
/// <param name="Name">Display name.</param>
/// <param name="MinPoints">Points needed.</param>
public sealed record AchievementTier(AchievementGrade? Grade, string Name, int MinPoints);

/// <summary>
///     The built in achievements, categories, grades, metrics, and ranks.
/// </summary>
public static class AchievementCatalog
{
    /// <summary>
    ///     Category for server made achievements that did not pick one.
    /// </summary>
    public const string CustomCategory = "custom";

    /// <summary>
    ///     Category for completing other categories.
    /// </summary>
    public const string PrestigeCategory = "prestige";

    /// <summary>
    ///     Category for achievements earned across every server.
    /// </summary>
    public const string GlobalCategory = "global";

    /// <summary>
    ///     Prefix of custom achievement keys.
    /// </summary>
    public const string CustomKeyPrefix = "custom:";

    /// <summary>
    ///     Prefix of server made category keys.
    /// </summary>
    public const string CategoryKeyPrefix = "cat:";

    /// <summary>
    ///     Guild ID global unlocks are stored under.
    /// </summary>
    public const ulong GlobalGuildId = 0;

    /// <summary>
    ///     Number of badge slots on a profile.
    /// </summary>
    public const int BadgeSlots = 4;

    /// <summary>
    ///     Every grade, easiest first.
    /// </summary>
    public static readonly IReadOnlyList<AchievementGradeInfo> Grades =
    [
        new(AchievementGrade.Bronze, "Bronze", 10, 0xCD7F32),
        new(AchievementGrade.Silver, "Silver", 25, 0xC0C7D0),
        new(AchievementGrade.Gold, "Gold", 50, 0xF5C542),
        new(AchievementGrade.Emerald, "Emerald", 100, 0x34D399),
        new(AchievementGrade.Amethyst, "Amethyst", 250, 0xA78BFA),
        new(AchievementGrade.Champion, "Champion", 500, 0xFF5D73)
    ];

    /// <summary>
    ///     Ranks, lowest first.
    /// </summary>
    public static readonly IReadOnlyList<AchievementTier> Tiers =
    [
        new(null, "Newcomer", 0),
        new(AchievementGrade.Bronze, "Bronze", 100),
        new(AchievementGrade.Silver, "Silver", 500),
        new(AchievementGrade.Gold, "Gold", 1500),
        new(AchievementGrade.Emerald, "Emerald", 3500),
        new(AchievementGrade.Amethyst, "Amethyst", 7000),
        new(AchievementGrade.Champion, "Champion", 12000)
    ];

    /// <summary>
    ///     Built in categories in their default order.
    /// </summary>
    public static readonly IReadOnlyList<AchievementCategoryInfo> BuiltInCategories =
    [
        new("messages", "Messages", "fa:comments", "Chatting, daily and weekly challenges, and message moments", true,
            true),
        new("voice", "Voice", "fa:microphone", "Time in voice, joins, and voice moments", true, true),
        new("reactions", "Reactions", "fa:face-smile", "Reacting, emoji variety, and reaction moments", true, true),
        new("invites", "Invites", "fa:users", "Bringing people to the server", true, true),
        new("commands", "Commands", "fa:code", "Using the bot", true, true),
        new("loyalty", "Loyalty", "fa:calendar", "Time since joining the server", true, true),
        new("boosts", "Boosts", "fa:bolt", "Boosting the server and keeping it up", true, true),
        new("community", "Community", "fa:heart", "Levels and reputation", true, true),
        new(PrestigeCategory, "Prestige", "fa:crown", "Finishing whole categories", true, true),
        new(GlobalCategory, "Global", "fa:globe", "Earned across every server", true, false),
        new(CustomCategory, "Server", "fa:wand-magic-sparkles", "Achievements this server made", true, false)
    ];

    /// <summary>
    ///     Metrics with their labels, in the order editors list them.
    /// </summary>
    public static readonly IReadOnlyList<AchievementMetricInfo> Metrics =
    [
        new(AchievementMetric.MessagesTotal, "Messages", "message", "messages",
            "Messages sent in the server", AchievementDirty.Messages, true, "messages"),
        new(AchievementMetric.MessagesToday, "Messages today", "message", "messages",
            "Messages sent today, UTC", AchievementDirty.Messages, true, "messages"),
        new(AchievementMetric.MessagesThisWeek, "Messages this week", "message", "messages",
            "Messages sent since Monday, UTC", AchievementDirty.Messages, true, "messages"),
        new(AchievementMetric.MessagesThisMonth, "Messages this month", "message", "messages",
            "Messages sent since the 1st, UTC", AchievementDirty.Messages, true, "messages"),
        new(AchievementMetric.TextChannels, "Text channels", "channel", "channels",
            "Different text channels posted in", AchievementDirty.Messages, true, "messages"),
        new(AchievementMetric.EveryTextChannel, "Every text channel", "channel", "channels",
            "Posted in every text channel they can post in", AchievementDirty.Messages, false, "messages"),
        new(AchievementMetric.VoiceHours, "Voice hours", "hour", "hours",
            "Hours spent in voice", AchievementDirty.Voice, true, "voice"),
        new(AchievementMetric.VoiceChannels, "Voice channels", "channel", "channels",
            "Different voice channels joined", AchievementDirty.Voice, true, "voice"),
        new(AchievementMetric.EveryVoiceChannel, "Every voice channel", "channel", "channels",
            "Been in every voice channel they can join", AchievementDirty.Voice, false, "voice"),
        new(AchievementMetric.VoiceJoins, "Voice joins", "join", "joins",
            "Times they joined voice", AchievementDirty.Voice, true, "achievements"),
        new(AchievementMetric.MutedHours, "Muted hours", "hour", "hours",
            "Hours spent muted in voice", AchievementDirty.Voice, true, "achievements"),
        new(AchievementMetric.Reactions, "Reactions", "reaction", "reactions",
            "Reactions added", AchievementDirty.Reactions, true, "achievements"),
        new(AchievementMetric.UniqueEmojis, "Different emojis", "emoji", "emojis",
            "Different emojis reacted with", AchievementDirty.Reactions, true, "achievements"),
        new(AchievementMetric.SameEmoji, "Same emoji", "reaction", "reactions",
            "Most reactions with a single emoji", AchievementDirty.Reactions, true, "achievements"),
        new(AchievementMetric.Invites, "Invites", "invite", "invites",
            "Net invites from invite tracking", AchievementDirty.Invites, true, "invites"),
        new(AchievementMetric.Commands, "Commands", "command", "commands",
            "Bot commands run in the server", AchievementDirty.Commands, true, "commands"),
        new(AchievementMetric.TenureDays, "Days in server", "day", "days",
            "Days since joining the server", AchievementDirty.Membership, true, "discord"),
        new(AchievementMetric.BoostMonths, "Months boosting", "month", "months",
            "Full months of boosting without a break", AchievementDirty.Membership, true, "discord"),
        new(AchievementMetric.XpLevel, "XP level", "level", "levels",
            "XP level in the server", AchievementDirty.Xp, true, "xp"),
        new(AchievementMetric.Reputation, "Reputation", "rep", "rep",
            "Reputation in the server", AchievementDirty.Reputation, true, "reputation"),
        new(AchievementMetric.NetWorth, "Net worth", "coin", "coins",
            "Wallet plus bank", AchievementDirty.Currency, true, "currency"),
        new(AchievementMetric.Unlocked, "Achievements", "achievement", "achievements",
            "Achievements unlocked in the server", AchievementDirty.None, true, "achievements"),
        new(AchievementMetric.Points, "Points", "point", "points",
            "Achievement points earned in the server", AchievementDirty.None, true, "achievements"),
        new(AchievementMetric.GlobalPoints, "Global points", "point", "points",
            "Achievement points earned across every server", AchievementDirty.None, false, "achievements"),
        new(AchievementMetric.GlobalServers, "Servers", "server", "servers",
            "Servers with at least one unlock", AchievementDirty.None, false, "achievements"),
        new(AchievementMetric.GlobalUnlocks, "Global achievements", "achievement", "achievements",
            "Achievements unlocked across every server", AchievementDirty.None, false, "achievements")
    ];

    /// <summary>
    ///     Every built in achievement in its default form.
    /// </summary>
    public static readonly IReadOnlyList<AchievementDefinition> BuiltIns = BuildBuiltIns();

    private static readonly Dictionary<string, AchievementDefinition> BuiltInsByKey =
        BuiltIns.ToDictionary(x => x.Key, StringComparer.Ordinal);

    private static readonly Dictionary<AchievementMetric, AchievementMetricInfo> MetricsByKey =
        Metrics.ToDictionary(x => x.Metric);

    private static readonly Dictionary<string, AchievementCategoryInfo> CategoriesByKey =
        BuiltInCategories.ToDictionary(x => x.Key, StringComparer.Ordinal);

    /// <summary>
    ///     Looks up a built in achievement.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The achievement, or null.</returns>
    public static AchievementDefinition? GetBuiltIn(string key)
    {
        return BuiltInsByKey.GetValueOrDefault(key);
    }

    /// <summary>
    ///     Looks up a built in category.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The category, or null.</returns>
    public static AchievementCategoryInfo? GetBuiltInCategory(string key)
    {
        return CategoriesByKey.GetValueOrDefault(key);
    }

    /// <summary>
    ///     Looks up a metric.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <returns>Its facts.</returns>
    public static AchievementMetricInfo GetMetric(AchievementMetric metric)
    {
        return MetricsByKey.TryGetValue(metric, out var info)
            ? info
            : new AchievementMetricInfo(metric, metric.ToString(), "", "", "", AchievementDirty.None, false, "");
    }

    /// <summary>
    ///     Facts about a grade.
    /// </summary>
    /// <param name="grade">The grade.</param>
    /// <returns>Its facts.</returns>
    public static AchievementGradeInfo GetGrade(AchievementGrade grade)
    {
        var index = Math.Clamp((int)grade, 0, Grades.Count - 1);
        return Grades[index];
    }

    /// <summary>
    ///     The rank a points total reaches.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>The rank.</returns>
    public static AchievementTier GetTier(int points)
    {
        var tier = Tiers[0];
        foreach (var candidate in Tiers)
        {
            if (points >= candidate.MinPoints)
                tier = candidate;
        }

        return tier;
    }

    /// <summary>
    ///     The rank after the one a points total reaches.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>The next rank, or null at the top.</returns>
    public static AchievementTier? GetNextTier(int points)
    {
        return Tiers.FirstOrDefault(t => t.MinPoints > points);
    }

    /// <summary>
    ///     The custom achievement key for an ID.
    /// </summary>
    /// <param name="id">The database ID.</param>
    /// <returns>The key.</returns>
    public static string CustomKey(int id)
    {
        return $"{CustomKeyPrefix}{id}";
    }

    /// <summary>
    ///     The server made category key for an ID.
    /// </summary>
    /// <param name="id">The database ID.</param>
    /// <returns>The key.</returns>
    public static string CategoryKey(int id)
    {
        return $"{CategoryKeyPrefix}{id}";
    }

    /// <summary>
    ///     Reads the database ID out of a custom achievement key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="id">The ID.</param>
    /// <returns>True when the key is a custom key.</returns>
    public static bool TryParseCustomKey(string key, out int id)
    {
        id = 0;
        return key.StartsWith(CustomKeyPrefix, StringComparison.Ordinal) &&
               int.TryParse(key.AsSpan(CustomKeyPrefix.Length), out id);
    }

    /// <summary>
    ///     Reads the database ID out of a server made category key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="id">The ID.</param>
    /// <returns>True when the key is a server made category key.</returns>
    public static bool TryParseCategoryKey(string key, out int id)
    {
        id = 0;
        return key.StartsWith(CategoryKeyPrefix, StringComparison.Ordinal) &&
               int.TryParse(key.AsSpan(CategoryKeyPrefix.Length), out id);
    }

    private static List<AchievementDefinition> BuildBuiltIns()
    {
        var list = new List<AchievementDefinition>();

        AddLadder(list, "messages", "messages_total", AchievementMetric.MessagesTotal,
        [
            (250, AchievementGrade.Bronze, "Breaking the Ice"),
            (1000, AchievementGrade.Bronze, "Regular"),
            (5000, AchievementGrade.Silver, "Chatterbox"),
            (10000, AchievementGrade.Silver, "Conversationalist"),
            (25000, AchievementGrade.Gold, "Town Crier"),
            (50000, AchievementGrade.Emerald, "Wordsmith"),
            (100000, AchievementGrade.Amethyst, "Living Legend"),
            (250000, AchievementGrade.Champion, "Unstoppable")
        ], n => $"Send {n:N0} messages");

        AddLadder(list, "messages", "messages_day", AchievementMetric.MessagesToday,
        [
            (100, AchievementGrade.Bronze, "Busy Day"),
            (500, AchievementGrade.Silver, "On a Roll"),
            (1000, AchievementGrade.Gold, "Marathon Day"),
            (5000, AchievementGrade.Amethyst, "No Sleep"),
            (15000, AchievementGrade.Champion, "Keyboard Inferno")
        ], n => $"Send {n:N0} messages in one day (UTC)");

        AddLadder(list, "messages", "messages_week", AchievementMetric.MessagesThisWeek,
        [
            (100, AchievementGrade.Bronze, "Active Week"),
            (1000, AchievementGrade.Silver, "Week of Words"),
            (5000, AchievementGrade.Gold, "Weekly Powerhouse"),
            (15000, AchievementGrade.Emerald, "Relentless")
        ], n => $"Send {n:N0} messages in one week (Monday to Sunday, UTC)");

        AddLadder(list, "messages", "messages_month", AchievementMetric.MessagesThisMonth,
        [
            (1000, AchievementGrade.Bronze, "Monthly Regular"),
            (5000, AchievementGrade.Silver, "Month of Mayhem"),
            (15000, AchievementGrade.Gold, "Monthly Titan"),
            (50000, AchievementGrade.Amethyst, "Calendar Conqueror")
        ], n => $"Send {n:N0} messages in one calendar month (UTC)");

        AddLadder(list, "messages", "text_channels", AchievementMetric.TextChannels,
        [
            (5, AchievementGrade.Bronze, "Explorer"),
            (15, AchievementGrade.Silver, "Wanderer")
        ], n => $"Post in {n:N0} different text channels");

        list.Add(Metric("messages", "text_channels_all", "Been Everywhere", "Post in every text channel you can post in",
            AchievementGrade.Gold, AchievementMetric.EveryTextChannel, 1));

        list.Add(Feat("messages", AchievementFeat.FirstMessage, "Hello World", "Send your first message",
            AchievementGrade.Bronze));
        list.Add(Feat("messages", AchievementFeat.Poll, "Pollster", "Start a poll", AchievementGrade.Bronze));
        list.Add(Feat("messages", AchievementFeat.Sticker, "Sticker Slapper", "Send a sticker", AchievementGrade.Bronze));
        list.Add(Feat("messages", AchievementFeat.CustomEmoji, "Emoji Speaker", "Use a custom emoji in a message",
            AchievementGrade.Bronze));
        list.Add(Feat("messages", AchievementFeat.Attachment, "Show and Tell", "Share a file or image",
            AchievementGrade.Bronze));
        list.Add(Feat("messages", AchievementFeat.Forward, "Forwarder", "Forward a message", AchievementGrade.Bronze));
        list.Add(Feat("messages", AchievementFeat.Mention, "Hey You", "Mention someone", AchievementGrade.Bronze));
        list.Add(Feat("messages", AchievementFeat.WallOfText, "Wall of Text", "Send a message of 1,000 characters or more",
            AchievementGrade.Silver));
        list.Add(Feat("messages", AchievementFeat.NightOwl, "Night Owl",
            "Send a message between 3 and 4 in the morning, in your birthday timezone or UTC",
            AchievementGrade.Silver, true));
        list.Add(Feat("messages", AchievementFeat.SelfReply, "Talking to Myself", "Reply to your own message",
            AchievementGrade.Silver, true));
        list.Add(Feat("messages", AchievementFeat.SelfMention, "It's Me", "Mention yourself",
            AchievementGrade.Silver, true));
        list.Add(Feat("messages", AchievementFeat.TypingStorm, "Speed Typist", "Send 5 messages within 10 seconds",
            AchievementGrade.Silver, true));
        list.Add(Feat("messages", AchievementFeat.QuickEdit, "Wait, Let Me Fix That",
            "Edit a message within 5 seconds of sending it", AchievementGrade.Silver, true));
        list.Add(Feat("messages", AchievementFeat.QuickDelete, "Never Mind",
            "Delete your own message within 5 seconds of sending it", AchievementGrade.Silver, true));
        list.Add(Feat("messages", AchievementFeat.Exact200, "Perfectly Measured",
            "Send a message exactly 200 characters long", AchievementGrade.Gold, true));

        AddLadder(list, "voice", "voice_hours", AchievementMetric.VoiceHours,
        [
            (5, AchievementGrade.Bronze, "Tuned In"),
            (24, AchievementGrade.Bronze, "All Day Long"),
            (100, AchievementGrade.Silver, "Regular Caller"),
            (250, AchievementGrade.Gold, "On Air"),
            (500, AchievementGrade.Emerald, "Broadcaster"),
            (1000, AchievementGrade.Amethyst, "Voice Veteran"),
            (2500, AchievementGrade.Champion, "Never Hung Up")
        ], n => $"Spend {n:N0} hours in voice");

        AddLadder(list, "voice", "voice_joins", AchievementMetric.VoiceJoins,
        [
            (5, AchievementGrade.Bronze, "Dropping In"),
            (50, AchievementGrade.Silver, "Frequent Flyer"),
            (250, AchievementGrade.Gold, "Revolving Door"),
            (1000, AchievementGrade.Emerald, "Always Around"),
            (5000, AchievementGrade.Amethyst, "Permanent Fixture")
        ], n => $"Join voice {n:N0} times");

        AddLadder(list, "voice", "voice_muted", AchievementMetric.MutedHours,
        [
            (1, AchievementGrade.Bronze, "Quiet Listener"),
            (10, AchievementGrade.Silver, "Silent Partner"),
            (50, AchievementGrade.Gold, "Mime"),
            (100, AchievementGrade.Emerald, "Vow of Silence")
        ], n => $"Spend {n:N0} hours muted in voice");

        list.Add(Metric("voice", "voice_channels_3", "Room Hopper", "Join 3 different voice channels",
            AchievementGrade.Bronze, AchievementMetric.VoiceChannels, 3));
        list.Add(Metric("voice", "voice_channels_all", "Tour Guide", "Spend time in every voice channel you can join",
            AchievementGrade.Gold, AchievementMetric.EveryVoiceChannel, 1));
        list.Add(Feat("voice", AchievementFeat.FirstVoice, "Say Something", "Join a voice channel for the first time",
            AchievementGrade.Bronze));
        list.Add(Feat("voice", AchievementFeat.EmptyRoom, "Anyone Here?", "Join a voice channel nobody else is in",
            AchievementGrade.Silver, true));
        list.Add(Feat("voice", AchievementFeat.QuickExit, "Wrong Room", "Leave voice within 2 seconds of joining",
            AchievementGrade.Silver, true));
        list.Add(Feat("voice", AchievementFeat.ChannelHopper, "Channel Surfer",
            "Visit 3 voice channels within 30 seconds", AchievementGrade.Silver, true));

        AddLadder(list, "reactions", "reactions", AchievementMetric.Reactions,
        [
            (25, AchievementGrade.Bronze, "First Impressions"),
            (100, AchievementGrade.Bronze, "Reactor"),
            (500, AchievementGrade.Silver, "Emoji Enthusiast"),
            (1000, AchievementGrade.Gold, "Reaction Machine"),
            (5000, AchievementGrade.Emerald, "Reaction Royalty"),
            (15000, AchievementGrade.Champion, "Emoji Overlord")
        ], n => $"Add {n:N0} reactions");

        AddLadder(list, "reactions", "unique_emojis", AchievementMetric.UniqueEmojis,
        [
            (10, AchievementGrade.Bronze, "Collector"),
            (50, AchievementGrade.Silver, "Curator"),
            (250, AchievementGrade.Gold, "Archivist"),
            (1000, AchievementGrade.Emerald, "Emoji Encyclopedia"),
            (2500, AchievementGrade.Amethyst, "Emoji Omniscient")
        ], n => $"React with {n:N0} different emojis");

        list.Add(Feat("reactions", AchievementFeat.FirstReaction, "First Reaction", "Add your first reaction",
            AchievementGrade.Bronze));
        list.Add(Feat("reactions", AchievementFeat.SelfReact, "Self Love", "React to your own message",
            AchievementGrade.Silver, true));
        list.Add(Feat("reactions", AchievementFeat.BotReact, "Bot Appreciation", "React to a bot's message",
            AchievementGrade.Silver, true));
        list.Add(Metric("reactions", "same_emoji_50", "One Trick", "React with the same emoji 50 times",
            AchievementGrade.Silver, AchievementMetric.SameEmoji, 50, true));

        AddLadder(list, "invites", "invites", AchievementMetric.Invites,
        [
            (5, AchievementGrade.Bronze, "Friend Bringer"),
            (25, AchievementGrade.Silver, "Recruiter"),
            (100, AchievementGrade.Gold, "Ambassador"),
            (500, AchievementGrade.Emerald, "Community Builder"),
            (1000, AchievementGrade.Amethyst, "Growth Engine"),
            (2500, AchievementGrade.Champion, "Kingmaker")
        ], n => $"Invite {n:N0} people who stay");

        AddLadder(list, "commands", "commands", AchievementMetric.Commands,
        [
            (10, AchievementGrade.Bronze, "Button Pusher"),
            (100, AchievementGrade.Silver, "Power User"),
            (500, AchievementGrade.Gold, "Command Line"),
            (1000, AchievementGrade.Emerald, "Automation Fan"),
            (5000, AchievementGrade.Amethyst, "Bot Whisperer"),
            (10000, AchievementGrade.Champion, "Root Access")
        ], n => $"Run {n:N0} bot commands");

        AddLadder(list, "loyalty", "tenure", AchievementMetric.TenureDays,
        [
            (7, AchievementGrade.Bronze, "Settling In"),
            (30, AchievementGrade.Bronze, "One Month In"),
            (90, AchievementGrade.Silver, "Three Months Strong"),
            (180, AchievementGrade.Gold, "Half a Year"),
            (365, AchievementGrade.Emerald, "One Year Club"),
            (730, AchievementGrade.Amethyst, "Two Year Veteran"),
            (1095, AchievementGrade.Amethyst, "Three Year Veteran"),
            (1825, AchievementGrade.Champion, "Pillar of the Community")
        ], n => n switch
        {
            7 => "Stay in the server for a week",
            30 => "Stay in the server for a month",
            90 => "Stay in the server for 3 months",
            180 => "Stay in the server for 6 months",
            365 => "Stay in the server for a year",
            _ => $"Stay in the server for {n / 365} years"
        });

        list.Add(Feat("boosts", AchievementFeat.Boosted, "Booster", "Boost the server", AchievementGrade.Silver));
        AddLadder(list, "boosts", "boost_months", AchievementMetric.BoostMonths,
        [
            (1, AchievementGrade.Gold, "Boost Streak: 1 Month"),
            (3, AchievementGrade.Emerald, "Boost Streak: 3 Months"),
            (6, AchievementGrade.Amethyst, "Boost Streak: 6 Months"),
            (12, AchievementGrade.Champion, "Boost Streak: 1 Year")
        ], n => n == 1 ? "Keep boosting for a month" : $"Keep boosting for {n} months in a row");

        AddLadder(list, "community", "xp_level", AchievementMetric.XpLevel,
        [
            (5, AchievementGrade.Bronze, "Leveling Up"),
            (10, AchievementGrade.Silver, "Double Digits"),
            (25, AchievementGrade.Gold, "Seasoned"),
            (50, AchievementGrade.Emerald, "Elite"),
            (100, AchievementGrade.Champion, "Max Power")
        ], n => $"Reach XP level {n:N0}");

        AddLadder(list, "community", "reputation", AchievementMetric.Reputation,
        [
            (10, AchievementGrade.Bronze, "Trusted"),
            (50, AchievementGrade.Silver, "Respected"),
            (100, AchievementGrade.Gold, "Pillar"),
            (500, AchievementGrade.Amethyst, "Legend of Goodwill")
        ], n => $"Earn {n:N0} reputation");

        list.Add(Completion("messages", "Message Master"));
        list.Add(Completion("voice", "Voice Master"));
        list.Add(Completion("reactions", "Reaction Master"));
        list.Add(Completion("invites", "Invite Master"));
        list.Add(Completion("commands", "Command Master"));
        list.Add(Completion("loyalty", "Loyalty Master"));
        list.Add(Completion("boosts", "Boost Master"));
        list.Add(Completion("community", "Community Master"));
        list.Add(new AchievementDefinition
        {
            Key = "prestige_all",
            CategoryKey = PrestigeCategory,
            Name = "Completionist",
            Description = "Unlock every other achievement in the server",
            Grade = AchievementGrade.Champion,
            Points = GetGrade(AchievementGrade.Champion).Points * 2,
            Trigger = AchievementTrigger.Completion
        });

        AddGlobalLadder(list, "global_points", AchievementMetric.GlobalPoints,
        [
            (1000, AchievementGrade.Silver, "Rising Star"),
            (5000, AchievementGrade.Gold, "Celebrity"),
            (25000, AchievementGrade.Champion, "Icon")
        ], n => $"Earn {n:N0} achievement points across every server");

        AddGlobalLadder(list, "global_servers", AchievementMetric.GlobalServers,
        [
            (3, AchievementGrade.Bronze, "Traveler"),
            (5, AchievementGrade.Silver, "Globetrotter"),
            (10, AchievementGrade.Gold, "Citizen of Everywhere")
        ], n => $"Unlock achievements in {n:N0} servers");

        AddGlobalLadder(list, "global_unlocks", AchievementMetric.GlobalUnlocks,
        [
            (100, AchievementGrade.Silver, "Achievement Hunter"),
            (500, AchievementGrade.Amethyst, "Trophy Case")
        ], n => $"Unlock {n:N0} achievements across every server");

        return list.Select((d, i) => new AchievementDefinition
        {
            Key = d.Key,
            CategoryKey = d.CategoryKey,
            Name = d.Name,
            Description = d.Description,
            Grade = d.Grade,
            Points = d.Points,
            Hidden = d.Hidden,
            Trigger = d.Trigger,
            Metric = d.Metric,
            Threshold = d.Threshold,
            Feat = d.Feat,
            Keyword = d.Keyword,
            IsGlobal = d.IsGlobal,
            Position = i
        }).ToList();
    }

    private static void AddLadder(List<AchievementDefinition> list, string category, string prefix,
        AchievementMetric metric, (long Threshold, AchievementGrade Grade, string Name)[] steps,
        Func<long, string> describe)
    {
        foreach (var (threshold, grade, name) in steps)
            list.Add(Metric(category, $"{prefix}_{threshold}", name, describe(threshold), grade, metric, threshold));
    }

    private static void AddGlobalLadder(List<AchievementDefinition> list, string prefix, AchievementMetric metric,
        (long Threshold, AchievementGrade Grade, string Name)[] steps, Func<long, string> describe)
    {
        foreach (var (threshold, grade, name) in steps)
        {
            list.Add(new AchievementDefinition
            {
                Key = $"{prefix}_{threshold}",
                CategoryKey = GlobalCategory,
                Name = name,
                Description = describe(threshold),
                Grade = grade,
                Points = GetGrade(grade).Points,
                Trigger = AchievementTrigger.Metric,
                Metric = metric,
                Threshold = threshold,
                IsGlobal = true
            });
        }
    }

    private static AchievementDefinition Metric(string category, string key, string name, string description,
        AchievementGrade grade, AchievementMetric metric, long threshold, bool hidden = false)
    {
        return new AchievementDefinition
        {
            Key = key,
            CategoryKey = category,
            Name = name,
            Description = description,
            Grade = grade,
            Points = GetGrade(grade).Points,
            Hidden = hidden,
            Trigger = AchievementTrigger.Metric,
            Metric = metric,
            Threshold = threshold
        };
    }

    private static AchievementDefinition Feat(string category, AchievementFeat feat, string name, string description,
        AchievementGrade grade, bool hidden = false)
    {
        return new AchievementDefinition
        {
            Key = $"feat_{ToSnake(feat.ToString())}",
            CategoryKey = category,
            Name = name,
            Description = description,
            Grade = grade,
            Points = GetGrade(grade).Points,
            Hidden = hidden,
            Trigger = AchievementTrigger.Feat,
            Feat = feat
        };
    }

    private static AchievementDefinition Completion(string category, string name)
    {
        var categoryName = BuiltInCategories.First(c => c.Key == category).Name;
        return new AchievementDefinition
        {
            Key = $"prestige_{category}",
            CategoryKey = PrestigeCategory,
            Name = name,
            Description = $"Unlock every {categoryName} achievement",
            Grade = AchievementGrade.Amethyst,
            Points = GetGrade(AchievementGrade.Amethyst).Points,
            Trigger = AchievementTrigger.Completion,
            Keyword = category
        };
    }

    /// <summary>
    ///     The feat key for a feat.
    /// </summary>
    /// <param name="feat">The feat.</param>
    /// <returns>The achievement key.</returns>
    public static string FeatKey(AchievementFeat feat)
    {
        return $"feat_{ToSnake(feat.ToString())}";
    }

    private static string ToSnake(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
                builder.Append('_');
            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
