#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a role whose members are exempt from anti-image-hash protection.
/// </summary>
[Table("AntiImageHashIgnoredRoles")]
public class AntiImageHashIgnoredRole
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID this exemption belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the exempt role ID.
    /// </summary>
    [Column("RoleId")]
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Gets or sets when the exemption was added.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}