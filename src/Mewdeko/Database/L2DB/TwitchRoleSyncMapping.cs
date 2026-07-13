#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a Twitch permission level to Discord role sync mapping.
/// </summary>
[Table("TwitchRoleSyncMappings")]
public class TwitchRoleSyncMapping
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID that owns this mapping.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the minimum Twitch permission level required for the role.
    /// </summary>
    [Column("PermissionLevel")]
    public int PermissionLevel { get; set; }

    /// <summary>
    ///     Gets or sets the Discord role ID to apply.
    /// </summary>
    [Column("RoleId")]
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Gets or sets whether this mapping is active.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets when the mapping was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    /// <summary>
    ///     Gets or sets when the mapping was last changed.
    /// </summary>
    [Column("LastUpdatedAt")]
    public DateTime? LastUpdatedAt { get; set; }
}