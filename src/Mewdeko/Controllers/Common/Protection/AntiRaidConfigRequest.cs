using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for anti-raid configuration
/// </summary>
public class AntiRaidConfigRequest
{
    /// <summary>
    ///     Whether anti-raid protection should be enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The number of users joining that triggers the anti-raid protection (2-30)
    /// </summary>
    public int UserThreshold { get; set; }

    /// <summary>
    ///     The time period in seconds in which the user threshold must be reached (2-300)
    /// </summary>
    public int Seconds { get; set; }

    /// <summary>
    ///     The punishment action to be applied when the protection is triggered
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration of the punishment in minutes, if applicable (0-1440)
    /// </summary>
    public int PunishDuration { get; set; }
}