using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Per-guild economy tuning. Every payout rate, cooldown and limit in the currency module was
///     previously a hardcoded constant, leaving server owners with no way to correct an economy that
///     had inflated or stalled.
/// </summary>
[Table("CurrencyConfigs")]
public class CurrencyConfig
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild these settings apply to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Smallest accepted wager on any game.
    /// </summary>
    [Column("MinBet")]
    public long MinBet { get; set; } = 1;

    /// <summary>
    ///     Largest accepted wager on any game, or 0 for unlimited. A ceiling here is what stops a
    ///     single lucky run from permanently ending a guild's economy.
    /// </summary>
    [Column("MaxBet")]
    public long MaxBet { get; set; }

    /// <summary>
    ///     Whether wagering games are available at all.
    /// </summary>
    [Column("GamblingEnabled")]
    public bool GamblingEnabled { get; set; } = true;

    /// <summary>
    ///     Scales every game payout. Values below 1.0 widen the house edge, above 1.0 narrow it.
    /// </summary>
    [Column("PayoutMultiplier")]
    public double PayoutMultiplier { get; set; } = 1.0;

    /// <summary>
    ///     Minimum delay between any two wagering commands from the same user, in seconds.
    /// </summary>
    [Column("GameCooldownSeconds")]
    public int GameCooldownSeconds { get; set; }

    /// <summary>
    ///     Maximum a user may lose to games in a rolling 24 hours before being cut off, or 0 to
    ///     disable the limit.
    /// </summary>
    [Column("LossLimitPerDay")]
    public long LossLimitPerDay { get; set; }

    /// <summary>
    ///     Whether users may transfer currency to each other.
    /// </summary>
    [Column("PayEnabled")]
    public bool PayEnabled { get; set; } = true;

    /// <summary>
    ///     Percentage of each transfer destroyed as tax. Acts as a sink on player-to-player trade.
    /// </summary>
    [Column("PayTaxPercent")]
    public int PayTaxPercent { get; set; }

    /// <summary>
    ///     Minimum delay between transfers from the same user, in seconds.
    /// </summary>
    [Column("PayCooldownSeconds")]
    public int PayCooldownSeconds { get; set; }

    /// <summary>
    ///     Smallest permitted transfer amount.
    /// </summary>
    [Column("PayMinimum")]
    public long PayMinimum { get; set; } = 1;

    /// <summary>
    ///     Whether the bank is available.
    /// </summary>
    [Column("BankEnabled")]
    public bool BankEnabled { get; set; } = true;

    /// <summary>
    ///     Maximum a user may hold in the bank, or 0 for unlimited.
    /// </summary>
    [Column("BankCapacity")]
    public long BankCapacity { get; set; }

    /// <summary>
    ///     Interest paid on banked currency each interval. This is a faucet, so it is off by default.
    /// </summary>
    [Column("BankInterestPercent")]
    public double BankInterestPercent { get; set; }

    /// <summary>
    ///     Hours between interest accruals.
    /// </summary>
    [Column("BankInterestHours")]
    public int BankInterestHours { get; set; } = 24;

    /// <summary>
    ///     Whether users may attempt to rob each other. Off by default because it is disruptive in
    ///     guilds that did not opt into it.
    /// </summary>
    [Column("RobEnabled")]
    public bool RobEnabled { get; set; }

    /// <summary>
    ///     Percentage chance a robbery attempt succeeds.
    /// </summary>
    [Column("RobSuccessChance")]
    public int RobSuccessChance { get; set; } = 35;

    /// <summary>
    ///     Maximum percentage of the target's wallet a successful robbery takes.
    /// </summary>
    [Column("RobMaxStealPercent")]
    public int RobMaxStealPercent { get; set; } = 20;

    /// <summary>
    ///     Percentage of the robber's own wallet destroyed on a failed attempt.
    /// </summary>
    [Column("RobFinePercent")]
    public int RobFinePercent { get; set; } = 15;

    /// <summary>
    ///     Wallet balance a target must hold before they can be robbed at all, protecting new users.
    /// </summary>
    [Column("RobMinimumWallet")]
    public long RobMinimumWallet { get; set; } = 100;

    /// <summary>
    ///     Minimum delay between robbery attempts from the same user, in seconds.
    /// </summary>
    [Column("RobCooldownSeconds")]
    public int RobCooldownSeconds { get; set; } = 3600;

    /// <summary>
    ///     Whether the work command is available.
    /// </summary>
    [Column("WorkEnabled")]
    public bool WorkEnabled { get; set; } = true;

    /// <summary>
    ///     Lower bound of the work payout range.
    /// </summary>
    [Column("WorkMinReward")]
    public long WorkMinReward { get; set; } = 50;

    /// <summary>
    ///     Upper bound of the work payout range.
    /// </summary>
    [Column("WorkMaxReward")]
    public long WorkMaxReward { get; set; } = 250;

    /// <summary>
    ///     Minimum delay between work commands from the same user, in seconds.
    /// </summary>
    [Column("WorkCooldownSeconds")]
    public int WorkCooldownSeconds { get; set; } = 1800;

    /// <summary>
    ///     Whether the crime command is available.
    /// </summary>
    [Column("CrimeEnabled")]
    public bool CrimeEnabled { get; set; } = true;

    /// <summary>
    ///     Lower bound of the crime payout range on success.
    /// </summary>
    [Column("CrimeMinReward")]
    public long CrimeMinReward { get; set; } = 200;

    /// <summary>
    ///     Upper bound of the crime payout range on success.
    /// </summary>
    [Column("CrimeMaxReward")]
    public long CrimeMaxReward { get; set; } = 800;

    /// <summary>
    ///     Percentage chance a crime attempt succeeds.
    /// </summary>
    [Column("CrimeSuccessChance")]
    public int CrimeSuccessChance { get; set; } = 45;

    /// <summary>
    ///     Lower bound of the fine charged on a failed crime.
    /// </summary>
    [Column("CrimeFineMin")]
    public long CrimeFineMin { get; set; } = 100;

    /// <summary>
    ///     Upper bound of the fine charged on a failed crime.
    /// </summary>
    [Column("CrimeFineMax")]
    public long CrimeFineMax { get; set; } = 500;

    /// <summary>
    ///     Minimum delay between crime commands from the same user, in seconds.
    /// </summary>
    [Column("CrimeCooldownSeconds")]
    public int CrimeCooldownSeconds { get; set; } = 3600;

    /// <summary>
    ///     Whether consecutive daily claims earn an escalating bonus.
    /// </summary>
    [Column("DailyStreakEnabled")]
    public bool DailyStreakEnabled { get; set; } = true;

    /// <summary>
    ///     Extra currency added per consecutive daily claim.
    /// </summary>
    [Column("DailyStreakBonus")]
    public long DailyStreakBonus { get; set; }

    /// <summary>
    ///     Ceiling on the accumulated streak bonus, or 0 for uncapped.
    /// </summary>
    [Column("DailyStreakMaxBonus")]
    public long DailyStreakMaxBonus { get; set; }

    /// <summary>
    ///     When this configuration row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}