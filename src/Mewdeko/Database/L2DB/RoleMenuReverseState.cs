using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RoleMenuReverseStates")]
public class RoleMenuReverseState
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    [Column("RoleId")]
    public ulong RoleId { get; set; }

    [Column("MenuId")]
    public int MenuId { get; set; }

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
