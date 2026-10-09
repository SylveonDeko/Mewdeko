using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for anti-external-app configuration
/// </summary>
public class AntiExternalAppConfigRequest
{
    /// <summary>
    ///     Whether anti-external-app protection should be enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The punishment for the member who ran the app
    /// </summary>
    public PunishmentAction Action { get; set; } = PunishmentAction.Timeout;

    /// <summary>
    ///     The duration of the punishment in minutes
    /// </summary>
    public int PunishDuration { get; set; } = 60;

    /// <summary>
    ///     The ID of the role to be added as punishment, if applicable
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     How many users and roles one app message may mention, 0 to turn the check off
    /// </summary>
    public int MentionThreshold { get; set; } = 5;

    /// <summary>
    ///     Whether app messages carrying a Discord invite link count as violations
    /// </summary>
    public bool BlockInvites { get; set; } = true;

    /// <summary>
    ///     How many app messages one member may trigger within the time window, 0 to turn the check off
    /// </summary>
    public int MaxMessages { get; set; } = 5;

    /// <summary>
    ///     The length of the window for the message limit, in seconds
    /// </summary>
    public int TimeWindowSeconds { get; set; } = 10;

    /// <summary>
    ///     Whether to delete the offending app message
    /// </summary>
    public bool DeleteMessages { get; set; } = true;

    /// <summary>
    ///     Whether to tell the member by DM
    /// </summary>
    public bool NotifyUser { get; set; } = true;
}
