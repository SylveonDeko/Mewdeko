using DataModel;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Models;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     The shop slash commands: browsing, buying, inventories and consuming items, plus the
///     administrative shop-admin subgroup.
/// </summary>
public partial class SlashCurrency
{
    /// <summary>
    ///     The shop service handling listings, purchases and inventories.
    /// </summary>
    public ShopService ShopService { get; set; }

    /// <summary>
    ///     Lists everything the server currently sells.
    /// </summary>
    [SlashCommand("shop", "Lists everything the server currently sells")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Shop()
    {
        await DeferAsync();
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

        await Interactive.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(30),
            InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);

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
    [SlashCommand("buy", "Buys an item from the server shop")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Buy([Summary("name", "The name of the item to buy")] string name)
    {
        await DeferAsync();
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
    [SlashCommand("inventory", "Shows the items a user owns")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Inventory([Summary("user", "The user to inspect, defaults to yourself")] IUser? user = null)
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

        await ctx.Interaction.RespondAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Uses up one of a consumable item you own.
    /// </summary>
    /// <param name="name">The name of the item to use.</param>
    [SlashCommand("use", "Uses up one of a consumable item you own")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Use([Summary("name", "The name of the item to use")] string name)
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
    ///     Administrative commands for managing the server shop.
    /// </summary>
    [Group("shop-admin", "Manage the server shop")]
    public class CurrencyShopAdmin(ShopService shopService) : MewdekoSlashSubmodule<ICurrencyService>
    {
        /// <summary>
        ///     Adds a role to the shop for users to buy.
        /// </summary>
        /// <param name="price">What the role costs.</param>
        /// <param name="role">The role granted on purchase.</param>
        /// <param name="name">The display name of the shop entry.</param>
        [SlashCommand("add-role", "Adds a role to the shop for users to buy")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task ShopAddRole([Summary("price", "What the role costs")] long price,
            [Summary("role", "The role granted on purchase")]
            IRole role,
            [Summary("name", "The display name of the shop entry")]
            string name)
        {
            if (price < 0)
            {
                await ErrorAsync(Strings.ShopInvalidPrice(ctx.Guild.Id));
                return;
            }

            var item = await shopService.AddItemAsync(new ShopItem
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
        [SlashCommand("add-item", "Adds a plain collectible item to the shop")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ShopAddItem([Summary("price", "What the item costs")] long price,
            [Summary("name", "The display name of the shop entry")]
            string name)
        {
            if (price < 0)
            {
                await ErrorAsync(Strings.ShopInvalidPrice(ctx.Guild.Id));
                return;
            }

            var item = await shopService.AddItemAsync(new ShopItem
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
        [SlashCommand("remove", "Removes an item from the shop along with everyone's copies")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ShopRemove([Summary("name", "The name of the item to remove")] string name)
        {
            if (!await shopService.RemoveItemAsync(ctx.Guild.Id, name))
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
        [SlashCommand("price", "Sets the price of an existing shop item")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ShopPrice([Summary("price", "The new price")] long price,
            [Summary("name", "The name of the item to reprice")]
            string name)
        {
            if (price < 0)
            {
                await ErrorAsync(Strings.ShopInvalidPrice(ctx.Guild.Id));
                return;
            }

            var item = await shopService.UpdateItemAsync(ctx.Guild.Id, name, x => x.Price = price);

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
        [SlashCommand("stock", "Sets how many of an item remain for sale, -1 for unlimited")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ShopStock([Summary("stock", "The remaining stock, or -1 for unlimited")] int stock,
            [Summary("name", "The name of the item to restock")]
            string name)
        {
            var item = await shopService.UpdateItemAsync(ctx.Guild.Id, name, x => x.Stock = stock < 0 ? -1 : stock);

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
        ///     Shows or hides a shop item without deleting it or anyone's copies.
        /// </summary>
        /// <param name="name">The name of the item to toggle.</param>
        [SlashCommand("toggle", "Shows or hides a shop item without deleting it")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ShopToggle([Summary("name", "The name of the item to toggle")] string name)
        {
            var item = await shopService.UpdateItemAsync(ctx.Guild.Id, name, x => x.Enabled = !x.Enabled);

            if (item is null)
            {
                await ErrorAsync(Strings.ShopNoSuchItem(ctx.Guild.Id, name));
                return;
            }

            await ConfirmAsync(item.Enabled
                ? Strings.ShopItemEnabled(ctx.Guild.Id, item.Name)
                : Strings.ShopItemHidden(ctx.Guild.Id, item.Name));
        }

        /// <summary>
        ///     Sets the description shown for a shop item.
        /// </summary>
        /// <param name="name">The name of the item.</param>
        /// <param name="description">The new description.</param>
        [SlashCommand("description", "Sets the description shown for a shop item")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ShopDesc([Summary("name", "The name of the item")] string name,
            [Summary("description", "The new description")]
            string description)
        {
            var itemName = name.Trim();
            var item = await shopService.UpdateItemAsync(ctx.Guild.Id, itemName,
                x => x.Description = description.Trim());

            if (item is null)
            {
                await ErrorAsync(Strings.ShopNoSuchItem(ctx.Guild.Id, itemName));
                return;
            }

            await ConfirmAsync(Strings.ShopDescSet(ctx.Guild.Id, item.Name));
        }
    }
}