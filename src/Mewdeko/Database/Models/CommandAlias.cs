using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a command alias in the database.
/// </summary>
[Table("CommandAlias")]
public class CommandAlias : DbEntity
{
    /// <summary>
    ///     Gets or sets the trigger for this command alias.
    /// </summary>
    public string? Trigger { get; set; }

    /// <summary>
    ///     Gets or sets the mapping (target command) for this alias.
    /// </summary>
    public string? Mapping { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the guild configuration this alias is associated with.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int? GuildConfigId { get; set; }
}