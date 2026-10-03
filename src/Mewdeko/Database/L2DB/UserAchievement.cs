using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("UserAchievements")]
public class UserAchievement
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public long Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    [Column("AchievementKey")]
    public string AchievementKey { get; set; } = null!;

    [Column("Points")]
    public int Points { get; set; }

    [Column("GrantedBy")]
    public ulong? GrantedBy { get; set; }

    [Column("UnlockedAt")]
    public DateTime UnlockedAt { get; set; }
}
