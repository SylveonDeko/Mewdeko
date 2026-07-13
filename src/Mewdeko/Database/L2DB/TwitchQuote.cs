#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a memorable Twitch chat quote saved for a guild.
/// </summary>
[Table("TwitchQuotes")]
public class TwitchQuote
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID that owns this quote.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the quote text.
    /// </summary>
    [Column("Text")]
    public string Text { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Twitch user credited for the quote.
    /// </summary>
    [Column("Author")]
    public string? Author { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch user or Discord user who saved the quote.
    /// </summary>
    [Column("AddedBy")]
    public string? AddedBy { get; set; }

    /// <summary>
    ///     Gets or sets when the quote was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}