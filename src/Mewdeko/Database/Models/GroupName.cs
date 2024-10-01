using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a group name associated with a guild configuration.
/// </summary>
public class GroupName : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild configuration ID.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    ///     Gets or sets the group number.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    ///     Gets or sets the name of the group.
    /// </summary>
    public string? Name { get; set; }
}