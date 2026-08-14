namespace Mewdeko.Modules.Currency.Services.Impl;

/// <summary>
///     Represents a user's balance in the currency system.
/// </summary>
public class LbCurrency
{
    /// <summary>
    ///     Gets or sets the balance amount.
    /// </summary>
    public long Balance { get; set; }

    /// <summary>
    ///     Gets or sets the banked amount.
    /// </summary>
    public long Bank { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets the user's total holdings across wallet and bank.
    /// </summary>
    public long NetWorth
    {
        get
        {
            return Balance + Bank;
        }
    }
}