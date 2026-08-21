using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     How many days of messages a ban purges within one scope.
/// </summary>
[Table("BanPruneSettings")]
public class BanPruneSetting
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; } // integer

    [Column("GuildId")]
    public ulong GuildId { get; set; } // numeric(20,0)

    /// <summary>
    ///     0 for the guild default, 1 for a category override, 2 for a channel override.
    /// </summary>
    [Column("ScopeType")]
    public int ScopeType { get; set; } // integer

    /// <summary>
    ///     The category or channel the override applies to, or 0 for the guild default.
    /// </summary>
    [Column("ScopeId")]
    public ulong ScopeId { get; set; } // numeric(20,0)

    /// <summary>
    ///     The moderation action this applies to, or an empty string for every action in the scope.
    /// </summary>
    [Column("ActionKey")]
    public string? ActionKey { get; set; } // text

    [Column("PruneDays")]
    public int PruneDays { get; set; } // integer

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; } // timestamp (6) without time zone
}