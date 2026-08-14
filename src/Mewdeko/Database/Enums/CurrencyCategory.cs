namespace Mewdeko.Database.Enums;

/// <summary>
///     Stable, never-localized classification written onto every ledger entry so faucets and sinks can
///     be told apart after the fact. Stored as the enum name, not its value, so analytics stay readable
///     in the database and survive reordering.
/// </summary>
public enum CurrencyCategory
{
    /// <summary>
    ///     Entries written before the ledger was categorized.
    /// </summary>
    Legacy,

    /// <summary>
    ///     Wagers taken by a game. Always a debit.
    /// </summary>
    GameBet,

    /// <summary>
    ///     Winnings paid out by a game. Always a credit.
    /// </summary>
    GamePayout,

    /// <summary>
    ///     Daily reward claims, including streak bonuses.
    /// </summary>
    Daily,

    /// <summary>
    ///     Payouts from the work command.
    /// </summary>
    Work,

    /// <summary>
    ///     Proceeds from a successful crime.
    /// </summary>
    Crime,

    /// <summary>
    ///     Fines charged for a failed crime.
    /// </summary>
    CrimeFine,

    /// <summary>
    ///     Currency taken from or lost to another user through robbery.
    /// </summary>
    Rob,

    /// <summary>
    ///     Penalty paid by a failed robber.
    /// </summary>
    RobFine,

    /// <summary>
    ///     Currency sent to another user.
    /// </summary>
    PaySent,

    /// <summary>
    ///     Currency received from another user.
    /// </summary>
    PayReceived,

    /// <summary>
    ///     Tax destroyed on a transfer.
    /// </summary>
    PayTax,

    /// <summary>
    ///     Movement between wallet and bank. Net neutral to the money supply.
    /// </summary>
    BankTransfer,

    /// <summary>
    ///     Interest paid on banked currency.
    /// </summary>
    BankInterest,

    /// <summary>
    ///     Currency spent in the shop.
    /// </summary>
    ShopPurchase,

    /// <summary>
    ///     Currency returned by a shop refund.
    /// </summary>
    ShopRefund,

    /// <summary>
    ///     Rewards granted by the XP system.
    /// </summary>
    XpReward,

    /// <summary>
    ///     Completion rewards from daily challenges.
    /// </summary>
    Challenge,

    /// <summary>
    ///     Manual adjustment by a server administrator.
    /// </summary>
    AdminAdjust
}