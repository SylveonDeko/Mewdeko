using DataModel;

namespace Mewdeko.Modules.Achievements.Common;

/// <summary>
///     A server's achievement settings with its lists parsed.
/// </summary>
public sealed class AchievementGuildSettings
{
    /// <summary>
    ///     The stored row.
    /// </summary>
    public required AchievementSetting Row { get; init; }

    /// <summary>
    ///     Built in or server made categories turned off.
    /// </summary>
    public required HashSet<string> DisabledCategories { get; init; }

    /// <summary>
    ///     Category keys in the order the server chose. Keys not listed follow in default order.
    /// </summary>
    public required List<string> CategoryOrder { get; init; }

    /// <summary>
    ///     Members with any of these roles earn nothing.
    /// </summary>
    public required HashSet<ulong> ExcludedRoles { get; init; }

    /// <summary>
    ///     Activity in these channels earns nothing.
    /// </summary>
    public required HashSet<ulong> ExcludedChannels { get; init; }

    /// <summary>
    ///     Which saved card designs categories and achievements use instead of the server's default.
    /// </summary>
    public required AchievementCardAssignments CardAssignments { get; init; }

    /// <summary>
    ///     Whether members can earn achievements in the server.
    /// </summary>
    public bool Enabled => Row.Enabled;

    /// <summary>
    ///     Where unlocks are announced.
    /// </summary>
    public AchievementAnnounceMode AnnounceMode => (AchievementAnnounceMode)Row.AnnounceMode;
}

/// <summary>
///     Everything a server can earn, resolved from the built in catalog, its overrides, and its own achievements.
/// </summary>
public sealed class AchievementGuildCatalog
{
    /// <summary>
    ///     Every achievement, enabled or not, in display order.
    /// </summary>
    public required IReadOnlyList<AchievementDefinition> All { get; init; }

    /// <summary>
    ///     Every achievement by key.
    /// </summary>
    public required IReadOnlyDictionary<string, AchievementDefinition> ByKey { get; init; }

    /// <summary>
    ///     Categories in the server's order, with server made ones included.
    /// </summary>
    public required IReadOnlyList<AchievementCategoryInfo> Categories { get; init; }

    /// <summary>
    ///     Server made categories by key.
    /// </summary>
    public required IReadOnlyDictionary<string, AchievementCategory> CustomCategories { get; init; }

    /// <summary>
    ///     Achievements members can currently earn in the server, excluding global ones.
    /// </summary>
    public required IReadOnlyList<AchievementDefinition> Earnable { get; init; }

    /// <summary>
    ///     Earnable keyword and reaction achievements, checked against every message and reaction.
    /// </summary>
    public required IReadOnlyList<AchievementDefinition> Watchers { get; init; }

    /// <summary>
    ///     Where the server's uploaded icons are served, by upload ID: a public URL on the CDN or the
    ///     dashboard, or an API path relative to the API root when neither is available.
    /// </summary>
    public required IReadOnlyDictionary<int, string> UploadUrls { get; init; }

    /// <summary>
    ///     Uploads made for the card designer rather than as icons. They are in <see cref="UploadUrls" /> but kept out
    ///     of the icon picker.
    /// </summary>
    public required IReadOnlySet<int> CardUploads { get; init; }

    /// <summary>
    ///     The image an icon shows, or null for glyph icons.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <returns>An image URL, an API path for uploads without a CDN, or null.</returns>
    public string? ImageUrl(AchievementIconRef icon)
    {
        return icon.Kind switch
        {
            AchievementIconKind.Url => icon.Value,
            AchievementIconKind.Emoji => AchievementIcons.EmojiUrl(icon),
            AchievementIconKind.Upload => AchievementIcons.UploadId(icon) is { } id ? UploadUrls.GetValueOrDefault(id) : null,
            _ => null
        };
    }

    /// <summary>
    ///     The category with a key.
    /// </summary>
    /// <param name="key">The category key.</param>
    /// <returns>The category, or the server category when the key is unknown.</returns>
    public AchievementCategoryInfo Category(string key)
    {
        return Categories.FirstOrDefault(c => c.Key == key)
               ?? AchievementCatalog.GetBuiltInCategory(key)
               ?? AchievementCatalog.GetBuiltInCategory(AchievementCatalog.CustomCategory)!;
    }

