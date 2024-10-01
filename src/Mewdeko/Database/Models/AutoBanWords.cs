namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a word or phrase that triggers an automatic ban when used.
/// </summary>
public class AutoBanEntry : DbEntity
{
    /// <summary>
    ///     Gets or sets the word or phrase that triggers the auto-ban.
    /// </summary>
    public string? Word { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the guild where this auto-ban entry is configured.
    /// </summary>
    public ulong GuildId { get; set; }
}