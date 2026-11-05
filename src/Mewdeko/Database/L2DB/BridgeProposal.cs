#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a proposal to bridge a channel in a pair, either one this side made or one it has to decide on.
/// </summary>
[Table("BridgeProposals")]
public class BridgeProposal
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the id both sides of the proposal share.
    /// </summary>
    [Column("ProposalId")]
    public Guid ProposalId { get; set; }

    /// <summary>
    ///     Gets or sets the pair the proposal belongs to.
    /// </summary>
    [Column("PairId")]
    public Guid PairId { get; set; }

    /// <summary>
    ///     Gets or sets whether the other side proposed it and this side has to decide.
    /// </summary>
    [Column("Incoming")]
    public bool Incoming { get; set; }

    /// <summary>
    ///     Gets or sets the channel on this side.
    /// </summary>
    [Column("LocalChannelId")]
    public ulong LocalChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the channel on the other side.
    /// </summary>
    [Column("RemoteChannelId")]
    public ulong RemoteChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the channel on the other side.
    /// </summary>
    [Column("RemoteChannelName")]
    public string RemoteChannelName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the user who proposed it.
    /// </summary>
    [Column("ProposedBy")]
    public ulong ProposedBy { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
