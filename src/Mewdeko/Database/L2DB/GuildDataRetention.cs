using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A server the bot has left whose stored data is scheduled to be purged after a grace period.
/// </summary>
[Table("GuildDataRetention")]
public class GuildDataRetention
{
    /// <summary>
    ///     The guild the bot left.
    /// </summary>
    [Column("GuildId", IsPrimaryKey = true)]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The guild's name at the time it was queued, kept because the guild is no longer reachable.
    /// </summary>
    [Column("GuildName")]
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    ///     When the bot left, or when the orphaned data was discovered.
    /// </summary>
    [Column("LeftAt")]
    public DateTime LeftAt { get; set; }

    /// <summary>
    ///     The earliest time the purge may run.
    /// </summary>
    [Column("PurgeAfter")]
    public DateTime PurgeAfter { get; set; }

    /// <summary>
    ///     When the purge ran, or null while it is still pending.
    /// </summary>
    [Column("PurgedAt")]
    public DateTime? PurgedAt { get; set; }

    /// <summary>
    ///     How many rows the purge removed across all tables.
    /// </summary>
    [Column("RowsDeleted")]
    public long RowsDeleted { get; set; }

    /// <summary>
    ///     Why the guild was queued: "left" for a live leave event, "scan" for an orphan scan, "manual" for an owner command.
    /// </summary>
    [Column("Source")]
    public string Source { get; set; } = "left";

    /// <summary>
    ///     When the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}