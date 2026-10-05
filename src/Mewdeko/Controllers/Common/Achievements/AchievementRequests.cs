using System.Text.Json.Serialization;

namespace Mewdeko.Controllers.Common.Achievements;

/// <summary>
///     Changes to a server's achievement settings. Null fields stay as they are. IDs may arrive as numbers or strings.
/// </summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public class AchievementSettingsRequest
{
    /// <summary>
    ///     Whether members can earn achievements.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    ///     0 auto, 1 where it happened, 2 log channel, 3 DMs only, 4 silent.
    /// </summary>
    public int? AnnounceMode { get; set; }

    /// <summary>
    ///     Log channel, 0 to clear.
    /// </summary>
    public ulong? LogChannelId { get; set; }

    /// <summary>
    ///     Whether members get DMs unless they turn them off.
    /// </summary>
    public bool? DmByDefault { get; set; }

    /// <summary>
    ///     Whether unlock messages mention members.
    /// </summary>
    public bool? MentionUsers { get; set; }

    /// <summary>
    ///     Unlock message source, "" to clear.
    /// </summary>
    public string? UnlockMessage { get; set; }

    /// <summary>
    ///     XP per point earned.
    /// </summary>
    public int? XpPerPoint { get; set; }

    /// <summary>
    ///     Whether hidden achievements show their names before unlocking.
    /// </summary>
    public bool? RevealHidden { get; set; }

    /// <summary>
    ///     Whether unlock messages carry a generated image of the achievement.
    /// </summary>
    public bool? UnlockImage { get; set; }

    /// <summary>
    ///     Seconds after which unlock messages in channels are deleted, or 0 to keep them.
    /// </summary>
    public int? DeleteAfter { get; set; }

    /// <summary>
    ///     Roles that earn nothing.
    /// </summary>
    public List<ulong>? ExcludedRoleIds { get; set; }

    /// <summary>
    ///     Channels that earn nothing.
    /// </summary>
    public List<ulong>? ExcludedChannelIds { get; set; }

    /// <summary>
    ///     Channels where achievements are earned but unlocks are never announced.
    /// </summary>
    public List<ulong>? QuietChannelIds { get; set; }

    /// <summary>
    ///     Whether unlocks are kept out of channels the member cannot send messages in.
    /// </summary>
    public bool? RequireSendPermission { get; set; }
}

/// <summary>
///     A server's changes to a built in achievement. Null text fields keep the default.
/// </summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public class AchievementOverrideRequest
{
    /// <summary>
    ///     Whether members can earn it.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Name, or null for the default.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Description, or null for the default.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Icon, or null for the category's.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    ///     Points, or null for the default.
    /// </summary>
    public int? Points { get; set; }

    /// <summary>
    ///     Hidden until unlocked, or null for the default.
    /// </summary>
    public bool? Hidden { get; set; }

    /// <summary>
    ///     Reward role, or null for none.
    /// </summary>
    public ulong? RoleRewardId { get; set; }

    /// <summary>
    ///     Reward currency.
    /// </summary>
    public long CurrencyReward { get; set; }

    /// <summary>
    ///     Reward XP.
    /// </summary>
    public int XpReward { get; set; }
}

/// <summary>
///     A server made achievement.
/// </summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public class CustomAchievementRequest
{
    /// <summary>
    ///     Category key.
    /// </summary>
    public string CategoryKey { get; set; } = "custom";

    /// <summary>
    ///     Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Description, or null to describe the goal.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Icon: <c>fa:name</c>, a custom emoji, an https image, or <c>upload:id</c>. Null uses the category's.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    ///     0 Bronze to 5 Champion.
    /// </summary>
    public int Grade { get; set; }

    /// <summary>
    ///     Points, or null for the grade's default.
    /// </summary>
    public int? Points { get; set; }

    /// <summary>
    ///     Hidden until unlocked.
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    ///     Whether members can earn it.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     0 metric, 1 phrase, 2 reaction, 3 manual.
    /// </summary>
    public int Trigger { get; set; }

    /// <summary>
    ///     Metric value for metric achievements.
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
    ///     Channel limit for phrase and reaction achievements, or null.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     Reward role, or null.
    /// </summary>
    public ulong? RoleRewardId { get; set; }

    /// <summary>
    ///     Reward currency.
    /// </summary>
    public long CurrencyReward { get; set; }

    /// <summary>
    ///     Reward XP.
    /// </summary>
    public int XpReward { get; set; }
}

/// <summary>
///     Turns many achievements on or off.
/// </summary>
public class AchievementBulkEnableRequest
{
    /// <summary>
    ///     Achievement keys.
    /// </summary>
    public List<string> Keys { get; set; } = [];

