namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a highlight word for a user in a guild.
/// </summary>
public class Highlights : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the word to highlight.
    /// </summary>
    public string? Word { get; set; }
}