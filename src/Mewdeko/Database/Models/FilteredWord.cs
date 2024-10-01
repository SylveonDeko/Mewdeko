using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a word that is filtered in a guild.
/// </summary>
[Table("FilteredWord")]
public class FilteredWord : DbEntity
{
    /// <summary>
    ///     Gets or sets the word to be filtered.
    /// </summary>
    public string? Word { get; set; }

    /// <summary>
    ///     Gets or sets the guild configuration ID.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }
}