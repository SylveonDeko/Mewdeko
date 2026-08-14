using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     A quantity of a <see cref="ShopItem" /> owned by a user in a guild.
/// </summary>
[Table("UserInventoryItems")]
public class UserInventoryItem
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild this inventory entry belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The owning user.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     The shop item owned.
    /// </summary>
    [Column("ShopItemId")]
    public int ShopItemId { get; set; }

    /// <summary>
    ///     How many of the item the user owns.
    /// </summary>
    [Column("Quantity")]
    public int Quantity { get; set; }

    /// <summary>
    ///     Total currency the user has spent on this item across all purchases, kept for analytics
    ///     even after the item is consumed.
    /// </summary>
    [Column("TotalPaid")]
    public long TotalPaid { get; set; }

    /// <summary>
    ///     When the user first acquired the item.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}