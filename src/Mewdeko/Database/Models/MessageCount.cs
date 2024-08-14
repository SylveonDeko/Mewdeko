namespace Mewdeko.Database.Models;

/// <summary>
/// Count of messages per user, channel, etc
/// </summary>
public class MessageCount : DbEntity
{
    /// <summary>
    /// The guild to be able to look up count
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// The channel for the message
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// The UserId for lookups
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// The count for this combination
    /// </summary>
    public ulong Count { get; set; }
}