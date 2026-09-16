using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A user or role excluded from some part of invite tracking: inviters who never earn credit, or members hidden
///     from the leaderboard.
/// </summary>
[Table("InviteTrackingExclusions")]
public class InviteTrackingExclusion
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The user or role snowflake, depending on <see cref="Kind" />.
    /// </summary>
    [Column("TargetId")]
    public ulong TargetId { get; set; }

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.Utility.Common.InviteExclusionKind" /> this row represents.
    /// </summary>
    [Column("Kind")]
    public int Kind { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}