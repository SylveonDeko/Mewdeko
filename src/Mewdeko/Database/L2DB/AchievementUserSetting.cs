using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("AchievementUserSettings")]
public class AchievementUserSetting
{
    [Column("UserId", IsPrimaryKey = true)]
    public ulong UserId { get; set; }

    [Column("ProfileVisibility")]
    public int ProfileVisibility { get; set; }

    [Column("AchievementsVisibility")]
    public int AchievementsVisibility { get; set; }

    [Column("BadgesVisibility")]
    public int BadgesVisibility { get; set; }

    [Column("HideFromLeaderboards")]
    public bool HideFromLeaderboards { get; set; }

    [Column("DmUnlocks")]
    public int DmUnlocks { get; set; }

    [Column("ShowInLog")]
    public bool ShowInLog { get; set; } = true;

    [Column("MentionMe")]
    public bool MentionMe { get; set; } = true;

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
