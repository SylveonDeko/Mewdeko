namespace Mewdeko.Controllers.Common.Currency;

/// <summary>
///     A single row of the currency leaderboard, resolved to a Discord user.
/// </summary>
public class LeaderboardEntryResponse
{
    /// <summary>The user's position, starting at 1.</summary>
    public int Rank { get; set; }

    /// <summary>The user's ID.</summary>
    public ulong UserId { get; set; }

    /// <summary>The user's display name, or null if they could not be resolved.</summary>
    public string? Username { get; set; }

    /// <summary>The user's avatar URL, or null if unavailable.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Spendable currency.</summary>
    public long Wallet { get; set; }

    /// <summary>Banked currency.</summary>
    public long Bank { get; set; }

    /// <summary>Wallet and bank combined.</summary>
    public long NetWorth { get; set; }

    /// <summary>This user's share of the guild's total money supply, from 0 to 1.</summary>
    public double ShareOfSupply { get; set; }
}

/// <summary>
///     Snapshot of a guild's money supply and how concentrated it is.
/// </summary>
public class EconomySnapshotResponse
{
    /// <summary>Total currency in existence across wallets and banks.</summary>
    public long MoneySupply { get; set; }

    /// <summary>Total held in wallets, which is what robbery and wagering can reach.</summary>
    public long InWallets { get; set; }

    /// <summary>Total held in banks.</summary>
    public long InBanks { get; set; }

    /// <summary>Number of users holding anything.</summary>
    public int Holders { get; set; }

    /// <summary>Mean holding.</summary>
    public long Mean { get; set; }

    /// <summary>Median holding.</summary>
    public long Median { get; set; }

    /// <summary>Gini coefficient, 0 for perfect equality to 1 for total concentration.</summary>
    public double Gini { get; set; }

    /// <summary>Share of the supply held by the richest tenth of holders.</summary>
    public double TopTenPercentShare { get; set; }

    /// <summary>Net currency created minus destroyed over the requested window.</summary>
    public long NetChange { get; set; }
}

/// <summary>
///     Net currency created or destroyed by one ledger category.
/// </summary>
public class FlowBucketResponse
{
    /// <summary>The ledger category.</summary>
    public string Category { get; set; } = null!;

    /// <summary>Total credited to users.</summary>
    public long In { get; set; }

    /// <summary>Total debited from users, as a positive number.</summary>
    public long Out { get; set; }

    /// <summary>Net effect on the supply. Positive means this category is a faucet.</summary>
    public long Net { get; set; }

    /// <summary>How many ledger rows contributed.</summary>
    public int Entries { get; set; }
}

/// <summary>
///     Realized performance of one game over a window.
/// </summary>
public class GamePerformanceResponse
{
    /// <summary>The stable game key.</summary>
    public string Game { get; set; } = null!;

    /// <summary>Total staked by players.</summary>
    public long Wagered { get; set; }

    /// <summary>Total returned to players.</summary>
    public long Returned { get; set; }

    /// <summary>Return to player as a fraction of the amount wagered.</summary>
    public double ActualRtp { get; set; }

    /// <summary>Net currency the game removed from circulation.</summary>
    public long HouseTake { get; set; }

    /// <summary>How many wagers were placed.</summary>
    public int Plays { get; set; }

    /// <summary>How many distinct users played.</summary>
    public int Players { get; set; }
}

/// <summary>
///     Net change in the money supply on one day.
/// </summary>
public class SupplyPointResponse
{
    /// <summary>The day, in UTC.</summary>
    public DateTime Date { get; set; }

    /// <summary>Currency created minus destroyed.</summary>
    public long Net { get; set; }
}

/// <summary>
///     Everything the analytics view needs, in one round trip.
/// </summary>
public class EconomyAnalyticsResponse
{
    /// <summary>Current supply and distribution.</summary>
    public EconomySnapshotResponse Snapshot { get; set; } = null!;

    /// <summary>Where currency entered and left circulation.</summary>
    public List<FlowBucketResponse> Flow { get; set; } = [];

    /// <summary>Per-game realized performance.</summary>
    public List<GamePerformanceResponse> Games { get; set; } = [];

    /// <summary>Daily net supply change.</summary>
    public List<SupplyPointResponse> SupplyHistory { get; set; } = [];

    /// <summary>Currency destroyed as transfer tax over the window.</summary>
    public long TransferTax { get; set; }

    /// <summary>How many days the flow, game and history figures cover.</summary>
    public int WindowDays { get; set; }
}

/// <summary>
///     A shop item as the dashboard sees it, with its role resolved where applicable.
/// </summary>
public class ShopItemResponse
{
    /// <summary>The item's ID.</summary>
    public int Id { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Cost in guild currency.</summary>
    public long Price { get; set; }

    /// <summary>What the item delivers.</summary>
    public int ItemType { get; set; }

    /// <summary>Role granted for role items.</summary>
    public ulong? RoleId { get; set; }

    /// <summary>Name of the granted role, if it still exists.</summary>
    public string? RoleName { get; set; }

    /// <summary>Text delivered for text items.</summary>
    public string? TextContent { get; set; }

    /// <summary>Remaining stock, or -1 for unlimited.</summary>
    public int Stock { get; set; }

    /// <summary>Maximum a single user may own, or 0 for unlimited.</summary>
    public int MaxPerUser { get; set; }

    /// <summary>Role required to purchase, if any.</summary>
    public ulong? RequiredRoleId { get; set; }

    /// <summary>Name of the required role, if it still exists.</summary>
    public string? RequiredRoleName { get; set; }

    /// <summary>Whether the item can be used up.</summary>
    public bool Consumable { get; set; }

    /// <summary>Whether the item is visible and purchasable.</summary>
    public bool Enabled { get; set; }

    /// <summary>Ordering weight in the shop listing.</summary>
    public int SortOrder { get; set; }

    /// <summary>How many of this item users currently hold.</summary>
    public int Owned { get; set; }

    /// <summary>Total currency users have spent on this item.</summary>
    public long Revenue { get; set; }
}