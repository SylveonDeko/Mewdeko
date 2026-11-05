namespace Mewdeko.Modules.Moderation.Common;

/// <summary>
///     The punishments a custom DM can be set for.
/// </summary>
public enum PunishDmAction
{
    /// <summary>
    ///     A warning.
    /// </summary>
    Warn,

    /// <summary>
    ///     A mute.
    /// </summary>
    Mute,

    /// <summary>
    ///     A timeout.
    /// </summary>
    Timeout,

    /// <summary>
    ///     A kick.
    /// </summary>
    Kick,

    /// <summary>
    ///     A ban.
    /// </summary>
    Ban,

    /// <summary>
    ///     A softban.
    /// </summary>
    Softban
}
