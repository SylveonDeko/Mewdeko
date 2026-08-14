using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Tracks when a user last used a rate-limited currency action in a guild. Replaces the previous
///     approach of scanning transaction history for a localized description string, which reset every
///     time a guild changed its language and grew unboundedly with the ledger.
/// </summary>
[Table("CurrencyCooldowns")]
public class CurrencyCooldown
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild the cooldown applies to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The user the cooldown applies to.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Stable identifier for the action being limited, such as "daily" or "work". Never localized.
    /// </summary>
    [Column("CooldownKey")]
    public string CooldownKey { get; set; } = null!;

    /// <summary>
    ///     When the action was last performed, in UTC.
    /// </summary>
    [Column("LastUsed")]
    public DateTime LastUsed { get; set; }

    /// <summary>
    ///     Consecutive-use counter, used by the daily reward streak bonus. Resets when a claim window
    ///     is missed entirely.
    /// </summary>
    [Column("StreakCount")]
    public int StreakCount { get; set; }

    /// <summary>
    ///     When this cooldown row was first created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}