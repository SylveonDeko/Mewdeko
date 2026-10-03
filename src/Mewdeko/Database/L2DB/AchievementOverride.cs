using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("AchievementOverrides")]
public class AchievementOverride
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("AchievementKey")]
    public string AchievementKey { get; set; } = null!;

    [Column("Disabled")]
    public bool Disabled { get; set; }

    [Column("Name")]
    public string? Name { get; set; }

    [Column("Description")]
    public string? Description { get; set; }

    [Column("Icon")]
    public string? Icon { get; set; }

    [Column("Points")]
    public int? Points { get; set; }

    [Column("Hidden")]
    public bool? Hidden { get; set; }

    [Column("RoleRewardId")]
    public ulong? RoleRewardId { get; set; }

    [Column("CurrencyReward")]
    public long CurrencyReward { get; set; }

    [Column("XpReward")]
    public int XpReward { get; set; }

    [Column("DateModified")]
    public DateTime DateModified { get; set; }
}
