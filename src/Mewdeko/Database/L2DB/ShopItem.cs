using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     An item a guild offers for sale in its currency shop. Shop purchases are the primary sink that
///     removes currency from circulation.
/// </summary>
[Table("ShopItems")]
public class ShopItem
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild this item belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Display name, unique per guild case-insensitively. Used as the purchase identifier.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = null!;

    /// <summary>
    ///     Optional longer description shown in the shop listing.
    /// </summary>
    [Column("Description")]
    public string? Description { get; set; }

    /// <summary>
    ///     Cost in guild currency.
    /// </summary>
    [Column("Price")]
    public long Price { get; set; }

    /// <summary>
    ///     What the item does on purchase. See <see cref="Mewdeko.Database.Enums.ShopItemType" />.
    /// </summary>
    [Column("ItemType")]
    public int ItemType { get; set; }

    /// <summary>
    ///     Role granted when <see cref="ItemType" /> is a role item.
    /// </summary>
    [Column("RoleId")]
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     Text delivered to the buyer when <see cref="ItemType" /> is a text reward.
    /// </summary>
    [Column("TextContent")]
    public string? TextContent { get; set; }

    /// <summary>
    ///     Remaining stock, or -1 for unlimited.
    /// </summary>
    [Column("Stock")]
    public int Stock { get; set; }

    /// <summary>
    ///     Maximum quantity a single user may own, or 0 for unlimited.
    /// </summary>
    [Column("MaxPerUser")]
    public int MaxPerUser { get; set; }

    /// <summary>
    ///     Role a buyer must already have to purchase this item, if any.
    /// </summary>
    [Column("RequiredRoleId")]
    public ulong? RequiredRoleId { get; set; }

    /// <summary>
    ///     Whether owning the item allows spending it via the use command, removing it from inventory.
    /// </summary>
    [Column("Consumable")]
    public bool Consumable { get; set; }

    /// <summary>
    ///     Whether the item currently appears in the shop and can be bought.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    ///     Ordering weight within the shop listing, ascending.
    /// </summary>
    [Column("SortOrder")]
    public int SortOrder { get; set; }

    /// <summary>
    ///     When this item was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}