using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for anti-mass-post configuration
/// </summary>
public class AntiMassPostConfigRequest
{
    /// <summary>
    ///     Whether anti-mass-post protection should be enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The number of different channels that triggers the protection
    /// </summary>
    public int ChannelThreshold { get; set; }

    /// <summary>
    ///     The time window in seconds for tracking posts
    /// </summary>
    public int TimeWindowSeconds { get; set; }

    /// <summary>
    ///     Content similarity threshold (0.0-1.0)
    /// </summary>
    public double ContentSimilarityThreshold { get; set; }

    /// <summary>
    ///     Minimum content length to track
    /// </summary>
    public int MinContentLength { get; set; }

    /// <summary>
    ///     Only track messages containing links
    /// </summary>
    public bool CheckLinksOnly { get; set; }

    /// <summary>
    ///     Check for duplicate content
    /// </summary>
    public bool CheckDuplicateContent { get; set; }

    /// <summary>
    ///     Require content to be identical vs similar
    /// </summary>
    public bool RequireIdenticalContent { get; set; }

    /// <summary>
    ///     Whether content comparison is case sensitive
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    ///     Whether to delete detected messages
    /// </summary>
    public bool DeleteMessages { get; set; }

    /// <summary>
    ///     Whether to notify user via DM
    /// </summary>
    public bool NotifyUser { get; set; }

    /// <summary>
    ///     The punishment action to be applied
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration of the punishment in minutes
    /// </summary>
    public int PunishDuration { get; set; }

    /// <summary>
    ///     The ID of the role to be added as punishment, if applicable
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     Whether to ignore bot messages
    /// </summary>
    public bool IgnoreBots { get; set; }

    /// <summary>
    ///     Maximum number of messages to track per user
    /// </summary>
    public int MaxMessagesTracked { get; set; }
}