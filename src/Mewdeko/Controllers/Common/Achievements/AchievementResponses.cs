namespace Mewdeko.Controllers.Common.Achievements;

/// <summary>
///     One achievement as a server sees it.
/// </summary>
public class AchievementResponse
{
    /// <summary>
    ///     Stable key.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    ///     Category key.
    /// </summary>
    public string CategoryKey { get; set; } = "";

    /// <summary>
    ///     Display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     What it takes.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    ///     The icon it shows: its own or its category's, as <c>fa:name</c>, a custom emoji, an https URL, or
    ///     <c>upload:id</c>.
    /// </summary>
    public string Icon { get; set; } = "";

    /// <summary>
    ///     The icon's image, or null for glyph icons. Uploads on instances without a CDN give a path relative
    ///     to the API root.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    ///     0 Bronze to 5 Champion.
    /// </summary>
    public int Grade { get; set; }

    /// <summary>
    ///     Points it gives.
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    ///     Hidden until unlocked.
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    ///     Whether members can earn it right now, including its category being on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Whether the achievement itself is on, ignoring its category.
    /// </summary>
    public bool SelfEnabled { get; set; }

    /// <summary>
    ///     0 metric, 1 phrase, 2 reaction, 3 manual, 4 moment, 5 completion.
    /// </summary>
    public int Trigger { get; set; }

    /// <summary>
    ///     Metric value.
    /// </summary>
    public int Metric { get; set; }

    /// <summary>
    ///     Goal for metric achievements.
    /// </summary>
    public long Threshold { get; set; }

    /// <summary>
    ///     Phrase or emoji.
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    ///     Channel limit.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     Reward role.
    /// </summary>
    public ulong? RoleRewardId { get; set; }

    /// <summary>
    ///     Reward role name, or null when it is gone.
    /// </summary>
    public string? RoleRewardName { get; set; }

    /// <summary>
    ///     Reward currency.
    /// </summary>
    public long CurrencyReward { get; set; }

    /// <summary>
    ///     Reward XP.
    /// </summary>
    public int XpReward { get; set; }

    /// <summary>
    ///     Whether the server made it.
    /// </summary>
    public bool IsCustom { get; set; }

    /// <summary>
    ///     Database ID for server made achievements.
    /// </summary>
    public int? CustomId { get; set; }

    /// <summary>
    ///     Whether it is earned across every server.
    /// </summary>
    public bool IsGlobal { get; set; }

    /// <summary>
    ///     Whether the server changed a built in achievement.
    /// </summary>
    public bool IsOverridden { get; set; }

    /// <summary>
    ///     Members who unlocked it.
    /// </summary>
    public int UnlockCount { get; set; }

    /// <summary>
    ///     Built in name.
    /// </summary>
    public string? DefaultName { get; set; }

    /// <summary>
    ///     Built in description.
    /// </summary>
    public string? DefaultDescription { get; set; }

    /// <summary>
    ///     Built in points.
    /// </summary>
    public int? DefaultPoints { get; set; }

    /// <summary>
    ///     Built in hidden flag.
    /// </summary>
    public bool? DefaultHidden { get; set; }

    /// <summary>
    ///     Stored description: the custom description or the override, null when using the default.
    /// </summary>
    public string? RawDescription { get; set; }

    /// <summary>
    ///     Stored icon, null when it uses its category's.
    /// </summary>
    public string? RawIcon { get; set; }

    /// <summary>
    ///     Stored points: the custom points or the override, null when using the default.
    /// </summary>
    public int? RawPoints { get; set; }

    /// <summary>
    ///     Stored name override for built ins, null when using the default.
    /// </summary>
    public string? RawName { get; set; }
}

/// <summary>
///     A category with counts.
/// </summary>
public class AchievementCategoryResponse
{
    /// <summary>
    ///     Key.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    ///     Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Icon, in the same forms as an achievement's.
    /// </summary>
    public string Icon { get; set; } = "";

    /// <summary>
    ///     The icon's image, or null for glyph icons.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    ///     Description.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    ///     Whether it ships with the bot.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    ///     Whether it gives out badges.
    /// </summary>
    public bool HasBadges { get; set; }

    /// <summary>
    ///     Whether it is on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Database ID for server made categories.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    ///     Achievements in it.
    /// </summary>
    public int AchievementCount { get; set; }

    /// <summary>
    ///     Achievements in it that are on.
    /// </summary>
    public int EnabledCount { get; set; }
}

