#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents one recorded level-up in a guild.
/// </summary>
[Table("XpLevelUpLog")]
public class XpLevelUpLog
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild the level-up happened in.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the member who leveled up.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the level the member had before.
    /// </summary>
    [Column("OldLevel")]
    public int OldLevel { get; set; }

    /// <summary>
    ///     Gets or sets the level the member reached.
    /// </summary>
    [Column("NewLevel")]
    public int NewLevel { get; set; }

    /// <summary>
    ///     Gets or sets when the level-up happened.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
