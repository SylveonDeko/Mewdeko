using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Models;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Manages the per-guild currency shop and user inventories. Shop purchases are the economy's main
///     sink: without somewhere for currency to go, balances only ever accumulate and the leaderboard
///     stops meaning anything.
/// </summary>
public class ShopService : INService
{
    private readonly ICurrencyService currencyService;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<ShopService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ShopService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="currencyService">The currency service used to charge and refund buyers.</param>
    /// <param name="logger">The logger.</param>
    public ShopService(IDataConnectionFactory dbFactory, ICurrencyService currencyService, ILogger<ShopService> logger)
    {
        this.dbFactory = dbFactory;
        this.currencyService = currencyService;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets a guild's shop items in display order.
    /// </summary>
    /// <param name="guildId">The guild to list.</param>
    /// <param name="includeDisabled">Whether to include items hidden from buyers.</param>
    /// <returns>The guild's shop items.</returns>
    public async Task<IReadOnlyList<ShopItem>> GetItemsAsync(ulong guildId, bool includeDisabled = false)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.ShopItems
            .Where(x => x.GuildId == guildId)
            .Where(x => includeDisabled || x.Enabled)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Price)
            .ToListAsync();
    }

    /// <summary>
    ///     Finds a shop item by its name, case-insensitively.
    /// </summary>
    /// <param name="guildId">The guild to search.</param>
    /// <param name="name">The item name.</param>
    /// <returns>The matching item, or <see langword="null" /> if the guild has no such item.</returns>
    public async Task<ShopItem?> GetItemAsync(ulong guildId, string name)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.ShopItems
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name.ToLower() == name.ToLower());
    }

    /// <summary>
    ///     Creates a new shop item.
    /// </summary>
    /// <param name="item">The item to add. Its guild must already be set.</param>
    /// <returns>The created item, or <see langword="null" /> if the name is already taken.</returns>
    public async Task<ShopItem?> AddItemAsync(ShopItem item)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        item.DateAdded = DateTime.UtcNow;

        try
        {
            item.Id = await db.InsertWithInt32IdentityAsync(item);
            return item;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    ///     Deletes a shop item and every inventory entry referencing it.
    /// </summary>
    /// <param name="guildId">The guild the item belongs to.</param>
    /// <param name="name">The item name.</param>
    /// <returns><see langword="true" /> if an item was removed.</returns>
    public async Task<bool> RemoveItemAsync(ulong guildId, string name)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.ShopItems
            .Where(x => x.GuildId == guildId && x.Name.ToLower() == name.ToLower())
            .DeleteAsync() > 0;
    }

    /// <summary>
    ///     Applies a change to an existing shop item.
    /// </summary>
    /// <param name="guildId">The guild the item belongs to.</param>
    /// <param name="name">The item name.</param>
    /// <param name="mutate">An action applying the desired changes.</param>
    /// <returns>The updated item, or <see langword="null" /> if no such item exists.</returns>
    public async Task<ShopItem?> UpdateItemAsync(ulong guildId, string name, Action<ShopItem> mutate)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var item = await db.ShopItems
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name.ToLower() == name.ToLower());

        if (item is null)
            return null;

        mutate(item);
        await db.UpdateAsync(item);
        return item;
    }

    /// <summary>
    ///     Gets a user's inventory, paired with the shop item each entry refers to.
    /// </summary>
    /// <param name="guildId">The guild to look in.</param>
    /// <param name="userId">The owning user.</param>
    /// <returns>The user's owned items and quantities.</returns>
    public async Task<IReadOnlyList<InventoryEntry>> GetInventoryAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await (from inv in db.UserInventoryItems
                join item in db.ShopItems on inv.ShopItemId equals item.Id
                where inv.GuildId == guildId && inv.UserId == userId && inv.Quantity > 0
                orderby item.SortOrder, item.Name
                select new InventoryEntry
                {
                    Item = item, Quantity = inv.Quantity, TotalPaid = inv.TotalPaid
                })
            .ToListAsync();
    }

    /// <summary>
    ///     Purchases one unit of an item for a user.
    /// </summary>
    /// <remarks>
    ///     Funds are taken first with a conditional debit, then stock and the per-user limit are claimed.
    ///     If either claim fails the earlier steps are undone, so a buyer is never charged for an item
    ///     they did not receive and stock is never consumed without payment.
    /// </remarks>
    /// <param name="guildId">The guild the purchase happens in.</param>
    /// <param name="userId">The buying user.</param>
    /// <param name="name">The item name.</param>
    /// <param name="userRoleIds">The roles the buyer holds, used to enforce purchase requirements.</param>
    /// <returns>The outcome of the purchase and the item involved.</returns>
    public async Task<PurchaseResult> PurchaseAsync(ulong guildId, ulong userId, string name,
        IReadOnlyCollection<ulong> userRoleIds)
    {
        var item = await GetItemAsync(guildId, name);

        if (item is null)
            return new PurchaseResult(PurchaseOutcome.NoSuchItem, null);
        if (!item.Enabled)
            return new PurchaseResult(PurchaseOutcome.Disabled, item);
        if (item.RequiredRoleId.HasValue && !userRoleIds.Contains(item.RequiredRoleId.Value))
            return new PurchaseResult(PurchaseOutcome.MissingRequiredRole, item);
        if (item.Stock == 0)
            return new PurchaseResult(PurchaseOutcome.OutOfStock, item);

        if (!await currencyService.TryDebitAsync(userId, item.Price, $"Shop: {item.Name}",
                CurrencyCategory.ShopPurchase, guildId))
            return new PurchaseResult(PurchaseOutcome.InsufficientFunds, item);

        await using var db = await dbFactory.CreateConnectionAsync();

        if (item.Stock > 0)
        {
            var stockTaken = await db.ShopItems
                .Where(x => x.Id == item.Id && x.Stock > 0)
                .Set(x => x.Stock, x => x.Stock - 1)
                .UpdateAsync();

            if (stockTaken == 0)
            {
                await RefundAsync(guildId, userId, item, "Shop refund: out of stock");
                return new PurchaseResult(PurchaseOutcome.OutOfStock, item);
            }
        }

        var maxPerUser = item.MaxPerUser;

        var granted = await db.UserInventoryItems
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.ShopItemId == item.Id)
            .Where(x => maxPerUser <= 0 || x.Quantity < maxPerUser)
            .Set(x => x.Quantity, x => x.Quantity + 1)
            .Set(x => x.TotalPaid, x => x.TotalPaid + item.Price)
            .UpdateAsync();

        if (granted == 0)
        {
            var alreadyOwns = await db.UserInventoryItems
                .AnyAsync(x => x.GuildId == guildId && x.UserId == userId && x.ShopItemId == item.Id);

            if (alreadyOwns)
            {
                await RestoreStockAsync(db, item);
                await RefundAsync(guildId, userId, item, "Shop refund: ownership limit");
                return new PurchaseResult(PurchaseOutcome.LimitReached, item);
            }

            try
            {
                await db.InsertAsync(new UserInventoryItem
                {
                    GuildId = guildId,
                    UserId = userId,
                    ShopItemId = item.Id,
                    Quantity = 1,
                    TotalPaid = item.Price,
                    DateAdded = DateTime.UtcNow
                });
            }
            catch (Exception)
            {
                var retried = await db.UserInventoryItems
                    .Where(x => x.GuildId == guildId && x.UserId == userId && x.ShopItemId == item.Id)
                    .Where(x => maxPerUser <= 0 || x.Quantity < maxPerUser)
                    .Set(x => x.Quantity, x => x.Quantity + 1)
                    .Set(x => x.TotalPaid, x => x.TotalPaid + item.Price)
                    .UpdateAsync();

                if (retried == 0)
                {
                    await RestoreStockAsync(db, item);
                    await RefundAsync(guildId, userId, item, "Shop refund: ownership limit");
                    return new PurchaseResult(PurchaseOutcome.LimitReached, item);
                }
            }
        }

        return new PurchaseResult(PurchaseOutcome.Success, item);
    }

    /// <summary>
    ///     Consumes one unit of a consumable item from a user's inventory.
    /// </summary>
    /// <param name="guildId">The guild the inventory belongs to.</param>
    /// <param name="userId">The owning user.</param>
    /// <param name="name">The item name.</param>
    /// <returns>The outcome of the attempt and the item involved.</returns>
    public async Task<PurchaseResult> ConsumeAsync(ulong guildId, ulong userId, string name)
    {
        var item = await GetItemAsync(guildId, name);

        if (item is null)
            return new PurchaseResult(PurchaseOutcome.NoSuchItem, null);
        if (!item.Consumable)
            return new PurchaseResult(PurchaseOutcome.NotConsumable, item);

        await using var db = await dbFactory.CreateConnectionAsync();

        var consumed = await db.UserInventoryItems
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.ShopItemId == item.Id && x.Quantity > 0)
            .Set(x => x.Quantity, x => x.Quantity - 1)
            .UpdateAsync();

        return consumed > 0
            ? new PurchaseResult(PurchaseOutcome.Success, item)
            : new PurchaseResult(PurchaseOutcome.NotOwned, item);
    }

    /// <summary>
    ///     Sums everything a guild's users have spent in the shop, for the sink breakdown in analytics.
    /// </summary>
    /// <param name="guildId">The guild to total.</param>
    /// <returns>The total currency spent on shop items.</returns>
    public async Task<long> GetTotalSpentAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.UserInventoryItems
            .Where(x => x.GuildId == guildId)
            .SumAsync(x => (long?)x.TotalPaid) ?? 0;
    }

    private async Task RestoreStockAsync(MewdekoDb db, ShopItem item)
    {
        if (item.Stock <= 0)
            return;

        await db.ShopItems
            .Where(x => x.Id == item.Id && x.Stock >= 0)
            .Set(x => x.Stock, x => x.Stock + 1)
            .UpdateAsync();
    }

    private async Task RefundAsync(ulong guildId, ulong userId, ShopItem item, string reason)
    {
        await currencyService.CreditAsync(userId, item.Price, reason, CurrencyCategory.ShopRefund, guildId);
        logger.LogInformation("Refunded {Price} to {UserId} in {GuildId}: {Reason}", item.Price, userId, guildId,
            reason);
    }
}