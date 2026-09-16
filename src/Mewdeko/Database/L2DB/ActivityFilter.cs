using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     An activity name on a guild's activity whitelist or blacklist. Which one it is depends on the guild's
///     activity filter mode.
/// </summary>
[Table("ActivityFilters")]
public class ActivityFilter
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The activity name, matched case insensitively.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = "";

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}