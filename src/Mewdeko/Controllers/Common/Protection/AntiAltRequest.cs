using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for anti-alt configuration
/// </summary>
public class AntiAltRequest
{
    /// <summary>
    ///     The minimum account age in minutes
    /// </summary>
    public int MinAgeMinutes { get; set; }

    /// <summary>
    ///     The punishment action to apply
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration of the punishment in minutes (for timeout/mute)
    /// </summary>
    public int ActionDurationMinutes { get; set; }

    /// <summary>
    ///     The role ID to assign (for AddRole action)
    /// </summary>
    public ulong? RoleId { get; set; }
}