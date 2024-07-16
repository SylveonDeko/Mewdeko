namespace Mewdeko.Database.Models;

/// <summary>
/// Represents a confession entry in the database.
/// </summary>
public class Confessions : DbEntity
{
    /// <summary>
    /// Gets or sets the ID of the guild where the confession was made.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who made the confession.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the message containing the confession.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where the confession was posted.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the number of this confession.
    /// </summary>
    public ulong ConfessNumber { get; set; }

    /// <summary>
    /// Gets or sets the content of the confession.
    /// </summary>
    public string? Confession { get; set; }
}