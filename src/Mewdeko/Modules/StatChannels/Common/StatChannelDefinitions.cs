namespace Mewdeko.Modules.StatChannels.Common;

/// <summary>
///     The shape of the value a stat type produces.
/// </summary>
public enum StatChannelValueKind
{
    /// <summary>
    ///     A number that display styles apply to.
    /// </summary>
    Number = 0,

    /// <summary>
    ///     Free text such as a member or category name.
    /// </summary>
    Text = 1,

    /// <summary>
    ///     A true or false state rendered with the configured true and false text.
    /// </summary>
    Boolean = 2
}

/// <summary>
///     Extra configuration a stat type needs before it can resolve.
/// </summary>
public enum StatChannelRequirement
{
    /// <summary>
    ///     No extra configuration.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Needs a role.
    /// </summary>
    Role = 1,

    /// <summary>
    ///     Needs a target date.
    /// </summary>
    Date = 2,

    /// <summary>
    ///     Needs a numeric goal target.
    /// </summary>
    Goal = 3,

    /// <summary>
    ///     Needs the name of a Twitch chat counter.
    /// </summary>
    CounterName = 4,

    /// <summary>
    ///     Needs a counting channel.
    /// </summary>
    CountingChannel = 5,

    /// <summary>
    ///     Needs a configured Minecraft server.
    /// </summary>
    MinecraftServer = 6
}

/// <summary>
///     Descriptive metadata for a stat type, used to drive command choices, the dashboard pickers and previews.
/// </summary>
/// <param name="Type">The stat type.</param>
/// <param name="Name">The human readable name.</param>
/// <param name="Category">The grouping this stat belongs to.</param>
/// <param name="Description">A one line description.</param>
/// <param name="DefaultTemplate">The template applied when the user does not supply one.</param>
/// <param name="Placeholders">Placeholders specific to this stat, on top of the common set.</param>
/// <param name="ValueKind">The shape of the produced value.</param>
/// <param name="Requirement">Extra configuration this stat needs.</param>
/// <param name="Example">A rendered example of the default template.</param>
/// <param name="RecommendedStyle">The display style that suits this stat best.</param>
/// <param name="Realtime">Whether this stat benefits from a faster than default refresh.</param>
public record StatChannelDefinition(
    StatChannelType Type,
    string Name,
    string Category,
    string Description,
    string DefaultTemplate,
    string[] Placeholders,
    StatChannelValueKind ValueKind,
    StatChannelRequirement Requirement,
    string Example,
    StatChannelDisplayStyle RecommendedStyle,
    bool Realtime);

/// <summary>
///     The catalogue of every stat type Mewdeko can display in a channel name.
/// </summary>
public static class StatChannelDefinitions
{
    /// <summary>
    ///     Placeholders usable by every stat type.
    /// </summary>
    public static readonly string[] CommonPlaceholders =
    [
        "%count%", "%count.raw%", "%server.name%", "%server.id%",
        "%server.members%", "%server.boostcount%", "%server.boostlevel%"
    ];

