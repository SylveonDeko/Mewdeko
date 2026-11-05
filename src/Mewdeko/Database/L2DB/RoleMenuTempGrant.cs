using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RoleMenuTempGrants")]
public class RoleMenuTempGrant
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

    [Column("ExpiresAt")]
    public DateTime ExpiresAt { get; set; }
}
