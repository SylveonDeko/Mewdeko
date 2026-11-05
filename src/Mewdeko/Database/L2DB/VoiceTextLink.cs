#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a voice channel linked to a text channel that only its members can use.
/// </summary>
[Table("VoiceTextLinks")]
public class VoiceTextLink
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
    ///     Gets or sets the voice channel ID.
    /// </summary>
    [Column("VoiceChannelId")]
    public ulong VoiceChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the text channel ID.
    /// </summary>
    [Column("TextChannelId")]
    public ulong TextChannelId { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
