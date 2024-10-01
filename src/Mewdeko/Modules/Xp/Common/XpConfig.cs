using Mewdeko.Common.Yml;

namespace Mewdeko.Modules.Xp.Common;

/// <summary>
///     Represents the configuration for the XP system, including rates of XP gain and cooldowns.
/// </summary>
public sealed class XpConfig
{
    /// <summary>
    ///     Gets or sets the configuration version. Do not change manually.
    /// </summary>
    [Comment("DO NOT CHANGE")]
    public int Version { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the amount of XP received per message.
    /// </summary>
    [Comment("How much XP will the users receive per message")]
    public int XpPerMessage { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the cooldown (in minutes) for how often users can receive XP for messages.
    /// </summary>
    [Comment("How often can the users receive XP in minutes")]
    public int MessageXpCooldown { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the average amount of XP earned per minute in voice chat.
    /// </summary>
    [Comment("Average amount of xp earned per minute in VC")]
    public double VoiceXpPerMinute { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the maximum amount of minutes the bot will keep track of a user in a voice channel.
    /// </summary>
    [Comment("The maximum amount of minutes the bot will keep track of a user in a voice channel")]
    public int VoiceMaxMinutes { get; set; } = 720;
}