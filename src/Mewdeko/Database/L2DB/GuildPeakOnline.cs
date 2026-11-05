#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents the highest number of members a guild has had online at once.
/// </summary>
[Table("GuildPeakOnline")]
public class GuildPeakOnline
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
    ///     Gets or sets the highest online member count seen.
    /// </summary>
    [Column("PeakCount")]
    public int PeakCount { get; set; }

    /// <summary>
    ///     Gets or sets when the peak was reached.
    /// </summary>
    [Column("PeakAt")]
    public DateTime PeakAt { get; set; }
}
