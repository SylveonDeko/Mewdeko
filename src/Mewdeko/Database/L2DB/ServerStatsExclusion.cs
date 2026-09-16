using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A channel, role or member left out of message and voice stats.
/// </summary>
[Table("ServerStatsExclusions")]
public class ServerStatsExclusion
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The channel, role or user snowflake, depending on <see cref="Kind" />.
    /// </summary>
    [Column("TargetId")]
    public ulong TargetId { get; set; }

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.ServerStats.Common.StatsExclusionKind" /> this row represents.
    /// </summary>
    [Column("Kind")]
    public int Kind { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}