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
    MemberGoal = 11
}