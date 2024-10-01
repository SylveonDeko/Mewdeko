namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a warning given with the second warning system in a guild.
/// </summary>
public class Warning2 : DbEntity
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
    ///     Gets or sets the reason for the warning.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the warning is forgiven.
    /// </summary>
    public bool Forgiven { get; set; } = false;

    /// <summary>
    ///     Gets or sets the username of the person who forgave the warning.
    /// </summary>
    public string? ForgivenBy { get; set; }

    /// <summary>
    ///     Gets or sets the username of the moderator who issued the warning.
    /// </summary>
    public string? Moderator { get; set; }
}