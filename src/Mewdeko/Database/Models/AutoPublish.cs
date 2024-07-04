namespace Mewdeko.Database.Models;

/// <summary>
/// Represents a configuration for auto-publishing messages in a specific channel.
/// </summary>
public class AutoPublish : DbEntity
{
    /// <summary>
    /// Gets or sets the ID of the guild where this auto-publish configuration is active.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where messages should be auto-published.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the ID of users whose messages should not be auto-published.
    /// </summary>
    public ulong BlacklistedUsers { get; set; }
}