using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("CustomAchievements")]
public class CustomAchievement
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("CategoryKey")]
    public string CategoryKey { get; set; } = "custom";

    [Column("Name")]
    public string Name { get; set; } = null!;

    [Column("Description")]
    public string? Description { get; set; }

    [Column("Icon")]
    public string? Icon { get; set; }

    [Column("Grade")]
    public int Grade { get; set; }

    [Column("Points")]
    public int? Points { get; set; }

    [Column("Hidden")]
    public bool Hidden { get; set; }

    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    [Column("Position")]
    public int Position { get; set; }

    [Column("TriggerType")]
    public int TriggerType { get; set; }

    [Column("Metric")]
    public int Metric { get; set; }

    [Column("Threshold")]
    public long Threshold { get; set; }

    [Column("Keyword")]
    public string? Keyword { get; set; }

    [Column("ChannelId")]
    public ulong? ChannelId { get; set; }

    [Column("RoleRewardId")]
    public ulong? RoleRewardId { get; set; }

    [Column("CurrencyReward")]
    public long CurrencyReward { get; set; }

    [Column("XpReward")]
    public int XpReward { get; set; }

    [Column("CreatedBy")]
    public ulong CreatedBy { get; set; }

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }

    [Column("DateModified")]
    public DateTime DateModified { get; set; }
}
