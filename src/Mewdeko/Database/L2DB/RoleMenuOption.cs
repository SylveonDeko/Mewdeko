using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RoleMenuOptions")]
public class RoleMenuOption
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("RoleMenuId")]
    public int RoleMenuId { get; set; }

    [Column("RoleId")]
    public ulong RoleId { get; set; }

    [Column("Label")]
    public string Label { get; set; } = null!;

    [Column("Emoji")]
    public string? Emoji { get; set; }

    [Column("Description")]
    public string? Description { get; set; }

    [Column("ButtonStyle")]
    public int ButtonStyle { get; set; } = 2;

    [Column("Position")]
    public int Position { get; set; }

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
