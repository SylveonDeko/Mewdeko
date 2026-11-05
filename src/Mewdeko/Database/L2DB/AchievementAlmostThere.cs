using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Marks that a member was told they are close to an achievement, so it is only said once.
/// </summary>
[Table("AchievementAlmostThere")]
public class AchievementAlmostThere
{
    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong GuildId { get; set; }

    [Column("UserId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong UserId { get; set; }

    [Column("AchievementKey", IsPrimaryKey = true, PrimaryKeyOrder = 2)]
    public string AchievementKey { get; set; } = null!;

    [Column("NotifiedAt")]
    public DateTime NotifiedAt { get; set; }
}
