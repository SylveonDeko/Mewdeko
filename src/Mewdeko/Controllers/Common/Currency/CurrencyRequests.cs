namespace Mewdeko.Controllers.Common.Currency;

/// <summary>
///     Request to change a guild's economy settings. Every field is optional; only those supplied are
///     applied, so the dashboard can send partial updates without having to round-trip the whole config.
/// </summary>
public class UpdateEconomyConfigRequest
{
    /// <summary>Whether wagering games are available.</summary>
    public bool? GamblingEnabled { get; set; }

    /// <summary>Smallest accepted wager.</summary>
    public long? MinBet { get; set; }

    /// <summary>Largest accepted wager, or 0 for unlimited.</summary>
    public long? MaxBet { get; set; }

    /// <summary>Scales game payouts. Below 1.0 widens the house edge.</summary>
    public double? PayoutMultiplier { get; set; }

    /// <summary>Minimum delay between wagering commands, in seconds.</summary>
    public int? GameCooldownSeconds { get; set; }

    /// <summary>Maximum a user may lose to games in a rolling day, or 0 to disable.</summary>
    public long? LossLimitPerDay { get; set; }

    /// <summary>Whether users may transfer currency to each other.</summary>
    public bool? PayEnabled { get; set; }

    /// <summary>Percentage of each transfer destroyed as tax.</summary>
    public int? PayTaxPercent { get; set; }

    /// <summary>Minimum delay between transfers, in seconds.</summary>
    public int? PayCooldownSeconds { get; set; }

    /// <summary>Smallest permitted transfer.</summary>
    public long? PayMinimum { get; set; }

    /// <summary>Whether the bank is available.</summary>
    public bool? BankEnabled { get; set; }

    /// <summary>Maximum bank holding, or 0 for unlimited.</summary>
    public long? BankCapacity { get; set; }

    /// <summary>Interest paid on banked currency each interval.</summary>
    public double? BankInterestPercent { get; set; }

    /// <summary>Hours between interest accruals.</summary>
    public int? BankInterestHours { get; set; }

    /// <summary>Whether users may rob each other.</summary>
    public bool? RobEnabled { get; set; }

    /// <summary>Percentage chance a robbery succeeds.</summary>
    public int? RobSuccessChance { get; set; }

    /// <summary>Maximum percentage of a target's wallet a robbery takes.</summary>
    public int? RobMaxStealPercent { get; set; }

    /// <summary>Percentage of the robber's wallet destroyed on failure.</summary>
    public int? RobFinePercent { get; set; }

    /// <summary>Wallet balance a target must hold to be robbable.</summary>
    public long? RobMinimumWallet { get; set; }

    /// <summary>Minimum delay between robbery attempts, in seconds.</summary>
    public int? RobCooldownSeconds { get; set; }

    /// <summary>Whether the work command is available.</summary>
    public bool? WorkEnabled { get; set; }

    /// <summary>Lower bound of the work payout range.</summary>
    public long? WorkMinReward { get; set; }

    /// <summary>Upper bound of the work payout range.</summary>
    public long? WorkMaxReward { get; set; }

    /// <summary>Minimum delay between work commands, in seconds.</summary>
    public int? WorkCooldownSeconds { get; set; }

    /// <summary>Whether the crime command is available.</summary>
    public bool? CrimeEnabled { get; set; }

    /// <summary>Lower bound of the crime payout range.</summary>
    public long? CrimeMinReward { get; set; }

    /// <summary>Upper bound of the crime payout range.</summary>
    public long? CrimeMaxReward { get; set; }

    /// <summary>Percentage chance a crime succeeds.</summary>
    public int? CrimeSuccessChance { get; set; }

    /// <summary>Lower bound of the fine for a failed crime.</summary>
    public long? CrimeFineMin { get; set; }

    /// <summary>Upper bound of the fine for a failed crime.</summary>
    public long? CrimeFineMax { get; set; }

    /// <summary>Minimum delay between crime commands, in seconds.</summary>
    public int? CrimeCooldownSeconds { get; set; }

    /// <summary>Whether consecutive daily claims earn a bonus.</summary>
    public bool? DailyStreakEnabled { get; set; }

    /// <summary>Extra currency per consecutive daily claim.</summary>
    public long? DailyStreakBonus { get; set; }

    /// <summary>Ceiling on the streak bonus, or 0 for uncapped.</summary>
    public long? DailyStreakMaxBonus { get; set; }
}

/// <summary>
///     Request to create or replace a shop item.
/// </summary>
public class ShopItemRequest
{
    /// <summary>Display name, unique per guild.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Optional longer description.</summary>
    public string? Description { get; set; }

    /// <summary>Cost in guild currency.</summary>
    public long Price { get; set; }

    /// <summary>What the item delivers. See <see cref="Mewdeko.Database.Enums.ShopItemType" />.</summary>
    public int ItemType { get; set; }

    /// <summary>Role granted for role items.</summary>
    public ulong? RoleId { get; set; }

    /// <summary>Text delivered for text items.</summary>
    public string? TextContent { get; set; }

    /// <summary>Remaining stock, or -1 for unlimited.</summary>
    public int Stock { get; set; } = -1;

    /// <summary>Maximum a single user may own, or 0 for unlimited.</summary>
    public int MaxPerUser { get; set; }

    /// <summary>Role required to purchase, if any.</summary>
    public ulong? RequiredRoleId { get; set; }

    /// <summary>Whether the item can be used up.</summary>
    public bool Consumable { get; set; }

    /// <summary>Whether the item is visible and purchasable.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Ordering weight in the shop listing.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
///     Request to adjust a user's balance from the dashboard.
/// </summary>
public class AdjustBalanceRequest
{
    /// <summary>The user to adjust.</summary>
    public ulong UserId { get; set; }

    /// <summary>The amount to add. Negative removes currency.</summary>
    public long Amount { get; set; }

    /// <summary>Reason recorded on the ledger entry.</summary>
    public string? Reason { get; set; }
}