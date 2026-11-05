#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     What a timed channel action changes.
/// </summary>
public enum ChannelTimedActionKind
{
    /// <summary>
    ///     Denies Send Messages.
    /// </summary>
    Lock = 0,

    /// <summary>
    ///     Denies View Channel.
    /// </summary>
    Hide = 1
}

/// <summary>
///     Represents a channel that is locked or hidden, with what its overwrites were before.
/// </summary>
[Table("ChannelTimedActions")]
public class ChannelTimedAction
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets whether the channel was locked or hidden.
    /// </summary>
    [Column("Kind")]
    public ChannelTimedActionKind Kind { get; set; }

    /// <summary>
    ///     Gets or sets the JSON list of the changed roles and what their overwrites were before.
    /// </summary>
    [Column("Previous")]
    public string Previous { get; set; } = "[]";

    /// <summary>
    ///     Gets or sets when the action is undone, or null if it lasts until it is undone by hand.
    /// </summary>
    [Column("EndsAt")]
    public DateTime? EndsAt { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who started the action.
    /// </summary>
    [Column("CreatedBy")]
    public ulong CreatedBy { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
