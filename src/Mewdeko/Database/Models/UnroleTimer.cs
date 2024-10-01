using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents an unrole timer for a user in a guild.
/// </summary>
[Table("UnroleTimer")]
public class UnroleTimer : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild configuration ID.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the role ID.
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Gets or sets the unrole date and time.
    /// </summary>
    public DateTime UnbanAt { get; set; }

    /// <summary>
    ///     Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return UserId.GetHashCode() ^ RoleId.GetHashCode();
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        return obj is UnroleTimer ut && ut.UserId == UserId && ut.RoleId == RoleId;
    }
}