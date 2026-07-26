using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     A user or role explicitly allowed to manage the dashboard access list for a guild, independent of the
///     guild's <c>AdminsCanManageAccess</c> setting.
/// </summary>
[Table("DashboardAccessManagers")]
public class DashboardAccessManager
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild this entry applies to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Whether <see cref="TargetId" /> refers to a user or a role. See
    ///     <see cref="Mewdeko.Database.Enums.DashboardAccessTargetType" />.
    /// </summary>
    [Column("TargetType")]
    public int TargetType { get; set; }

    /// <summary>
    ///     The Discord user or role ID this entry grants management rights to.
    /// </summary>
    [Column("TargetId")]
    public ulong TargetId { get; set; }

    /// <summary>
    ///     The ID of the user who granted this entry.
    /// </summary>
    [Column("GrantedBy")]
    public ulong GrantedBy { get; set; }

    /// <summary>
    ///     When this entry was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}