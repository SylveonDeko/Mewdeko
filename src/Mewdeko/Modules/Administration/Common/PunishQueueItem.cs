namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     Represents an item in the punishment queue.
/// </summary>
public class PunishQueueItem
{
    /// <summary>
    ///     Gets or sets the action to be taken as punishment.
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     Gets or sets the type of protection for which the punishment is intended.
    /// </summary>
    public ProtectionType Type { get; set; }

    /// <summary>
    ///     Gets or sets the duration of mute in minutes (if applicable).
    /// </summary>
    public int MuteTime { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the role associated with the punishment (if applicable).
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     Gets or sets the user to be punished.
    /// </summary>
    public IGuildUser User { get; set; }
}