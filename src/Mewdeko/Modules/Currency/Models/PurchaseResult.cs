using DataModel;

namespace Mewdeko.Modules.Currency.Models;

/// <summary>
///     Why a shop purchase or item use did or did not go through.
/// </summary>
public enum PurchaseOutcome
{
    /// <summary>
    ///     The purchase completed and the item is in the buyer's inventory.
    /// </summary>
    Success,

    /// <summary>
    ///     The guild has no item by that name.
    /// </summary>
    NoSuchItem,

    /// <summary>
    ///     The item exists but is currently hidden from buyers.
    /// </summary>
    Disabled,

    /// <summary>
    ///     The buyer lacks the role the item requires.
    /// </summary>
    MissingRequiredRole,

    /// <summary>
    ///     The item has no stock left.
    /// </summary>
    OutOfStock,

    /// <summary>
    ///     The buyer's wallet did not cover the price.
    /// </summary>
    InsufficientFunds,

    /// <summary>
    ///     The buyer already owns as many as the item permits.
    /// </summary>
    LimitReached,

    /// <summary>
    ///     The item cannot be consumed.
    /// </summary>
    NotConsumable,

    /// <summary>
    ///     The user does not own the item they tried to use.
    /// </summary>
    NotOwned
}

/// <summary>
///     The outcome of a shop purchase or item use, together with the item it concerned.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Item">The item involved, or <see langword="null" /> when no item matched.</param>
public readonly record struct PurchaseResult(PurchaseOutcome Outcome, ShopItem? Item);