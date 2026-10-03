using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("AchievementEmojiUses")]
public class AchievementEmojiUse
{
    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong GuildId { get; set; }

    [Column("UserId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong UserId { get; set; }

    [Column("Emoji", IsPrimaryKey = true, PrimaryKeyOrder = 2)]
    public string Emoji { get; set; } = null!;

    [Column("Count")]
    public int Count { get; set; }
}
