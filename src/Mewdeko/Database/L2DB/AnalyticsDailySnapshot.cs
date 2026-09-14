using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Nightly snapshot of fleet size and feature adoption.
/// </summary>
[Table("AnalyticsDailySnapshot")]
public class AnalyticsDailySnapshot
{
    /// <summary>
    ///     The day, UTC.
    /// </summary>
    [Column("Day", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public DateTime Day { get; set; }

    /// <summary>
    ///     The bot instance.
    /// </summary>
    [Column("Bot", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public string Bot { get; set; } = string.Empty;

    /// <summary>
    ///     Guild count.
    /// </summary>
    [Column("Guilds")]
    public int Guilds { get; set; }

    /// <summary>
    ///     Summed member count.
    /// </summary>
    [Column("Users")]
    public long Users { get; set; }

    /// <summary>
    ///     JSON object of feature key to number of guilds with it configured.
    /// </summary>
    [Column("FeaturesJson")]
    public string? FeaturesJson { get; set; }
}