#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a channel that is exempt from anti-image-hash protection.
/// </summary>
[Table("AntiImageHashIgnoredChannels")]
public class AntiImageHashIgnoredChannel
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID this exemption belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the exempt channel ID.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets when the exemption was added.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}