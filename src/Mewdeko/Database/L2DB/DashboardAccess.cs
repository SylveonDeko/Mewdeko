using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     A grant of restricted dashboard access to a user or role for a guild. The actual sections and access
///     levels the grant covers live in <see cref="DashboardAccessSection" /> rows referencing this entry.
/// </summary>
[Table("DashboardAccess")]
public class DashboardAccess
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild this grant applies to.
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
    ///     The Discord user or role ID this grant applies to.
    /// </summary>
    [Column("TargetId")]
    public ulong TargetId { get; set; }

    /// <summary>
    ///     The ID of the user who created this grant.
    /// </summary>
    [Column("GrantedBy")]
    public ulong GrantedBy { get; set; }

    /// <summary>
    ///     When this grant was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}