    /// <summary>
    ///     The icon an achievement shows: its own, or its category's when it has none.
    /// </summary>
    /// <param name="definition">The achievement.</param>
    /// <returns>The parsed icon.</returns>
    public AchievementIconRef IconFor(AchievementDefinition definition)
    {
        var own = AchievementIcons.Parse(definition.Icon);
        if (own.Kind != AchievementIconKind.None)
            return own;
        var category = AchievementIcons.Parse(Category(definition.CategoryKey).Icon);
        return category.Kind != AchievementIconKind.None
            ? category
            : new AchievementIconRef(AchievementIconKind.Glyph, AchievementIcons.FolderGlyph);
    }
}

/// <summary>
///     One achievement and where a member stands on it.
/// </summary>
public sealed class AchievementProgress
{
    /// <summary>
    ///     The achievement.
    /// </summary>
    public required AchievementDefinition Definition { get; init; }

    /// <summary>
    ///     When it was unlocked, or null.
    /// </summary>
    public DateTime? UnlockedAt { get; init; }

    /// <summary>
    ///     The member's current value for metric achievements, or null when unknown.
    /// </summary>
    public long? Current { get; init; }

    /// <summary>
    ///     Whether it is unlocked.
    /// </summary>
    public bool Unlocked => UnlockedAt.HasValue;

    /// <summary>
    ///     Share done, 0 to 1.
    /// </summary>
    public double Fraction
    {
        get
        {
            if (Unlocked)
                return 1;
            if (Current is null || Definition.Threshold <= 0)
                return 0;
            return Math.Clamp((double)Current.Value / Definition.Threshold, 0, 1);
        }
    }
}

/// <summary>
///     A badge a member owns or could own.
/// </summary>
public sealed class AchievementBadge
{
    /// <summary>
    ///     Stable key: "{category}:{grade}", "tier:{grade}", or "custom:{id}".
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    ///     Display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Stored icon value.
    /// </summary>
    public required string Icon { get; init; }

    /// <summary>
    ///     Grade, which sets its color.
    /// </summary>
    public AchievementGrade Grade { get; init; }

    /// <summary>
    ///     How it is earned.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    ///     Short label drawn on the profile card.
    /// </summary>
    public required string Short { get; init; }
}

/// <summary>
///     A member's achievement totals in one server.
/// </summary>
public sealed class AchievementMemberSummary
{
    /// <summary>
    ///     The member.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    ///     Points earned.
    /// </summary>
    public int Points { get; init; }

    /// <summary>
    ///     Achievements unlocked, including disabled ones that stay unlocked.
    /// </summary>
    public int Unlocked { get; init; }

    /// <summary>
    ///     Achievements members can earn right now.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    ///     Rank on the points leaderboard, or 0 when unranked.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    ///     The rank reached.
    /// </summary>
    public required AchievementTier Tier { get; init; }

    /// <summary>
    ///     The next rank, or null at the top.
    /// </summary>
    public AchievementTier? NextTier { get; init; }

    /// <summary>
    ///     Most recent unlock.
    /// </summary>
    public DateTime? LastUnlockAt { get; init; }

    /// <summary>
    ///     Equipped badge keys by slot, null for empty.
    /// </summary>
    public required string?[] Equipped { get; init; }
}

/// <summary>
///     One row on an achievement leaderboard.
/// </summary>
public sealed class AchievementLeaderboardEntry
{
    /// <summary>
    ///     1 based rank.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    ///     The member.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    ///     Points earned.
    /// </summary>
    public int Points { get; init; }

    /// <summary>
    ///     Achievements unlocked.
    /// </summary>
    public int Unlocked { get; init; }

    /// <summary>
    ///     Most recent unlock.
    /// </summary>
    public DateTime? LastUnlockAt { get; init; }
}

/// <summary>
///     Why a write was refused.
/// </summary>
public enum AchievementError
{
    /// <summary>
    ///     It worked.
    /// </summary>
    None,

    /// <summary>
    ///     The achievement or category does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The name is empty or too long.
    /// </summary>
    NameInvalid,

    /// <summary>
    ///     A text field is too long.
    /// </summary>
    TextTooLong,

    /// <summary>
    ///     The metric cannot be used for server made achievements.
    /// </summary>
    MetricInvalid,

    /// <summary>
    ///     The goal is not a positive number.
    /// </summary>
    ThresholdInvalid,

    /// <summary>
    ///     A keyword or reaction achievement has nothing to watch for.
    /// </summary>
    KeywordMissing,

    /// <summary>
    ///     The server has as many achievements as it can.
    /// </summary>
    TooManyAchievements,

