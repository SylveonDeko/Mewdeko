using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
/// Represents an AFK (Away From Keyboard) entry in the database.
/// </summary>
[Table("AFK")]
public class Afk : DbEntity
{
    /// <summary>
    /// Gets or sets the user ID associated with this AFK entry.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the guild ID associated with this AFK entry.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the AFK message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this AFK status was timed.
    /// </summary>
    public bool WasTimed { get; set; } = false;

    /// <summary>
    /// Gets or sets the time when the AFK status will expire.
    /// </summary>
    public DateTime? When { get; set; }
}