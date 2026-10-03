using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("AchievementCardDesigns")]
public class AchievementCardDesign
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("Name")]
    public string Name { get; set; } = "";

    [Column("Template")]
    public string Template { get; set; } = "";

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }

    [Column("DateUpdated")]
    public DateTime DateUpdated { get; set; }
}
