using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RoleMenus")]
public class RoleMenu
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("Name")]
    public string Name { get; set; } = null!;

    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    [Column("MessageId")]
    public ulong? MessageId { get; set; }

    [Column("Message")]
    public string? Message { get; set; }

    [Column("Style")]
    public int Style { get; set; }

    [Column("Placeholder")]
    public string? Placeholder { get; set; }

    [Column("Mode")]
    public int Mode { get; set; }

    [Column("MinRoles")]
    public int MinRoles { get; set; }

    [Column("MaxRoles")]
    public int MaxRoles { get; set; }

    [Column("RequiredRoleId")]
    public ulong? RequiredRoleId { get; set; }

    [Column("ReplyMode")]
    public int ReplyMode { get; set; }

    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    [Column("CreatedBy")]
    public ulong CreatedBy { get; set; }

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }

    [Column("DateModified")]
    public DateTime DateModified { get; set; }

    [Association(ThisKey = nameof(Id), OtherKey = nameof(RoleMenuOption.RoleMenuId))]
    public List<RoleMenuOption> Options { get; set; } = [];
}