/// <summary>
///     A grade.
/// </summary>
public class AchievementGradeResponse
{
    /// <summary>
    ///     0 to 5.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    ///     Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Default points.
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    ///     Color as "#RRGGBB".
    /// </summary>
    public string Color { get; set; } = "";
}

/// <summary>
///     A metric.
/// </summary>
public class AchievementMetricResponse
{
    /// <summary>
    ///     Numeric value.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    ///     Enum name.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    ///     Label.
    /// </summary>
    public string Label { get; set; } = "";

    /// <summary>
    ///     Unit for one.
    /// </summary>
    public string Unit { get; set; } = "";

    /// <summary>
    ///     Unit for several.
    /// </summary>
    public string UnitPlural { get; set; } = "";

    /// <summary>
    ///     What it counts.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    ///     Data source key.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    ///     Whether servers can build achievements on it.
    /// </summary>
    public bool AllowCustom { get; set; }
}

/// <summary>
///     A rank.
/// </summary>
public class AchievementTierResponse
{
    /// <summary>
    ///     Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Points needed.
    /// </summary>
    public int MinPoints { get; set; }

    /// <summary>
    ///     Grade it shares a color with, or null.
    /// </summary>
    public int? Grade { get; set; }
}

/// <summary>
///     A placeholder for unlock messages.
/// </summary>
public class AchievementPlaceholderResponse
{
    /// <summary>
    ///     The placeholder.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     What it becomes.
    /// </summary>
    public string Description { get; set; } = "";
}

/// <summary>
///     Size limits.
/// </summary>
public class AchievementLimitsResponse
{
    /// <summary>
    ///     Most server made achievements.
    /// </summary>
    public int MaxCustomAchievements { get; set; }

    /// <summary>
    ///     Most server made categories.
    /// </summary>
    public int MaxCustomCategories { get; set; }

    /// <summary>
    ///     Longest name.
    /// </summary>
    public int NameLength { get; set; }

    /// <summary>
    ///     Longest description.
    /// </summary>
    public int DescriptionLength { get; set; }

    /// <summary>
    ///     Longest phrase.
    /// </summary>
    public int KeywordLength { get; set; }

    /// <summary>
    ///     Longest unlock message.
    /// </summary>
    public int MessageLength { get; set; }

    /// <summary>
    ///     Most points.
    /// </summary>
    public int MaxPoints { get; set; }

    /// <summary>
    ///     Most currency or XP.
    /// </summary>
    public long MaxReward { get; set; }

    /// <summary>
    ///     Badge slots.
    /// </summary>
    public int BadgeSlots { get; set; }
}

/// <summary>
///     Everything a server can earn, plus reference data for editors.
/// </summary>
public class AchievementCatalogResponse
{
    /// <summary>
    ///     Categories in the server's order.
    /// </summary>
    public List<AchievementCategoryResponse> Categories { get; set; } = [];

    /// <summary>
    ///     Achievements in display order.
    /// </summary>
    public List<AchievementResponse> Achievements { get; set; } = [];

    /// <summary>
    ///     Grades.
    /// </summary>
    public List<AchievementGradeResponse> Grades { get; set; } = [];

    /// <summary>
    ///     Metrics.
    /// </summary>
    public List<AchievementMetricResponse> Metrics { get; set; } = [];

    /// <summary>
    ///     Ranks.
    /// </summary>
    public List<AchievementTierResponse> Tiers { get; set; } = [];

    /// <summary>
    ///     Unlock message placeholders.
    /// </summary>
    public List<AchievementPlaceholderResponse> Placeholders { get; set; } = [];

    /// <summary>
    ///     Size limits.
    /// </summary>
    public AchievementLimitsResponse Limits { get; set; } = new();

    /// <summary>
    ///     Images the server uploaded for icons.
    /// </summary>
    public List<AchievementIconUploadResponse> Uploads { get; set; } = [];
}

/// <summary>
///     An image a server uploaded for icons.
/// </summary>
public class AchievementIconUploadResponse
{
    /// <summary>
    ///     The upload ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The icon value that uses it: <c>upload:id</c>.
    /// </summary>
    public string Icon { get; set; } = "";

    /// <summary>
    ///     Where it is served: a CDN URL or a path relative to the API root.
    /// </summary>
    public string Url { get; set; } = "";
}

