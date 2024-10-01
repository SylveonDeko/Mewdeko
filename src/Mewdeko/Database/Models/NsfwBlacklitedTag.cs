using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a NSFW blacklisted tag in a guild.
/// </summary>
public class NsfwBlacklitedTag : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild configuration ID.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    ///     Gets or sets the tag.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    ///     Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return Tag.GetHashCode(StringComparison.InvariantCulture);
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        return obj is NsfwBlacklitedTag x && x.Tag == Tag;
    }
}