    /// <summary>
    ///     Every stat definition in display order.
    /// </summary>
    public static readonly IReadOnlyList<StatChannelDefinition> All =
    [
        new StatChannelDefinition(StatChannelType.TotalMembers, "Total Members", "Members",
            "Every member in the server, bots included.", "👥 Members: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "👥 Members: 12,480",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.HumanMembers, "Human Members", "Members",
            "Members that are not bots.", "🧍 Humans: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🧍 Humans: 12,033",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.BotCount, "Bots", "Members",
            "Bot accounts in the server.", "🤖 Bots: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🤖 Bots: 447",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.OnlineMembers, "Online Members", "Members",
            "Members with any non-offline presence.", "🟢 Online: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🟢 Online: 1,204",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.IdleMembers, "Idle Members", "Members",
            "Members currently marked idle.", "🌙 Idle: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🌙 Idle: 318",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.DndMembers, "Do Not Disturb", "Members",
            "Members currently marked do not disturb.", "⛔ DND: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "⛔ DND: 96",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.StreamingMembers, "Streaming Members", "Members",
            "Members with a streaming activity.", "📺 Streaming: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "📺 Streaming: 7",
            StatChannelDisplayStyle.Comma, true),
        new StatChannelDefinition(StatChannelType.InVoiceMembers, "In Voice", "Members",
            "Members connected to any voice channel.", "🔊 In Voice: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🔊 In Voice: 23",
            StatChannelDisplayStyle.Comma, true),
        new StatChannelDefinition(StatChannelType.RoleMembers, "Role Members", "Members",
            "Members holding a specific role.", "%role.name%: %count%", ["%role.name%", "%role.id%", "%role.color%"],
            StatChannelValueKind.Number, StatChannelRequirement.Role, "Verified: 8,910",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.AfkCount, "AFK Members", "AFK",
            "Members currently marked AFK by Mewdeko.", "💤 AFK: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "💤 AFK: 42",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.NewestMember, "Newest Member", "Members",
            "The most recently joined member.", "👋 Newest: %member.name%", ["%member.name%", "%member.id%"],
            StatChannelValueKind.Text, StatChannelRequirement.None, "👋 Newest: sylveondeko",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.MembersJoinedToday, "Joined Today", "Members",
            "Members who joined in the last 24 hours.", "📈 Joined Today: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "📈 Joined Today: 137",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.MembersJoinedWeek, "Joined This Week", "Members",
            "Members who joined in the last 7 days.", "📈 This Week: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "📈 This Week: 902",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.MemberGoal, "Member Goal", "Members",
            "Progress toward a member count target.", "🎯 %count% / %goal%",
            ["%goal%", "%goal.raw%", "%goal.percent%", "%goal.remaining%", "%goal.bar%"],
            StatChannelValueKind.Number, StatChannelRequirement.Goal, "🎯 12,480 / 15,000",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.Countdown, "Countdown", "Server",
            "Time remaining until a target date.", "⏳ %days%d %hours%h left",
            ["%days%", "%hours%", "%minutes%", "%total.hours%"],
            StatChannelValueKind.Number, StatChannelRequirement.Date, "⏳ 12d 6h left",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.ChannelCount, "All Channels", "Server",
            "Every channel in the server.", "📁 Channels: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "📁 Channels: 184",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.TextChannelCount, "Text Channels", "Server",
            "Text channels only.", "💬 Text: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "💬 Text: 96",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.VoiceChannelCount, "Voice Channels", "Server",
            "Voice channels only.", "🔊 Voice: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🔊 Voice: 41",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.CategoryCount, "Categories", "Server",
            "Channel categories.", "🗂️ Categories: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🗂️ Categories: 14",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.StageChannelCount, "Stage Channels", "Server",
            "Stage channels only.", "🎙️ Stages: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🎙️ Stages: 3",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.ForumChannelCount, "Forum Channels", "Server",
            "Forum channels only.", "🗣️ Forums: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🗣️ Forums: 6",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.ThreadCount, "Active Threads", "Server",
            "Threads currently cached as active.", "🧵 Threads: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🧵 Threads: 58",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.RoleCount, "Roles", "Server",
            "Roles in the server.", "🎭 Roles: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🎭 Roles: 72",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.BoostCount, "Boosts", "Server",
            "Active Nitro boosts.", "💎 Boosts: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "💎 Boosts: 34",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.BoostLevel, "Boost Level", "Server",
            "Current boost tier, 0 through 3.", "💎 Level %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "💎 Level 3",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.NextBoostGoal, "Boosts To Next Tier", "Server",
            "Boosts still needed to reach the next tier.", "💎 %count% to Tier %tier.next%",
            ["%tier.next%", "%tier.current%"],
            StatChannelValueKind.Number, StatChannelRequirement.None, "💎 4 to Tier 3",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.EmojiCount, "Emojis", "Server",
            "All custom emojis.", "😀 Emojis: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "😀 Emojis: 210",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.AnimatedEmojiCount, "Animated Emojis", "Server",
            "Animated custom emojis.", "✨ Animated: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "✨ Animated: 88",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.StaticEmojiCount, "Static Emojis", "Server",
            "Non-animated custom emojis.", "😀 Static: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "😀 Static: 122",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.StickerCount, "Stickers", "Server",
            "Custom stickers.", "🏷️ Stickers: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🏷️ Stickers: 31",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.EmojiSlotsUsed, "Emoji Slots", "Server",
            "Emoji slots used against the tier cap.", "😀 Slots: %count%/%slots.max%", ["%slots.max%", "%slots.free%"],
            StatChannelValueKind.Number, StatChannelRequirement.None, "😀 Slots: 210/250",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.ServerAge, "Server Age", "Server",
            "Days since the server was created.", "🎂 Age: %count% days", ["%years%", "%created%"],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🎂 Age: 2,190 days",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.EventCount, "Scheduled Events", "Server",
            "Scheduled events currently listed.", "📅 Events: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "📅 Events: 4",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.InviteCount, "Tracked Invites", "Invite Tracking",
            "Invites tracked by the invite counter.", "🔗 Invites: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🔗 Invites: 3,812",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.BotUptime, "Bot Uptime", "Server",
            "How long Mewdeko has been running.", "🟢 Uptime: %days%d %hours%h", ["%days%", "%hours%", "%minutes%"],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🟢 Uptime: 6d 14h",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.TwitchLiveStatus, "Twitch Live Status", "Twitch",
            "Whether the linked Twitch channel is live right now.", "%twitch.name%: %status%",
            ["%status%", "%twitch.name%"],
            StatChannelValueKind.Boolean, StatChannelRequirement.None, "sylveondeko: 🔴 LIVE",
            StatChannelDisplayStyle.Plain, true),
        new StatChannelDefinition(StatChannelType.TwitchViewers, "Twitch Viewers", "Twitch",
            "Viewers on the current Twitch stream.", "👁️ Viewers: %count%", ["%twitch.name%"],
            StatChannelValueKind.Number, StatChannelRequirement.None, "👁️ Viewers: 1,204",
            StatChannelDisplayStyle.Comma, true),
        new StatChannelDefinition(StatChannelType.TwitchGame, "Twitch Category", "Twitch",
            "The category the Twitch channel is streaming.", "🎮 %game%", ["%game%", "%title%", "%twitch.name%"],
            StatChannelValueKind.Text, StatChannelRequirement.None, "🎮 Just Chatting",
            StatChannelDisplayStyle.Plain, true),
        new StatChannelDefinition(StatChannelType.TwitchUptime, "Twitch Stream Uptime", "Twitch",
            "How long the current Twitch stream has been live.", "⏱️ Live %hours%h %minutes%m",
            ["%hours%", "%minutes%"],
            StatChannelValueKind.Number, StatChannelRequirement.None, "⏱️ Live 3h 42m",
            StatChannelDisplayStyle.Plain, true),
        new StatChannelDefinition(StatChannelType.TwitchFollowers, "Twitch Followers", "Twitch",
            "Total Twitch followers.", "💜 Followers: %count%", ["%twitch.name%"],
            StatChannelValueKind.Number, StatChannelRequirement.None, "💜 Followers: 48.2K",
            StatChannelDisplayStyle.Compact, false),
        new StatChannelDefinition(StatChannelType.TwitchSubs, "Twitch Subscribers", "Twitch",
            "Total Twitch subscribers.", "⭐ Subs: %count%", ["%twitch.name%"],
            StatChannelValueKind.Number, StatChannelRequirement.None, "⭐ Subs: 1,340",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.TwitchCounter, "Twitch Chat Counter", "Twitch",
            "A named counter driven by Twitch chat commands.", "%counter.name%: %count%", ["%counter.name%"],
            StatChannelValueKind.Number, StatChannelRequirement.CounterName, "Deaths: 27",
            StatChannelDisplayStyle.Plain, true),
        new StatChannelDefinition(StatChannelType.TwitchLastRaider, "Last Twitch Raider", "Twitch",
            "The most recent channel that raided.", "⚔️ Last Raid: %raider%", ["%raider%", "%raid.viewers%"],
            StatChannelValueKind.Text, StatChannelRequirement.None, "⚔️ Last Raid: shroud",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.TwitchRecentStreams, "Recent Twitch Streams", "Twitch",
            "Stream sessions started in the last 14 days.", "📆 Streams: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "📆 Streams: 9",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.MinecraftPlayers, "Minecraft Players", "Minecraft",
            "Players currently online on a watched server.", "⛏️ Players: %count%/%players.max%",
            ["%players.max%", "%server.version%"],
            StatChannelValueKind.Number, StatChannelRequirement.MinecraftServer, "⛏️ Players: 34/100",
            StatChannelDisplayStyle.Plain, true),
        new StatChannelDefinition(StatChannelType.MinecraftStatus, "Minecraft Status", "Minecraft",
            "Whether a watched Minecraft server is reachable.", "⛏️ %status%", ["%status%", "%latency%"],
            StatChannelValueKind.Boolean, StatChannelRequirement.MinecraftServer, "⛏️ 🟢 Online",
            StatChannelDisplayStyle.Plain, true),
        new StatChannelDefinition(StatChannelType.CountingCurrent, "Counting Current", "Counting",
            "The current number in a counting channel.", "🔢 Count: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.CountingChannel, "🔢 Count: 48,201",
            StatChannelDisplayStyle.Comma, true),
        new StatChannelDefinition(StatChannelType.CountingRecord, "Counting Record", "Counting",
            "The highest number ever reached in a counting channel.", "🏆 Record: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.CountingChannel, "🏆 Record: 52,908",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.OpenTickets, "Open Tickets", "Tickets",
            "Tickets that are open and not archived.", "🎫 Open Tickets: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🎫 Open Tickets: 12",
            StatChannelDisplayStyle.Plain, true),
        new StatChannelDefinition(StatChannelType.TicketsToday, "Tickets Today", "Tickets",
            "Tickets opened in the last 24 hours.", "🎫 Today: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🎫 Today: 29",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.PendingSuggestions, "Pending Suggestions", "Suggestions",
            "Suggestions still awaiting a decision.", "💡 Pending: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "💡 Pending: 47",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.ActiveGiveaways, "Active Giveaways", "Giveaways",
            "Giveaways currently running.", "🎉 Giveaways: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🎉 Giveaways: 3",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.StarboardPosts, "Starboard Posts", "Starboard",
            "Total posts pinned to the starboard.", "⭐ Starred: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "⭐ Starred: 1,908",
            StatChannelDisplayStyle.Comma, false),
        new StatChannelDefinition(StatChannelType.TopXpUser, "Top XP Member", "XP",
            "The member with the most XP.", "🏅 Top: %member.name%", ["%member.name%", "%member.id%", "%member.xp%"],
            StatChannelValueKind.Text, StatChannelRequirement.None, "🏅 Top: sylveondeko",
            StatChannelDisplayStyle.Plain, false),
        new StatChannelDefinition(StatChannelType.TotalGuildXp, "Total Guild XP", "XP",
            "All XP earned across the server.", "📊 Total XP: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "📊 Total XP: 84.3M",
            StatChannelDisplayStyle.Compact, false),
        new StatChannelDefinition(StatChannelType.TotalCurrency, "Currency In Circulation", "Currency",
            "Total guild currency held by members.", "💰 In Circulation: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "💰 In Circulation: 12.4M",
            StatChannelDisplayStyle.Compact, false),
        new StatChannelDefinition(StatChannelType.ActivePolls, "Active Polls", "Polls",
            "Polls currently accepting votes.", "🗳️ Polls: %count%", [],
            StatChannelValueKind.Number, StatChannelRequirement.None, "🗳️ Polls: 2",
            StatChannelDisplayStyle.Plain, false),
    ];

    private static readonly Dictionary<StatChannelType, StatChannelDefinition> ByType =
        All.ToDictionary(x => x.Type);

    /// <summary>
    ///     Gets the definition for a stat type.
    /// </summary>
    /// <param name="type">The stat type.</param>
    /// <returns>The definition, or null when the type is unknown.</returns>
    public static StatChannelDefinition? Get(StatChannelType type)
    {
        return ByType.GetValueOrDefault(type);
    }

    /// <summary>
    ///     Gets the default template for a stat type.
    /// </summary>
    /// <param name="type">The stat type.</param>
    /// <returns>The default template.</returns>
    public static string DefaultTemplate(StatChannelType type)
    {
        return Get(type)?.DefaultTemplate ?? "%count%";
    }
}