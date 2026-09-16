using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     All time seconds a member has spent in one activity in a guild. Segments age out after 90 days, so this is
///     what all time rankings read.
/// </summary>
[Table("ActivityTotals")]
public class ActivityTotal
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    [Column("ApplicationId")]
    public ulong? ApplicationId { get; set; }

    [Column("Name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     The Discord <see cref="Discord.ActivityType" /> value.
    /// </summary>
    [Column("Type")]
    public int Type { get; set; }

    [Column("Seconds")]
    public long Seconds { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}