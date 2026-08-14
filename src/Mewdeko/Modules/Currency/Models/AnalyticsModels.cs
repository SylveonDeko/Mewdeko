namespace Mewdeko.Modules.Currency.Models;

/// <summary>
///     A snapshot of how much currency exists in a guild and how evenly it is spread.
/// </summary>
public class EconomySnapshot
{
    /// <summary>
    ///     Total currency in existence, wallets and banks combined.
    /// </summary>
    public long MoneySupply { get; set; }

    /// <summary>
    ///     Total currency held in wallets, which is the portion exposed to robbery and wagering.
    /// </summary>
    public long InWallets { get; set; }

    /// <summary>
    ///     Total currency held in banks.
    /// </summary>
    public long InBanks { get; set; }

    /// <summary>
    ///     Number of users holding a non-zero balance.
    /// </summary>
    public int Holders { get; set; }

    /// <summary>
    ///     Mean holding across all holders.
    /// </summary>
    public long Mean { get; set; }

    /// <summary>
    ///     Median holding, which diverges sharply from the mean once a few users dominate.
    /// </summary>
    public long Median { get; set; }

    /// <summary>
    ///     Gini coefficient of the holdings distribution, from 0 for perfect equality to 1 for total
    ///     concentration in one user. Useful as a single number for whether an economy has ossified.
    /// </summary>
    public double Gini { get; set; }

    /// <summary>
    ///     Share of the money supply held by the richest ten percent of holders.
    /// </summary>
    public double TopTenPercentShare { get; set; }
}

/// <summary>
///     Net currency created or destroyed by one ledger category over a window.
/// </summary>
/// <param name="Category">The ledger category.</param>
/// <param name="In">Total credited to users.</param>
/// <param name="Out">Total debited from users, as a positive number.</param>
/// <param name="Entries">How many ledger rows contributed.</param>
public readonly record struct FlowBucket(string Category, long In, long Out, int Entries)
{
    /// <summary>
    ///     Net effect on the money supply. Positive means this category is a faucet.
    /// </summary>
    public long Net
    {
        get
        {
            return In - Out;
        }
    }
}

/// <summary>
///     Realized performance of a single game over a window.
/// </summary>
/// <param name="Game">The stable game key.</param>
/// <param name="Wagered">Total staked by players.</param>
/// <param name="Returned">Total paid back to players.</param>
/// <param name="Plays">How many wagers were placed.</param>
/// <param name="Players">How many distinct users played.</param>
public readonly record struct GamePerformance(string Game, long Wagered, long Returned, int Plays, int Players)
{
    /// <summary>
    ///     Actual return to player, as a fraction of the amount wagered. Values above 1 mean the game
    ///     has been paying out more than it takes and is inflating the economy.
    /// </summary>
    public double ActualRtp
    {
        get
        {
            return Wagered <= 0 ? 0 : (double)Returned / Wagered;
        }
    }

    /// <summary>
    ///     Net currency the game removed from circulation. Negative means it created currency.
    /// </summary>
    public long HouseTake
    {
        get
        {
            return Wagered - Returned;
        }
    }
}

/// <summary>
///     Net change in the money supply on a single day.
/// </summary>
/// <param name="Date">The day, in UTC.</param>
/// <param name="Net">Currency created minus currency destroyed.</param>
public readonly record struct SupplyPoint(DateTime Date, long Net);