/// <summary>
///     One Font Awesome glyph the icon picker offers.
/// </summary>
public class AchievementGlyphResponse
{
    /// <summary>
    ///     The name, used as <c>fa:name</c>.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The primary layer's code point; the bundled font draws the secondary layer at this plus 0x100000.
    /// </summary>
    public int Codepoint { get; set; }

    /// <summary>
    ///     Other names to search by.
    /// </summary>
    public List<string> Aliases { get; set; } = [];
}

/// <summary>
///     A server's achievement settings.
/// </summary>
public class AchievementSettingsResponse
{
    /// <summary>
    ///     Whether members can earn achievements.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     0 auto, 1 where it happened, 2 log channel, 3 DMs only, 4 silent.
    /// </summary>
    public int AnnounceMode { get; set; }

    /// <summary>
    ///     Log channel.
    /// </summary>
    public ulong? LogChannelId { get; set; }

    /// <summary>
    ///     DMs unless members turn them off.
    /// </summary>
    public bool DmByDefault { get; set; }

    /// <summary>
    ///     Mention members in unlock messages.
    /// </summary>
    public bool MentionUsers { get; set; }

    /// <summary>
    ///     Unlock message source, or null for the default.
    /// </summary>
    public string? UnlockMessage { get; set; }

    /// <summary>
    ///     XP per point.
    /// </summary>
    public int XpPerPoint { get; set; }

    /// <summary>
    ///     Hidden achievements show their names before unlocking.
    /// </summary>
    public bool RevealHidden { get; set; }

    /// <summary>
    ///     Unlock messages carry a generated image of the achievement.
    /// </summary>
    public bool UnlockImage { get; set; }

    /// <summary>
    ///     Seconds after which unlock messages in channels are deleted, or 0 to keep them.
    /// </summary>
    public int DeleteAfter { get; set; }

    /// <summary>
    ///     Categories turned off.
    /// </summary>
    public List<string> DisabledCategories { get; set; } = [];

    /// <summary>
    ///     Category order.
    /// </summary>
    public List<string> CategoryOrder { get; set; } = [];

    /// <summary>
    ///     Roles that earn nothing.
    /// </summary>
    public List<ulong> ExcludedRoleIds { get; set; } = [];

    /// <summary>
    ///     Channels that earn nothing.
    /// </summary>
    public List<ulong> ExcludedChannelIds { get; set; } = [];

    /// <summary>
    ///     When existing members were first checked, or null when not yet.
    /// </summary>
    public DateTime? BackfilledAt { get; set; }
}

/// <summary>
///     A recent unlock.
/// </summary>
public class AchievementRecentUnlockResponse
{
    /// <summary>
    ///     The member.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Their name.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    ///     Their avatar.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    ///     Achievement key.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    ///     Achievement name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Achievement icon.
    /// </summary>
    public string Icon { get; set; } = "";

    /// <summary>
    ///     The icon's image, or null for glyph icons.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    ///     Grade.
    /// </summary>
    public int Grade { get; set; }

    /// <summary>
    ///     When.
    /// </summary>
    public DateTime UnlockedAt { get; set; }
}

/// <summary>
///     An achievement and how many unlocked it.
/// </summary>
public class AchievementRarityResponse
{
    /// <summary>
    ///     Key.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    ///     Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Achievement icon.
    /// </summary>
    public string Icon { get; set; } = "";

    /// <summary>
    ///     The icon's image, or null for glyph icons.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    ///     Grade.
    /// </summary>
    public int Grade { get; set; }

    /// <summary>
    ///     Members who unlocked it.
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
///     The dashboard overview.
/// </summary>
public class AchievementOverviewResponse
{
    /// <summary>
    ///     Settings.
    /// </summary>
    public AchievementSettingsResponse Settings { get; set; } = new();

    /// <summary>
    ///     Data source key to whether it is on.
    /// </summary>
    public Dictionary<string, bool> DataSources { get; set; } = [];

    /// <summary>
    ///     Members with at least one unlock.
    /// </summary>
    public int Members { get; set; }

    /// <summary>
    ///     Unlocks in total.
    /// </summary>
    public int Unlocks { get; set; }

    /// <summary>
    ///     Unlocks in the last 7 days.
    /// </summary>
    public int UnlocksThisWeek { get; set; }

    /// <summary>
    ///     Achievements members can earn.
    /// </summary>
    public int Earnable { get; set; }

