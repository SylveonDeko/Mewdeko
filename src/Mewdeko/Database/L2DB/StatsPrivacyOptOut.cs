using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A user who asked not to be tracked. Their existing message and voice stats are scrubbed and no new ones are
///     recorded in any guild.
/// </summary>
[Table("StatsPrivacyOptOuts")]
public class StatsPrivacyOptOut
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}