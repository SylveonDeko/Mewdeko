using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RoleMenuGroups")]
public class RoleMenuGroup
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("Name")]
    public string Name { get; set; } = null!;

    [Column("MaxRoles")]
    public int MaxRoles { get; set; } = 1;

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
