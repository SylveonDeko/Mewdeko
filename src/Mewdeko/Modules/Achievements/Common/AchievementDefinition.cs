namespace Mewdeko.Modules.Achievements.Common;

/// <summary>
///     One achievement as a server sees it: a built in achievement with the server's overrides applied,
///     or one the server made itself.
/// </summary>
public sealed class AchievementDefinition
{
    /// <summary>
    ///     Stable key. Built in keys are snake case words, custom keys are "custom:{id}".
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    ///     Category key: a built in category or "cat:{id}" for a server made one.
    /// </summary>
    public required string CategoryKey { get; init; }

    /// <summary>
    ///     Display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     What it takes to unlock.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///     Icon the server picked for it, as stored, or empty to use its category's.
    /// </summary>
    public string Icon { get; init; } = "";

    /// <summary>
    ///     Grade, which sets the badge color.
    /// </summary>
    public AchievementGrade Grade { get; init; }

    /// <summary>
    ///     Points awarded on unlock.
    /// </summary>
    public int Points { get; init; }

    /// <summary>
    ///     Whether the name and description stay hidden until unlocked.
    /// </summary>
    public bool Hidden { get; init; }

    /// <summary>
    ///     Whether members can unlock it right now.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    ///     Whether the achievement itself is on, ignoring whether its category is.
    /// </summary>
    public bool SelfEnabled { get; init; } = true;

    /// <summary>
    ///     What unlocks it.
    /// </summary>
    public AchievementTrigger Trigger { get; init; }

    /// <summary>
    ///     The metric for metric achievements.
    /// </summary>
    public AchievementMetric Metric { get; init; }

    /// <summary>
    ///     The metric value to reach.
    /// </summary>
    public long Threshold { get; init; }

    /// <summary>
    ///     The moment to watch for, for feat achievements.
    /// </summary>
    public AchievementFeat? Feat { get; init; }

    /// <summary>
    ///     Phrase for keyword achievements, or emoji for reaction achievements.
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    ///     Channel a keyword or reaction achievement is limited to, or null for any channel.
    /// </summary>
    public ulong? ChannelId { get; init; }

    /// <summary>
    ///     Role handed out on unlock.
    /// </summary>
    public ulong? RoleRewardId { get; init; }

    /// <summary>
    ///     Currency handed out on unlock.
    /// </summary>
    public long CurrencyReward { get; init; }

    /// <summary>
    ///     XP handed out on unlock.
    /// </summary>
    public int XpReward { get; init; }

    /// <summary>
    ///     Whether the server made it.
    /// </summary>
    public bool IsCustom { get; init; }

    /// <summary>
    ///     Database ID for custom achievements, otherwise 0.
    /// </summary>
    public int CustomId { get; init; }

    /// <summary>
    ///     Whether it is earned across every server rather than in one.
    /// </summary>
    public bool IsGlobal { get; init; }

    /// <summary>
    ///     Whether the server changed a built in achievement.
    /// </summary>
    public bool IsOverridden { get; init; }

    /// <summary>
    ///     Order inside its category.
    /// </summary>
    public int Position { get; init; }

    /// <summary>
    ///     Whether reaching it depends on the metric.
    /// </summary>
    public bool IsMetric => Trigger == AchievementTrigger.Metric && Metric != AchievementMetric.None;
}
