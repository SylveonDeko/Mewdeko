using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents an unmute timer for a user in a guild.
/// </summary>
[Table("UnmuteTimer")]
public class UnmuteTimer : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild configuration ID.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int? GuildConfigId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the unmute date and time.
    /// </summary>
    public DateTime UnmuteAt { get; set; }

    /// <summary>
    ///     Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return UserId.GetHashCode();
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        return obj is UnmuteTimer ut && ut.UserId == UserId;
    }
}