using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("AchievementCategories")]
public class AchievementCategory
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("Name")]
    public string Name { get; set; } = null!;

    [Column("Description")]
    public string? Description { get; set; }

    [Column("Icon")]
    public string? Icon { get; set; }

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
