using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     The access level a <see cref="DashboardAccess" /> grant has for a single dashboard section (the section
///     name matches the bot API controller name, e.g. "Starboard", "Moderation").
/// </summary>
[Table("DashboardAccessSection")]
public class DashboardAccessSection
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The <see cref="DashboardAccess" /> grant this section entry belongs to.
    /// </summary>
    [Column("DashboardAccessId")]
    public int DashboardAccessId { get; set; }

    /// <summary>
    ///     The dashboard section name, matching the bot API controller name.
    /// </summary>
    [Column("Section", CanBeNull = false)]
    public string Section { get; set; } = null!;

    /// <summary>
    ///     The access level for this section. See <see cref="Mewdeko.Database.Enums.DashboardAccessLevel" />.
    /// </summary>
    [Column("Level")]
    public int Level { get; set; }
}