    /// <summary>
    ///     Achievements in total, on or off.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     Server made achievements.
    /// </summary>
    public int CustomCount { get; set; }

    /// <summary>
    ///     Latest unlocks.
    /// </summary>
    public List<AchievementRecentUnlockResponse> Recent { get; set; } = [];

    /// <summary>
    ///     Most unlocked achievements.
    /// </summary>
    public List<AchievementRarityResponse> MostCommon { get; set; } = [];

    /// <summary>
    ///     Least unlocked achievements that someone has.
    /// </summary>
    public List<AchievementRarityResponse> Rarest { get; set; } = [];
}

/// <summary>
///     A channel for selectors.
/// </summary>
public class AchievementChannelLookup
{
    /// <summary>
    ///     ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Category name.
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    ///     0 text, 2 voice, other Discord channel types.
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    ///     Whether the bot can post there.
    /// </summary>
    public bool CanSend { get; set; }
}

/// <summary>
///     A role for selectors.
/// </summary>
public class AchievementRoleLookup
{
    /// <summary>
    ///     ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     24 bit color.
    /// </summary>
    public uint Color { get; set; }

    /// <summary>
    ///     Position.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    ///     Whether the bot can give it out.
    /// </summary>
    public bool Assignable { get; set; }
}

/// <summary>
///     A server emoji for pickers.
/// </summary>
public class AchievementEmojiLookup
{
    /// <summary>
    ///     ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     "&lt;:name:id&gt;" form.
    /// </summary>
    public string Formatted { get; set; } = "";

    /// <summary>
    ///     Image URL.
    /// </summary>
    public string Url { get; set; } = "";
}

/// <summary>
///     Channels, roles, and emojis for editors.
/// </summary>
public class AchievementLookupsResponse
{
    /// <summary>
    ///     Text and voice channels.
    /// </summary>
    public List<AchievementChannelLookup> Channels { get; set; } = [];

    /// <summary>
    ///     Roles, highest first.
    /// </summary>
    public List<AchievementRoleLookup> Roles { get; set; } = [];

    /// <summary>
    ///     Server emojis.
    /// </summary>
    public List<AchievementEmojiLookup> Emojis { get; set; } = [];

    /// <summary>
    ///     Whether the bot has Manage Roles.
    /// </summary>
    public bool BotCanManageRoles { get; set; }
}

/// <summary>
///     A member's totals.
/// </summary>
public class AchievementMemberResponse
{
    /// <summary>
    ///     The member.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Username.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    ///     Display name in the server.
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    ///     Their account's display name, when they set one.
    /// </summary>
    public string? GlobalName { get; set; }

    /// <summary>
    ///     Avatar.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    ///     Points.
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    ///     Achievements unlocked.
    /// </summary>
    public int Unlocked { get; set; }

    /// <summary>
    ///     Rank name.
    /// </summary>
    public string Tier { get; set; } = "";

    /// <summary>
    ///     Rank grade, or null for the starting rank.
    /// </summary>
    public int? TierGrade { get; set; }

    /// <summary>
    ///     Latest unlock.
    /// </summary>
    public DateTime? LastUnlockAt { get; set; }

    /// <summary>
    ///     Place on the points leaderboard, 0 when unranked.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    ///     Whether they are still in the server.
    /// </summary>
    public bool InServer { get; set; }
}

/// <summary>
///     A page of members.
/// </summary>
public class AchievementMembersResponse
{
    /// <summary>
    ///     Rows matching the filter.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     The page.
    /// </summary>
    public List<AchievementMemberResponse> Members { get; set; } = [];
}

/// <summary>
///     Where a member stands on one achievement.
/// </summary>
public class AchievementProgressResponse
{
    /// <summary>
    ///     Achievement key.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    ///     When unlocked, or null.
    /// </summary>
    public DateTime? UnlockedAt { get; set; }

    /// <summary>
    ///     Current metric value, or null.
    /// </summary>
    public long? Current { get; set; }
}

/// <summary>
///     A badge.
/// </summary>
public class AchievementBadgeResponse
{
    /// <summary>
    ///     Key.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    ///     Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Icon.
    /// </summary>
    public string Icon { get; set; } = "";

    /// <summary>
    ///     The icon's image, or null for glyph icons.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    ///     Grade.
    /// </summary>
    public int Grade { get; set; }

