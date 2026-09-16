using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A named invite code. Labels show up in stats and greet placeholders, can grant a role to everyone who joins
///     through the code, and can credit a member other than the invite's Discord creator.
/// </summary>
[Table("InviteLabels")]
public class InviteLabel
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("InviteCode")]
    public string InviteCode { get; set; } = "";

    [Column("Label")]
    public string Label { get; set; } = "";

    /// <summary>
    ///     A role granted to members who join through this code, or null for none.
    /// </summary>
    [Column("RoleId")]
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     The member credited for joins through this code instead of the invite's creator, or null to credit the
    ///     creator. Used for bot-created personal links.
    /// </summary>
    [Column("OwnerUserId")]
    public ulong? OwnerUserId { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}