using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a user blacklist for publishing in a channel.
/// </summary>
[Table("PublishUserBlacklist")]
public class PublishUserBlacklist : DbEntity
{
    /// <summary>
    ///     Gets or sets the channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong User { get; set; }
}