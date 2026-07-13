#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a named counter available to Twitch chat commands.
/// </summary>
[Table("TwitchCounters")]
public class TwitchCounter
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID that owns this counter.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the counter name.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the current counter value.
    /// </summary>
    [Column("Value")]
    public int Value { get; set; }

    /// <summary>
    ///     Gets or sets when the counter was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    /// <summary>
    ///     Gets or sets when the counter was last changed.
    /// </summary>
    [Column("LastUpdatedAt")]
    public DateTime? LastUpdatedAt { get; set; }
}