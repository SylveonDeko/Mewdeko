#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a guild's own colour for the bot's ok and info embeds.
/// </summary>
[Table("GuildEmbedColors")]
public class GuildEmbedColor
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the colour as a 24-bit RGB value.
    /// </summary>
    [Column("OkColor")]
    public int OkColor { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
