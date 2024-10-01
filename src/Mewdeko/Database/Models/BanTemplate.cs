namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a template for ban messages.
/// </summary>
public class BanTemplate : DbEntity
{
    /// <summary>
    ///     Gets or sets the ID of the guild where this ban template is used.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the text of the ban template.
    /// </summary>
    public string? Text { get; set; }
}