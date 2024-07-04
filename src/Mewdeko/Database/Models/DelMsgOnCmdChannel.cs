using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
/// Represents a configuration for deleting messages on command channels.
/// </summary>
public class DelMsgOnCmdChannel : DbEntity
{
    /// <summary>
    /// Gets or sets the ID of the channel.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether message deletion is enabled for this channel.
    /// </summary>
    public bool State { get; set; } = true;

    /// <summary>
    /// Gets or sets the ID of the guild configuration this setting is associated with.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    /// Gets the hash code for this instance.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => ChannelId.GetHashCode();

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj) =>
        obj is DelMsgOnCmdChannel x
        && x.ChannelId == ChannelId;
}