    /// <summary>
    ///     The new state.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
///     A new order for a server's own achievements.
/// </summary>
public class AchievementOrderRequest
{
    /// <summary>
    ///     Custom achievement IDs in order.
    /// </summary>
    public List<int> Ids { get; set; } = [];
}

/// <summary>
///     A server made category.
/// </summary>
public class AchievementCategoryRequest
{
    /// <summary>
    ///     Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Icon, or null for the folder.
    /// </summary>
    public string? Icon { get; set; }
}

/// <summary>
///     An achievement as an editor has it, drawn before saving.
/// </summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public class AchievementImagePreviewRequest
{
    /// <summary>
    ///     The stored achievement it edits, or null for a new one.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    ///     Category key.
    /// </summary>
    public string? CategoryKey { get; set; }

    /// <summary>
    ///     Name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Icon, or null for the category's.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    ///     Grade, 0 to 5.
    /// </summary>
    public int Grade { get; set; }

    /// <summary>
    ///     Points, or null for the grade's default.
    /// </summary>
    public int? Points { get; set; }
}

/// <summary>
///     An image to use as an icon.
/// </summary>
public class AchievementIconUploadRequest
{
    /// <summary>
    ///     The image as raw base64 or a <c>data:image/...;base64,</c> URI. PNG, JPEG, GIF, or WebP.
    /// </summary>
    public string Data { get; set; } = "";
}

/// <summary>
///     Category order and which categories are off.
/// </summary>
public class AchievementCategoryLayoutRequest
{
    /// <summary>
    ///     Category keys in order.
    /// </summary>
    public List<string> Order { get; set; } = [];

    /// <summary>
    ///     Category keys turned off.
    /// </summary>
    public List<string> Disabled { get; set; } = [];
}

/// <summary>
///     An achievement to hand to a member.
/// </summary>
public class AchievementGrantRequest
{
    /// <summary>
    ///     Achievement key.
    /// </summary>
    public string Key { get; set; } = "";
}

/// <summary>
///     A member's own achievement preferences. Null fields stay as they are.
/// </summary>
public class AchievementUserSettingsRequest
{
    /// <summary>
    ///     0 everyone, 1 only me.
    /// </summary>
    public int? ProfileVisibility { get; set; }

    /// <summary>
    ///     0 everyone, 1 only me.
    /// </summary>
    public int? AchievementsVisibility { get; set; }

    /// <summary>
    ///     0 everyone, 1 only me.
    /// </summary>
    public int? BadgesVisibility { get; set; }

    /// <summary>
    ///     Whether to stay off leaderboards.
    /// </summary>
    public bool? HideFromLeaderboards { get; set; }

    /// <summary>
    ///     0 server default, 1 always, 2 never.
    /// </summary>
    public int? DmUnlocks { get; set; }

    /// <summary>
    ///     Whether unlocks are announced in the server.
    /// </summary>
    public bool? ShowInLog { get; set; }

    /// <summary>
    ///     Whether to be mentioned in unlock messages.
    /// </summary>
    public bool? MentionMe { get; set; }
}

/// <summary>
///     Badges for the four profile slots.
/// </summary>
public class AchievementBadgeSlotsRequest
{
    /// <summary>
    ///     Badge keys for slots 1 to 4, null for empty.
    /// </summary>
    public List<string?> Slots { get; set; } = [];
}

/// <summary>
///     A new card design, or changes to one. Null fields stay as they are.
/// </summary>
public class AchievementCardDesignRequest
{
    /// <summary>
    ///     Its name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The design.
    /// </summary>
    public global::Mewdeko.Modules.Achievements.Common.AchievementCardTemplate? Template { get; set; }

    /// <summary>
    ///     When creating, also make it the server default, as when the built in default is edited.
    /// </summary>
    public bool MakeDefault { get; set; }
}

/// <summary>
///     The design every achievement uses by default.
/// </summary>
public class AchievementCardDefaultRequest
{
    /// <summary>
    ///     The design ID, or null for the built in design.
    /// </summary>
    public int? Id { get; set; }
}

/// <summary>
///     The design a category or achievement uses instead of the default.
/// </summary>
public class AchievementCardAssignRequest
{
    /// <summary>
    ///     True when <see cref="Key" /> is a category key, false for an achievement key.
    /// </summary>
    public bool Category { get; set; }

    /// <summary>
    ///     The category or achievement key.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    ///     The design ID, or null to follow the default again.
    /// </summary>
    public int? Id { get; set; }
}

/// <summary>
///     A card design to draw before it is saved.
/// </summary>
public class AchievementCardPreviewRequest
{
    /// <summary>
    ///     The design as edited, or null to draw <see cref="DesignId" />.
    /// </summary>
    public global::Mewdeko.Modules.Achievements.Common.AchievementCardTemplate? Template { get; set; }

    /// <summary>
    ///     A saved design to draw when <see cref="Template" /> is null, or null for the design the achievement uses.
    /// </summary>
    public int? DesignId { get; set; }

    /// <summary>
    ///     Whether to draw the locked state, with sample progress.
    /// </summary>
    public bool Locked { get; set; }

    /// <summary>
    ///     The achievement to show, or null for a sample.
    /// </summary>
    public string? Key { get; set; }
}
