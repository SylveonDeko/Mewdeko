using DataModel;

namespace Mewdeko.Modules.Currency.Models;

/// <summary>
///     A single line of a user's shop inventory, joined to the item it refers to.
/// </summary>
public class InventoryEntry
{
    /// <summary>
    ///     The shop item owned.
    /// </summary>
    public ShopItem Item { get; set; } = null!;

    /// <summary>
    ///     How many the user owns.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    ///     Total currency spent acquiring this item.
    /// </summary>
    public long TotalPaid { get; set; }
}