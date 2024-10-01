using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a cooldown configuration for a command.
/// </summary>
public class CommandCooldown : DbEntity
{
    /// <summary>
    ///     Gets or sets the cooldown duration in seconds.
    /// </summary>
    public int Seconds { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the guild configuration this cooldown is associated with.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the command this cooldown applies to.
    /// </summary>
    public string? CommandName { get; set; }
}