using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A role members earn when they reach a number of net invites.
/// </summary>
[Table("InviteRoles")]
public class InviteRole
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("RoleId")]
    public ulong RoleId { get; set; }

    /// <summary>
    ///     The net invites a member needs for the role.
    /// </summary>
    [Column("RequiredInvites")]
    public int RequiredInvites { get; set; }

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
