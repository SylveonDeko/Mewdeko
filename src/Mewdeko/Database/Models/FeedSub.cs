using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
/// Represents a feed subscription in the database.
/// </summary>
[Table("FeedSub")]
public class FeedSub : DbEntity
{
    /// <summary>
    /// Gets or sets the ID of the guild configuration this subscription is associated with.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where feed updates will be posted.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the URL of the feed.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the message template for feed updates.
    /// </summary>
    public string Message { get; set; } = "-";

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode() =>
        Url.GetHashCode(StringComparison.InvariantCulture) ^ GuildConfigId.GetHashCode();

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj) =>
        obj is FeedSub s
        && s.Url.ToLower() == Url.ToLower()
        && s.GuildConfigId == GuildConfigId;
}