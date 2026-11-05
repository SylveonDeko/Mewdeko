#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a guild's auto reaction settings.
/// </summary>
[Table("AutoReactionSettings")]
public class AutoReactionSetting
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the settings belong to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the role whose members can configure auto reactions without Manage Server.
    /// </summary>
    [Column("ManagerRoleId")]
    public ulong? ManagerRoleId { get; set; }
}
