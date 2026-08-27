namespace Mewdeko.Modules.StatChannels.Common;

/// <summary>
///     A partial set of stat channel fields. Null members are left untouched by updates and fall back to the guild
///     defaults when creating.
/// </summary>
public class StatChannelOptions
{
    /// <summary>
    ///     The display template.
    /// </summary>
    public string? Template { get; set; }

    /// <summary>
    ///     The stat type to display.
    /// </summary>
    public StatChannelType? StatType { get; set; }

    /// <summary>
    ///     The style applied to the resolved number.
    /// </summary>
    public StatChannelDisplayStyle? DisplayStyle { get; set; }

    /// <summary>
    ///     Tuning for the chosen display style.
    /// </summary>
    public StatChannelStyleOptions? StyleOptions { get; set; }

    /// <summary>
    ///     How updates are pushed to Discord.
    /// </summary>
    public StatChannelUpdateMechanism? Mechanism { get; set; }

    /// <summary>
    ///     How often the channel refreshes, in minutes.
    /// </summary>
    public int? UpdateIntervalMinutes { get; set; }

    /// <summary>
    ///     The role counted by role member stats.
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     The countdown target date.
    /// </summary>
    public DateTime? CountdownDate { get; set; }

    /// <summary>
    ///     The member goal target.
    /// </summary>
    public int? GoalTarget { get; set; }

    /// <summary>
    ///     A snowflake or row target such as a counting channel or Minecraft server.
    /// </summary>
    public ulong? TargetId { get; set; }

    /// <summary>
    ///     A named target such as a Twitch chat counter.
    /// </summary>
    public string? TargetName { get; set; }
}