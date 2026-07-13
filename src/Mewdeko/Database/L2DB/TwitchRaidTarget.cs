#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a configured Twitch raid target suggestion.
/// </summary>
[Table("TwitchRaidTargets")]
public class TwitchRaidTarget
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID that owns this raid target.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch login to suggest for raids.
    /// </summary>
    [Column("TwitchLogin")]
    public string TwitchLogin { get; set; } = "";

    /// <summary>
    ///     Gets or sets an optional note about the raid target.
    /// </summary>
    [Column("Note")]
    public string? Note { get; set; }

    /// <summary>
    ///     Gets or sets whether this raid target can be suggested.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets when the raid target was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    /// <summary>
    ///     Gets or sets when the raid target was last changed.
    /// </summary>
    [Column("LastUpdatedAt")]
    public DateTime? LastUpdatedAt { get; set; }
}