    /// <summary>
    ///     How to earn it.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    ///     Short label.
    /// </summary>
    public string Short { get; set; } = "";
}

/// <summary>
///     One member in detail.
/// </summary>
public class AchievementMemberDetailResponse
{
    /// <summary>
    ///     Totals.
    /// </summary>
    public AchievementMemberResponse Member { get; set; } = new();

    /// <summary>
    ///     Achievements members can earn.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     Progress on every achievement shown.
    /// </summary>
    public List<AchievementProgressResponse> Progress { get; set; } = [];

    /// <summary>
    ///     Owned badges.
    /// </summary>
    public List<AchievementBadgeResponse> Badges { get; set; } = [];

    /// <summary>
    ///     Equipped badge keys by slot.
    /// </summary>
    public List<string?> Equipped { get; set; } = [];
}

/// <summary>
///     A member's own preferences.
/// </summary>
public class AchievementUserSettingsResponse
{
    /// <summary>
    ///     0 everyone, 1 only me.
    /// </summary>
    public int ProfileVisibility { get; set; }

    /// <summary>
    ///     0 everyone, 1 only me.
    /// </summary>
    public int AchievementsVisibility { get; set; }

    /// <summary>
    ///     0 everyone, 1 only me.
    /// </summary>
    public int BadgesVisibility { get; set; }

    /// <summary>
    ///     Off leaderboards.
    /// </summary>
    public bool HideFromLeaderboards { get; set; }

    /// <summary>
    ///     0 server default, 1 always, 2 never.
    /// </summary>
    public int DmUnlocks { get; set; }

    /// <summary>
    ///     Unlocks announced in the server.
    /// </summary>
    public bool ShowInLog { get; set; }

    /// <summary>
    ///     Mentioned in unlock messages.
    /// </summary>
    public bool MentionMe { get; set; }
}

/// <summary>
///     A member's own view of their achievements in a server.
/// </summary>
public class AchievementMeResponse
{
    /// <summary>
    ///     Whether the server has achievements on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Totals.
    /// </summary>
    public AchievementMemberResponse Member { get; set; } = new();

    /// <summary>
    ///     Achievements members can earn.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     Points needed for the next rank, or null at the top.
    /// </summary>
    public int? NextTierPoints { get; set; }

    /// <summary>
    ///     Next rank name, or null at the top.
    /// </summary>
    public string? NextTier { get; set; }

    /// <summary>
    ///     Points the current rank starts at.
    /// </summary>
    public int TierPoints { get; set; }

    /// <summary>
    ///     Categories in order.
    /// </summary>
    public List<AchievementCategoryResponse> Categories { get; set; } = [];

    /// <summary>
    ///     Achievements, with hidden ones masked until unlocked.
    /// </summary>
    public List<AchievementResponse> Achievements { get; set; } = [];

    /// <summary>
    ///     Progress on each achievement.
    /// </summary>
    public List<AchievementProgressResponse> Progress { get; set; } = [];

    /// <summary>
    ///     Owned badges.
    /// </summary>
    public List<AchievementBadgeResponse> Badges { get; set; } = [];

    /// <summary>
    ///     Equipped badge keys by slot.
    /// </summary>
    public List<string?> Equipped { get; set; } = [];

    /// <summary>
    ///     Preferences.
    /// </summary>
    public AchievementUserSettingsResponse Settings { get; set; } = new();

    /// <summary>
    ///     Global points.
    /// </summary>
    public long GlobalPoints { get; set; }

    /// <summary>
    ///     Global unlocks.
    /// </summary>
    public long GlobalUnlocked { get; set; }

    /// <summary>
    ///     Servers with an unlock.
    /// </summary>
    public int GlobalServers { get; set; }

    /// <summary>
    ///     Grades, for colors.
    /// </summary>
    public List<AchievementGradeResponse> Grades { get; set; } = [];
}

/// <summary>
///     A server's card designs, which one is the default, what uses which, and what the designer needs.
/// </summary>
public class AchievementCardResponse
{
    /// <summary>
    ///     Saved designs, oldest first.
    /// </summary>
    public List<AchievementCardDesignResponse> Designs { get; set; } = [];

    /// <summary>
    ///     The design every achievement uses unless its category or itself picks another, or null for the built in
    ///     design.
    /// </summary>
    public int? DefaultId { get; set; }

    /// <summary>
    ///     The built in design, the starting point for new designs.
    /// </summary>
    public required global::Mewdeko.Modules.Achievements.Common.AchievementCardTemplate BuiltIn { get; set; }

