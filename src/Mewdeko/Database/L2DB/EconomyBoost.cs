using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     How often an economy boost window repeats.
/// </summary>
public enum EconomyBoostSchedule
{
    /// <summary>
    ///     Runs once, from the start time for the duration.
    /// </summary>
    OneOff = 0,

    /// <summary>
    ///     Repeats every day at the start time's time of day.
    /// </summary>
    Daily = 1,

    /// <summary>
    ///     Repeats every week on the start time's day and time.
    /// </summary>
    Weekly = 2,

    /// <summary>
    ///     Repeats every month on the start time's day of the month and time.
    /// </summary>
    Monthly = 3
}

/// <summary>
///     A window of time in which a guild's earnings from chosen economy commands are raised or lowered by a percent.
/// </summary>
[Table("EconomyBoosts")]
public class EconomyBoost
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild this boost belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Display name, unique per guild case-insensitively.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = null!;

    /// <summary>
    ///     Percent added to earnings. Negative values lower them.
    /// </summary>
    [Column("PercentChange")]
    public int PercentChange { get; set; }

    /// <summary>
    ///     Comma separated command keys the boost applies to, or empty for every earning command.
    /// </summary>
    [Column("Commands")]
    public string Commands { get; set; } = string.Empty;

    /// <summary>
    ///     How the window repeats. See <see cref="EconomyBoostSchedule" />.
    /// </summary>
    [Column("ScheduleType")]
    public int ScheduleType { get; set; }

    /// <summary>
    ///     When the first window opens, in UTC. Repeating boosts reuse its time of day, weekday or day of the month.
    /// </summary>
    [Column("StartsAt")]
    public DateTime StartsAt { get; set; }

    /// <summary>
    ///     How long each window lasts, in minutes.
    /// </summary>
    [Column("DurationMinutes")]
    public int DurationMinutes { get; set; }

    /// <summary>
    ///     Whether the boost is switched on.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    ///     When the boost was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}
