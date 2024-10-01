using System.ComponentModel.DataAnnotations;
using LinqToDB.Mapping;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a database entity.
/// </summary>
public class DbEntity
{
    /// <summary>
    ///     Gets or sets the unique identifier for the entity.
    /// </summary>
    [Key]
    [Identity]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the date and time when the entity was added.
    ///     Defaults to the current UTC date and time.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "timestamp without time zone")]
    public DateTime? DateAdded { get; set; } = DateTime.Now;
}