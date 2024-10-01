using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a channel ID for filtering links in a guild.
/// </summary>
public class FilterLinksChannelId : DbEntity
{
    /// <summary>
    ///     Gets or sets the channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the guild configuration ID.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        return obj is FilterLinksChannelId f
               && f.ChannelId == ChannelId;
    }

    /// <summary>
    ///     Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return ChannelId.GetHashCode();
    }
}