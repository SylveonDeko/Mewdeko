using DataModel;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Models;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     The shop: the sink that gives currency somewhere to go.
/// </summary>
public partial class Currency
{
    /// <summary>
    ///     The shop service handling listings, purchases and inventories.
    /// </summary>
    public ShopService ShopService { get; set; }

    /// <summary>
    ///     Lists everything the server currently sells.
    /// </summary>
    /// <example>.shop</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Shop()
    {
        var items = await ShopService.GetItemsAsync(ctx.Guild.Id);

        if (items.Count == 0)
        {
            await ErrorAsync(Strings.ShopEmpty(ctx.Guild.Id));
            return;
        }

        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((items.Count - 1) / 8)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(30));

        Task<PageBuilder> PageFactory(int index)
        {
            var page = new PageBuilder()
                .WithTitle(Strings.ShopTitle(ctx.Guild.Id, ctx.Guild.Name))
                .WithDescription(Strings.ShopDescription(ctx.Guild.Id))
                .WithOkColor();

            for (var i = index * 8; i < (index + 1) * 8 && i < items.Count; i++)
            {
                var item = items[i];
                var details = new List<string>
                {
                    Strings.ShopItemPrice(ctx.Guild.Id, item.Price, emote)
                };

                if (item.Stock >= 0)
                    details.Add(Strings.ShopItemStock(ctx.Guild.Id, item.Stock));
                if (item.MaxPerUser > 0)
                    details.Add(Strings.ShopItemLimit(ctx.Guild.Id, item.MaxPerUser));
                if (item.RequiredRoleId.HasValue)
                    details.Add(Strings.ShopItemRequiresRole(ctx.Guild.Id, $"<@&{item.RequiredRoleId.Value}>"));
                if (!string.IsNullOrWhiteSpace(item.Description))
                    details.Add(item.Description);

                page.AddField(item.Name, string.Join("\n", details));
            }

            return Task.FromResult(page);
        }
    }

    /// <summary>
    ///     Buys an item from the server shop.
    /// </summary>
    /// <param name="name">The name of the item to buy.</param>
    /// <example>.buy VIP Role</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Buy([Remainder] string name)
    {
        var guildUser = (IGuildUser)ctx.User;
        var result = await ShopService.PurchaseAsync(ctx.Guild.Id, ctx.User.Id, name, guildUser.RoleIds.ToList());
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        switch (result.Outcome)
        {
            case PurchaseOutcome.NoSuchItem:
                await ErrorAsync(Strings.ShopNoSuchItem(ctx.Guild.Id, name));
                return;
            case PurchaseOutcome.Disabled:
                await ErrorAsync(Strings.ShopItemDisabled(ctx.Guild.Id, name));
                return;
            case PurchaseOutcome.MissingRequiredRole:
                await ErrorAsync(Strings.ShopMissingRole(ctx.Guild.Id,
                    $"<@&{result.Item!.RequiredRoleId!.Value}>"));
                return;
            case PurchaseOutcome.OutOfStock:
                await ErrorAsync(Strings.ShopOutOfStock(ctx.Guild.Id, name));
                return;
            case PurchaseOutcome.InsufficientFunds:
                await ErrorAsync(Strings.ShopInsufficientFunds(ctx.Guild.Id, result.Item!.Price, emote));
                return;
            case PurchaseOutcome.LimitReached:
                await ErrorAsync(Strings.ShopLimitReached(ctx.Guild.Id, result.Item!.MaxPerUser, name));
                return;
        }

        var item = result.Item!;

        if (item.ItemType == (int)ShopItemType.Role && item.RoleId.HasValue)
        {
            var role = ctx.Guild.GetRole(item.RoleId.Value);

            if (role is not null)
            {
                try
                {
                    await guildUser.AddRoleAsync(role);
                }
                catch (Exception)
                {
                    await ErrorAsync(Strings.ShopRoleGrantFailed(ctx.Guild.Id, role.Name));
                    return;
                }
            }
        }

        if (item.ItemType == (int)ShopItemType.Text && !string.IsNullOrWhiteSpace(item.TextContent))
        {
            try
            {
                await guildUser.SendMessageAsync(Strings.ShopTextDelivery(ctx.Guild.Id, item.Name, item.TextContent));
            }
            catch (Exception)
            {
                await ErrorAsync(Strings.ShopDmFailed(ctx.Guild.Id));
            }
        }

        await ConfirmAsync(Strings.ShopPurchased(ctx.Guild.Id, item.Name, item.Price, emote));
    }

    /// <summary>
    ///     Shows the items a user owns.
    /// </summary>
    /// <param name="user">The user whose inventory to show. Defaults to yourself.</param>
    /// <example>.inventory</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Inventory(IUser? user = null)
    {
        user ??= ctx.User;

        var entries = await ShopService.GetInventoryAsync(ctx.Guild.Id, user.Id);

        if (entries.Count == 0)
        {
            await ErrorAsync(Strings.InventoryEmpty(ctx.Guild.Id, user.Username));
            return;
        }

        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.InventoryTitle(ctx.Guild.Id, user.Username))
            .WithFooter(Strings.InventoryFooter(ctx.Guild.Id, entries.Sum(x => x.TotalPaid), emote));

        foreach (var entry in entries.Take(25))
        {
            eb.AddField($"{entry.Item.Name} x{entry.Quantity}",
                entry.Item.Consumable
                    ? Strings.InventoryConsumable(ctx.Guild.Id, entry.Item.Name)
                    : entry.Item.Description ?? Strings.InventoryNoDescription(ctx.Guild.Id), true);
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Uses up one of a consumable item you own.
    /// </summary>
    /// <param name="name">The name of the item to use.</param>
    /// <example>.use Lottery Ticket</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Use([Remainder] string name)
    {
        var result = await ShopService.ConsumeAsync(ctx.Guild.Id, ctx.User.Id, name);

        switch (result.Outcome)
        {
            case PurchaseOutcome.NoSuchItem:
                await ErrorAsync(Strings.ShopNoSuchItem(ctx.Guild.Id, name));
                return;
            case PurchaseOutcome.NotConsumable:
                await ErrorAsync(Strings.UseNotConsumable(ctx.Guild.Id, name));
                return;
            case PurchaseOutcome.NotOwned:
                await ErrorAsync(Strings.UseNotOwned(ctx.Guild.Id, name));
                return;
        }

        var item = result.Item!;

        if (item.ItemType == (int)ShopItemType.Text && !string.IsNullOrWhiteSpace(item.TextContent))
        {
            try
            {
                await ctx.User.SendMessageAsync(Strings.ShopTextDelivery(ctx.Guild.Id, item.Name, item.TextContent));
            }
            catch (Exception)
            {
                await ErrorAsync(Strings.ShopDmFailed(ctx.Guild.Id));
            }
        }

        await ConfirmAsync(Strings.UseSuccess(ctx.Guild.Id, item.Name));
    }

    /// <summary>
    ///     Adds a role to the shop for users to buy.
    /// </summary>
    /// <param name="price">What the role costs.</param>
    /// <param name="role">The role granted on purchase.</param>
    /// <param name="name">The display name of the shop entry.</param>
    /// <example>.shopaddrole 5000 @VIP VIP Access</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task ShopAddRole(long price, IRole role, [Remainder] string name)
    {
        if (price < 0)
        {
            await ErrorAsync(Strings.ShopInvalidPrice(ctx.Guild.Id));
            return;
        }

        var item = await ShopService.AddItemAsync(new ShopItem
        {
            GuildId = ctx.Guild.Id,
            Name = name,
            Price = price,
            ItemType = (int)ShopItemType.Role,
            RoleId = role.Id,
            Stock = -1,
            MaxPerUser = 1,
            Enabled = true
        });

        if (item is null)
        {
            await ErrorAsync(Strings.ShopDuplicateName(ctx.Guild.Id, name));
            return;
        }

        await ConfirmAsync(Strings.ShopItemAdded(ctx.Guild.Id, name, price,
            await Service.GetCurrencyEmote(ctx.Guild.Id)));
    }

    /// <summary>
    ///     Adds a plain collectible item to the shop, with no automatic effect on purchase.
    /// </summary>
    /// <param name="price">What the item costs.</param>
    /// <param name="name">The display name of the shop entry.</param>
    /// <example>.shopadditem 250 Lottery Ticket</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ShopAddItem(long price, [Remainder] string name)
    {
        if (price < 0)
        {
            await ErrorAsync(Strings.ShopInvalidPrice(ctx.Guild.Id));
            return;
        }

        var item = await ShopService.AddItemAsync(new ShopItem
        {
            GuildId = ctx.Guild.Id,
            Name = name,
            Price = price,
            ItemType = (int)ShopItemType.Collectible,
            Stock = -1,
            Enabled = true,
            Consumable = true
        });

        if (item is null)
        {
            await ErrorAsync(Strings.ShopDuplicateName(ctx.Guild.Id, name));
            return;
        }

        await ConfirmAsync(Strings.ShopItemAdded(ctx.Guild.Id, name, price,
            await Service.GetCurrencyEmote(ctx.Guild.Id)));
    }

    /// <summary>
    ///     Removes an item from the shop, along with everyone's copies of it.
    /// </summary>
    /// <param name="name">The name of the item to remove.</param>
    /// <example>.shopremove VIP Access</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ShopRemove([Remainder] string name)
    {
        if (!await ShopService.RemoveItemAsync(ctx.Guild.Id, name))
        {
            await ErrorAsync(Strings.ShopNoSuchItem(ctx.Guild.Id, name));
            return;
        }

        await ConfirmAsync(Strings.ShopItemRemoved(ctx.Guild.Id, name));
    }

    /// <summary>
    ///     Sets the price of an existing shop item.
    /// </summary>
    /// <param name="price">The new price.</param>
    /// <param name="name">The name of the item to reprice.</param>
    /// <example>.shopprice 7500 VIP Access</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ShopPrice(long price, [Remainder] string name)
    {
        if (price < 0)
        {
            await ErrorAsync(Strings.ShopInvalidPrice(ctx.Guild.Id));
            return;
        }

        var item = await ShopService.UpdateItemAsync(ctx.Guild.Id, name, x => x.Price = price);

        if (item is null)
        {
            await ErrorAsync(Strings.ShopNoSuchItem(ctx.Guild.Id, name));
            return;
        }

        await ConfirmAsync(Strings.ShopPriceSet(ctx.Guild.Id, item.Name, price,
            await Service.GetCurrencyEmote(ctx.Guild.Id)));
    }

    /// <summary>
    ///     Sets how many of an item remain for sale.
    /// </summary>
    /// <param name="stock">The remaining stock, or -1 for unlimited.</param>
    /// <param name="name">The name of the item to restock.</param>
    /// <example>.shopstock 10 VIP Access</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ShopStock(int stock, [Remainder] string name)
    {
        var item = await ShopService.UpdateItemAsync(ctx.Guild.Id, name, x => x.Stock = stock < 0 ? -1 : stock);

        if (item is null)
        {
            await ErrorAsync(Strings.ShopNoSuchItem(ctx.Guild.Id, name));
            return;
        }

        await ConfirmAsync(stock < 0
            ? Strings.ShopStockUnlimited(ctx.Guild.Id, item.Name)
            : Strings.ShopStockSet(ctx.Guild.Id, item.Name, stock));
    }

    /// <summary>
    ///     Sets the description shown for a shop item.
    /// </summary>
    /// <param name="name">The name of the item, followed by a pipe and the new description.</param>
    /// <example>.shopdesc VIP Access | Grants the VIP role and channel access</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ShopDesc([Remainder] string name)
    {
        var parts = name.Split('|', 2);

        if (parts.Length != 2)
        {
            await ErrorAsync(Strings.ShopDescUsage(ctx.Guild.Id));
            return;
        }

        var itemName = parts[0].Trim();
        var description = parts[1].Trim();

        var item = await ShopService.UpdateItemAsync(ctx.Guild.Id, itemName, x => x.Description = description);

        if (item is null)
        {
            await ErrorAsync(Strings.ShopNoSuchItem(ctx.Guild.Id, itemName));
            return;
        }

        await ConfirmAsync(Strings.ShopDescSet(ctx.Guild.Id, item.Name));
    }

    /// <summary>
    ///     Shows or hides a shop item without deleting it or anyone's copies.
    /// </summary>
    /// <param name="name">The name of the item to toggle.</param>
    /// <example>.shoptoggle VIP Access</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ShopToggle([Remainder] string name)
    {
        var item = await ShopService.UpdateItemAsync(ctx.Guild.Id, name, x => x.Enabled = !x.Enabled);

        if (item is null)
        {
            await ErrorAsync(Strings.ShopNoSuchItem(ctx.Guild.Id, name));
            return;
        }

        await ConfirmAsync(item.Enabled
            ? Strings.ShopItemEnabled(ctx.Guild.Id, item.Name)
            : Strings.ShopItemHidden(ctx.Guild.Id, item.Name));
    }
}