    /// <summary>
    ///     The server has as many categories as it can.
    /// </summary>
    TooManyCategories,

    /// <summary>
    ///     The reward role cannot be given out by the bot.
    /// </summary>
    RoleNotAssignable,

    /// <summary>
    ///     The icon is not a known glyph, a custom emoji, an https image, or one of the server's uploads.
    /// </summary>
    IconInvalid,

    /// <summary>
    ///     The server already has the most icon uploads allowed.
    /// </summary>
    TooManyUploads,

    /// <summary>
    ///     The server already has the most card designs allowed.
    /// </summary>
    TooManyCards,

    /// <summary>
    ///     The server already has the most card designer images allowed.
    /// </summary>
    TooManyCardImages,

    /// <summary>
    ///     The category key is unknown.
    /// </summary>
    CategoryInvalid,

    /// <summary>
    ///     Points or rewards are out of range.
    /// </summary>
    RewardInvalid,

    /// <summary>
    ///     The achievement is already unlocked, or not unlocked when revoking.
    /// </summary>
    AlreadyInState,

    /// <summary>
    ///     The badge is not owned or the slot is out of range.
    /// </summary>
    BadgeInvalid
}

/// <summary>
///     The outcome of a write.
/// </summary>
/// <typeparam name="T">What the write returns.</typeparam>
/// <param name="Error">Why it was refused, or None.</param>
/// <param name="Value">The result when it worked.</param>
public sealed record AchievementResult<T>(AchievementError Error, T? Value)
{
    /// <summary>
    ///     Whether it worked.
    /// </summary>
    public bool Success => Error == AchievementError.None;

    /// <summary>
    ///     A result that worked.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    public static AchievementResult<T> Ok(T value)
    {
        return new AchievementResult<T>(AchievementError.None, value);
    }

    /// <summary>
    ///     A refused result.
    /// </summary>
    /// <param name="error">Why.</param>
    /// <returns>The result.</returns>
    public static AchievementResult<T> Fail(AchievementError error)
    {
        return new AchievementResult<T>(error, default);
    }
}

/// <summary>
///     A server made achievement as an editor submits it.
/// </summary>
public sealed class CustomAchievementDraft
{
    /// <summary>
    ///     Category key.
    /// </summary>
    public string CategoryKey { get; set; } = AchievementCatalog.CustomCategory;

    /// <summary>
    ///     Display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     What it takes, or null to describe the goal automatically.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Icon as given: a glyph name, a custom emoji, an https image, or upload:id. Null uses the category's.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    ///     Grade.
    /// </summary>
    public AchievementGrade Grade { get; set; }

    /// <summary>
    ///     Points, or null for the grade's default.
    /// </summary>
    public int? Points { get; set; }

    /// <summary>
    ///     Whether it stays hidden until unlocked.
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    ///     Whether members can earn it.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     What unlocks it.
    /// </summary>
    public AchievementTrigger Trigger { get; set; }

    /// <summary>
    ///     Metric for metric achievements.
    /// </summary>
    public AchievementMetric Metric { get; set; }

    /// <summary>
    ///     Goal for metric achievements.
    /// </summary>
    public long Threshold { get; set; }

    /// <summary>
    ///     Phrase or emoji for keyword and reaction achievements.
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    ///     Channel a keyword or reaction achievement is limited to.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     Role handed out on unlock.
    /// </summary>
    public ulong? RoleRewardId { get; set; }

    /// <summary>
    ///     Currency handed out on unlock.
    /// </summary>
    public long CurrencyReward { get; set; }

    /// <summary>
    ///     XP handed out on unlock.
    /// </summary>
    public int XpReward { get; set; }
}

/// <summary>
///     A server's changes to a built in achievement as an editor submits them. Null fields keep the default.
/// </summary>
public sealed class AchievementOverrideDraft
{
    /// <summary>
    ///     Whether members can earn it.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Icon as given, or null for the category's.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    ///     Points.
    /// </summary>
    public int? Points { get; set; }

    /// <summary>
    ///     Whether it stays hidden until unlocked.
    /// </summary>
    public bool? Hidden { get; set; }

    /// <summary>
    ///     Role handed out on unlock.
    /// </summary>
    public ulong? RoleRewardId { get; set; }

    /// <summary>
    ///     Currency handed out on unlock.
    /// </summary>
    public long CurrencyReward { get; set; }

    /// <summary>
    ///     XP handed out on unlock.
    /// </summary>
    public int XpReward { get; set; }
}
