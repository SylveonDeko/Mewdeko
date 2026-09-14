using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Hourly count of a feature firing in a guild.
/// </summary>
[Table("AnalyticsFeatureActivity")]
public class AnalyticsFeatureActivity
{
    /// <summary>
    ///     Start of the hour, UTC.
    /// </summary>
    [Column("HourUtc", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public DateTime HourUtc { get; set; }

    /// <summary>
    ///     The guild.
    /// </summary>
    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The feature key.
    /// </summary>
    [Column("Feature", IsPrimaryKey = true, PrimaryKeyOrder = 2)]
    public string Feature { get; set; } = string.Empty;

    /// <summary>
    ///     The bot instance.
    /// </summary>
    [Column("Bot")]
    public string Bot { get; set; } = string.Empty;

    /// <summary>
    ///     Successful uses in the hour.
    /// </summary>
    [Column("Count")]
    public int Count { get; set; }

    /// <summary>
    ///     Failed uses in the hour.
    /// </summary>
    [Column("Errors")]
    public int Errors { get; set; }
}