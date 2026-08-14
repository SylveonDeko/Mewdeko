namespace Mewdeko.Database.Enums;

/// <summary>
///     What a shop item delivers to the buyer when purchased.
/// </summary>
public enum ShopItemType
{
    /// <summary>
    ///     Grants a Discord role on purchase.
    /// </summary>
    Role = 0,

    /// <summary>
    ///     A pure inventory entry with no side effect, used for collectibles and server-run events.
    /// </summary>
    Collectible = 1,

    /// <summary>
    ///     Sends the buyer a block of text, such as a code or set of instructions.
    /// </summary>
    Text = 2
}