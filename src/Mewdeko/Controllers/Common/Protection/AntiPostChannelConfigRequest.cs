using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for anti-post-channel configuration
/// </summary>
public class AntiPostChannelConfigRequest
{
    /// <summary>
    ///     Whether anti-post-channel protection should be enabled
    /// </summary>
    public bool Enabled { get; set; }

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
    ///     Whether to delete messages posted in honeypot channels
    /// </summary>
    public bool DeleteMessages { get; set; }

    /// <summary>
    ///     Whether to notify user via DM
    /// </summary>
    public bool NotifyUser { get; set; }

    /// <summary>
    ///     Whether to ignore bot messages
    /// </summary>
    public bool IgnoreBots { get; set; }
}