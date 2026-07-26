using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Per-guild settings for the restricted dashboard access feature.
/// </summary>
[Table("DashboardAccessSettings")]
public class DashboardAccessSettings
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild these settings belong to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Whether users with Administrator or ManageGuild permission may manage the dashboard access list
    ///     without being explicitly listed in <c>DashboardAccessManagers</c>.
    /// </summary>
    [Column("AdminsCanManageAccess")]
    public bool AdminsCanManageAccess { get; set; }

    /// <summary>
    ///     When these settings were created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}