#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a community linked once with a community on the other platform, so either side can see the other's
///     shared channels and bridge them without a code per channel.
/// </summary>
[Table("BridgePairs")]
public class BridgePair
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the id both sides of the pair share.
    /// </summary>
    [Column("PairId")]
    public Guid PairId { get; set; }

    /// <summary>
    ///     Gets or sets the guild on this side of the pair.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the community on the other platform.
    /// </summary>
    [Column("RemoteGuildId")]
    public ulong RemoteGuildId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the community on the other platform.
    /// </summary>
    [Column("RemoteGuildName")]
    public string RemoteGuildName { get; set; } = "";

    /// <summary>
    ///     Gets or sets how incoming bridge proposals are handled: 0 approves each bridge, 1 is automatic.
    /// </summary>
    [Column("ApprovalMode")]
    public int ApprovalMode { get; set; }

    /// <summary>
    ///     Gets or sets the space-separated channels this side hides from the pair.
    /// </summary>
    [Column("HiddenChannelIds")]
    public string HiddenChannelIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets the user who made or accepted the pair code on this side.
    /// </summary>
    [Column("CreatedBy")]
    public ulong CreatedBy { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
