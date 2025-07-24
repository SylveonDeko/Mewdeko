using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for anti-mass mention configuration
/// </summary>
public class AntiMassMentionRequest
{
    /// <summary>
    ///     Whether anti-mass mention protection should be enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The number of mentions allowed in a single message
    /// </summary>
    public int MentionThreshold { get; set; }

    /// <summary>
    ///     The time window in seconds to track mentions
    /// </summary>
    public int TimeWindowSeconds { get; set; }

    /// <summary>
    ///     The maximum allowed mentions in the time window
    /// </summary>
    public int MaxMentionsInTimeWindow { get; set; }

    /// <summary>
    ///     Whether to ignore bot accounts
    /// </summary>
    public bool IgnoreBots { get; set; }

    /// <summary>
    ///     The punishment action to apply
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The mute time in minutes (for mute/timeout action)
    /// </summary>
    public int MuteTime { get; set; }

    /// <summary>
    ///     The role ID to assign (for AddRole action)
    /// </summary>
    public ulong? RoleId { get; set; }
}