    /// <summary>
    ///     Which design categories and achievements pick instead of the default.
    /// </summary>
    public required global::Mewdeko.Modules.Achievements.Common.AchievementCardAssignments Assignments { get; set; }

    /// <summary>
    ///     Images uploaded for the card.
    /// </summary>
    public List<AchievementIconUploadResponse> Images { get; set; } = [];

    /// <summary>
    ///     Placeholders text elements can use.
    /// </summary>
    public List<string> Placeholders { get; set; } = [];

    /// <summary>
    ///     The server's dashboard palette as the bot derives it, for swatches.
    /// </summary>
    public Dictionary<string, string> Palette { get; set; } = [];

    /// <summary>
    ///     Size limits.
    /// </summary>
    public AchievementCardLimitsResponse Limits { get; set; } = new();
}

/// <summary>
///     Limits of a card design.
/// </summary>
public class AchievementCardLimitsResponse
{
    /// <summary>Smallest card width.</summary>
    public int MinWidth { get; set; } = global::Mewdeko.Modules.Achievements.Common.AchievementCardTemplate.MinWidth;

    /// <summary>Largest card width.</summary>
    public int MaxWidth { get; set; } = global::Mewdeko.Modules.Achievements.Common.AchievementCardTemplate.MaxWidth;

    /// <summary>Smallest card height.</summary>
    public int MinHeight { get; set; } = global::Mewdeko.Modules.Achievements.Common.AchievementCardTemplate.MinHeight;

    /// <summary>Largest card height.</summary>
    public int MaxHeight { get; set; } = global::Mewdeko.Modules.Achievements.Common.AchievementCardTemplate.MaxHeight;

    /// <summary>Most elements.</summary>
    public int MaxElements { get; set; } = global::Mewdeko.Modules.Achievements.Common.AchievementCardTemplate.MaxElements;

    /// <summary>Longest element text.</summary>
    public int MaxText { get; set; } = global::Mewdeko.Modules.Achievements.Common.AchievementCardTemplate.MaxText;

    /// <summary>Most card images.</summary>
    public int MaxImages { get; set; } = global::Mewdeko.Modules.Achievements.Services.AchievementIconService.MaxCardUploads;

    /// <summary>Most saved designs.</summary>
    public int MaxDesigns { get; set; } = global::Mewdeko.Modules.Achievements.Services.AchievementService.MaxCardDesigns;

    /// <summary>Longest design name.</summary>
    public int NameLength { get; set; } = global::Mewdeko.Modules.Achievements.Services.AchievementService.CardNameLength;
}

/// <summary>
///     A saved card design.
/// </summary>
public class AchievementCardDesignResponse
{
    /// <summary>The design ID.</summary>
    public int Id { get; set; }

    /// <summary>Its name.</summary>
    public string Name { get; set; } = "";

    /// <summary>The design.</summary>
    public required global::Mewdeko.Modules.Achievements.Common.AchievementCardTemplate Template { get; set; }

    /// <summary>When it was last saved.</summary>
    public DateTime DateUpdated { get; set; }
}

/// <summary>
///     A drawn card preview and where each element landed on it.
/// </summary>
public class AchievementCardPreviewResponse
{
    /// <summary>
    ///     The PNG as a data URI.
    /// </summary>
    public string Image { get; set; } = "";

    /// <summary>
    ///     Card width, without the transparent margin.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Card height, without the transparent margin.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     The transparent margin around the card on every side, in pixels.
    /// </summary>
    public int Margin { get; set; }

    /// <summary>
    ///     Where each element landed, in card pixels.
    /// </summary>
    public List<AchievementCardBoxResponse> Layout { get; set; } = [];

    /// <summary>
    ///     The design as the bot read it, after limits were applied.
    /// </summary>
    public global::Mewdeko.Modules.Achievements.Common.AchievementCardTemplate? Template { get; set; }
}

/// <summary>
///     Where one element landed on a card.
/// </summary>
public class AchievementCardBoxResponse
{
    /// <summary>The element ID.</summary>
    public string Id { get; set; } = "";

    /// <summary>Left edge.</summary>
    public float X { get; set; }

    /// <summary>Top edge.</summary>
    public float Y { get; set; }

    /// <summary>Width.</summary>
    public float W { get; set; }

    /// <summary>Height.</summary>
    public float H { get; set; }

    /// <summary>Whether it was drawn on this card.</summary>
    public bool Drawn { get; set; }
}
