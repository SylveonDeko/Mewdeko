using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("AchievementMembers")]
public class AchievementMember
{
    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong GuildId { get; set; }

    [Column("UserId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong UserId { get; set; }

    [Column("Points")]
    public int Points { get; set; }

    [Column("UnlockedCount")]
    public int UnlockedCount { get; set; }

    [Column("LastUnlockAt")]
    public DateTime? LastUnlockAt { get; set; }

    [Column("VoiceJoins")]
    public long VoiceJoins { get; set; }

    [Column("MutedSeconds")]
    public long MutedSeconds { get; set; }

    [Column("Badge1")]
    public string? Badge1 { get; set; }

    [Column("Badge2")]
    public string? Badge2 { get; set; }

    [Column("Badge3")]
    public string? Badge3 { get; set; }

    [Column("Badge4")]
    public string? Badge4 { get; set; }

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
