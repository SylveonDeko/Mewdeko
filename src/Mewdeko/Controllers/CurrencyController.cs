using DataModel;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.Currency;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.CardDesigns.Services;
using Mewdeko.Modules.Currency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Exposes a guild's economy to the dashboard: balances, the shop, tuning, and the analytics that
///     make the tuning meaningful.
/// </summary>
/// <remarks>
///     The economy figures here come from the same ledger the commands write, so nothing needs to be
///     recomputed or kept in sync separately. Analytics is deliberately one endpoint rather than four,
///     because a dashboard view showing supply without showing what is creating it is not actionable.
/// </remarks>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class CurrencyController : Controller
{
    private readonly CurrencyAnalyticsService analyticsService;
    private readonly DiscordShardedClient client;
    private readonly CurrencyConfigService configService;
    private readonly CardDesignService designs;
    private readonly GuildSettingsService guildSettings;
    private readonly ICurrencyService currencyService;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EconomyBoostService boostService;
    private readonly ILogger<CurrencyController> logger;
    private readonly ShopService shopService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CurrencyController" /> class.
    /// </summary>
    /// <param name="currencyService">The currency service.</param>
    /// <param name="configService">The per-guild economy configuration service.</param>
    /// <param name="analyticsService">The economy analytics service.</param>
    /// <param name="shopService">The shop service.</param>
    /// <param name="boostService">The scheduled boost service.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="client">The Discord client, used to resolve users and roles.</param>
    /// <param name="guildSettings">The guild settings service, which holds the currency emoji and daily reward.</param>
    /// <param name="designs">The card design library, for design shop items.</param>
    /// <param name="logger">The logger.</param>
    public CurrencyController(
        ICurrencyService currencyService,
        CurrencyConfigService configService,
        CurrencyAnalyticsService analyticsService,
        ShopService shopService,
        EconomyBoostService boostService,
        IDataConnectionFactory dbFactory,
        DiscordShardedClient client,
        GuildSettingsService guildSettings,
        CardDesignService designs,
        ILogger<CurrencyController> logger)
    {
        this.guildSettings = guildSettings;
        this.designs = designs;
        this.boostService = boostService;
        this.currencyService = currencyService;
        this.configService = configService;
        this.analyticsService = analyticsService;
        this.shopService = shopService;
        this.dbFactory = dbFactory;
        this.client = client;
        this.logger = logger;
    }

    #region Analytics

    /// <summary>
    ///     Gets the full economy analytics view: supply and distribution now, plus where currency has
    ///     been coming from, where it has been going, and how each game has actually been performing.
    /// </summary>
    /// <param name="guildId">The guild to report on.</param>
    /// <param name="days">How many days back the windowed figures cover. Clamped to 1-365.</param>
    /// <returns>Snapshot, flow breakdown, per-game performance and daily supply history.</returns>
    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics(ulong guildId, [FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var window = TimeSpan.FromDays(days);

        var snapshot = await analyticsService.GetSnapshotAsync(guildId);
        var flow = await analyticsService.GetFlowAsync(guildId, window);
        var games = await analyticsService.GetGamePerformanceAsync(guildId, window);
        var history = await analyticsService.GetSupplyHistoryAsync(guildId, days);
        var tax = await analyticsService.GetTransferTaxAsync(guildId, window);

        return Ok(new EconomyAnalyticsResponse
        {
            WindowDays = days,
            TransferTax = tax,
            Snapshot = new EconomySnapshotResponse
            {
                MoneySupply = snapshot.MoneySupply,
                InWallets = snapshot.InWallets,
                InBanks = snapshot.InBanks,
                Holders = snapshot.Holders,
                Mean = snapshot.Mean,
                Median = snapshot.Median,
                Gini = snapshot.Gini,
                TopTenPercentShare = snapshot.TopTenPercentShare,
                NetChange = history.Sum(x => x.Net)
            },
            Flow = flow.Select(x => new FlowBucketResponse
            {
                Category = x.Category,
                In = x.In,
                Out = x.Out,
                Net = x.Net,
                Entries = x.Entries
            }).ToList(),
            Games = games.Select(x => new GamePerformanceResponse
            {
                Game = x.Game,
                Wagered = x.Wagered,
                Returned = x.Returned,
                ActualRtp = x.ActualRtp,
                HouseTake = x.HouseTake,
                Plays = x.Plays,
                Players = x.Players
            }).ToList(),
            SupplyHistory = history.Select(x => new SupplyPointResponse
            {
                Date = x.Date, Net = x.Net
            }).ToList()
        });
    }

    #endregion

    #region Balances

    /// <summary>
    ///     Gets the guild's currency leaderboard, resolved to Discord users.
    /// </summary>
    /// <param name="guildId">The guild to list.</param>
    /// <param name="page">Zero-based page index.</param>
    /// <param name="pageSize">Entries per page, capped at 100.</param>
    /// <returns>A page of leaderboard entries ordered by net worth.</returns>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(ulong guildId, [FromQuery] int page = 0,
        [FromQuery] int pageSize = 25)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(0, page);

        var guild = client.GetGuild(guildId);
        var balances = (await currencyService.GetAllUserBalancesAsync(guildId))
            .Where(x => x.NetWorth > 0)
            .OrderByDescending(x => x.NetWorth)
            .ToList();

        var supply = balances.Sum(x => x.NetWorth);

        var entries = balances
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select((x, i) =>
            {
                var user = guild?.GetUser(x.UserId);

                return new LeaderboardEntryResponse
                {
                    Rank = (page * pageSize) + i + 1,
                    UserId = x.UserId,
                    Username = user?.Username,
                    AvatarUrl = user?.GetAvatarUrl() ?? user?.GetDefaultAvatarUrl(),
                    Wallet = x.Balance,
                    Bank = x.Bank,
                    NetWorth = x.NetWorth,
                    ShareOfSupply = supply <= 0 ? 0 : (double)x.NetWorth / supply
                };
            })
            .ToList();

        return Ok(new
        {
            entries, total = balances.Count, supply
        });
    }

    /// <summary>
    ///     Gets a single user's holdings and recent ledger entries.
    /// </summary>
    /// <param name="guildId">The guild to look in.</param>
    /// <param name="userId">The user to inspect.</param>
    /// <returns>The user's wallet, bank, and transaction history.</returns>
    [HttpGet("balance/{userId}")]
    public async Task<IActionResult> GetBalance(ulong guildId, ulong userId)
    {
        var (wallet, bank) = await currencyService.GetBalancesAsync(userId, guildId);
        var transactions = await currencyService.GetTransactionsAsync(userId, guildId, 100);

        return Ok(new
        {
            userId,
            wallet,
            bank,
            netWorth = wallet + bank,
            transactions = transactions.Select(t => new
            {
                t.Id,
                t.Amount,
                t.Description,
                t.Category,
                t.Source,
                t.DateAdded
            })
        });
    }

    /// <summary>
    ///     Adds or removes currency from a user's wallet.
    /// </summary>
    /// <param name="guildId">The guild to adjust in.</param>
    /// <param name="request">The user, amount and reason.</param>
    /// <returns>The user's holdings after the adjustment.</returns>
    [HttpPost("balance")]
    public async Task<IActionResult> AdjustBalance(ulong guildId, [FromBody] AdjustBalanceRequest request)
    {
        if (request.Amount == 0)
            return BadRequest("Amount must be non-zero.");

        await currencyService.CreditAsync(request.UserId, request.Amount,
            string.IsNullOrWhiteSpace(request.Reason) ? "Dashboard adjustment" : request.Reason,
            CurrencyCategory.AdminAdjust, guildId, "dashboard");

        logger.LogInformation("Dashboard adjusted {UserId} in {GuildId} by {Amount}", request.UserId, guildId,
            request.Amount);

        var (wallet, bank) = await currencyService.GetBalancesAsync(request.UserId, guildId);

        return Ok(new
        {
            wallet, bank, netWorth = wallet + bank
        });
    }

    #endregion

    #region Setup

    /// <summary>
    ///     Gets the economy setup: currency emoji, daily reward, and whether the economy counts as set up.
    /// </summary>
    /// <param name="guildId">The guild to read.</param>
    /// <returns>The setup state.</returns>
    [HttpGet("setup")]
    public async Task<IActionResult> GetSetup(ulong guildId)
    {
        return Ok(await BuildSetupAsync(guildId));
    }

    /// <summary>
    ///     Applies a partial update to the economy setup.
    /// </summary>
    /// <param name="guildId">The guild to update.</param>
    /// <param name="request">The fields to change. Omitted fields are left alone.</param>
    /// <returns>The setup state after the update, or a 400 explaining what is wrong.</returns>
    [HttpPatch("setup")]
    public async Task<IActionResult> UpdateSetup(ulong guildId, [FromBody] EconomySetupRequest request)
    {
        if (request.CurrencyEmoji is not null && CurrencySetupRules.ValidateEmoji(request.CurrencyEmoji) is { } emojiError)
            return BadRequest(emojiError);

        var guildConfig = await guildSettings.GetGuildConfig(guildId);
        var amount = request.RewardAmount ?? guildConfig.RewardAmount;
        var cooldown = request.RewardTimeoutSeconds ?? guildConfig.RewardTimeoutSeconds;

        if ((request.RewardAmount.HasValue || request.RewardTimeoutSeconds.HasValue) &&
            CurrencySetupRules.ValidateReward(amount, cooldown) is { } rewardError)
            return BadRequest(rewardError);

        if (request.CurrencyEmoji is not null)
            guildConfig.CurrencyEmoji = request.CurrencyEmoji.Trim();
        guildConfig.RewardAmount = amount;
        guildConfig.RewardTimeoutSeconds = cooldown;
        await guildSettings.UpdateGuildConfig(guildId, guildConfig);

        if (request.SetupComplete.HasValue)
            await configService.UpdateAsync(guildId, config => config.SetupComplete = request.SetupComplete.Value);

        return Ok(await BuildSetupAsync(guildId));
    }

    private async Task<EconomySetupResponse> BuildSetupAsync(ulong guildId)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guildId);
        var economy = await configService.GetConfigAsync(guildId);

        await using var db = await dbFactory.CreateConnectionAsync();
        var configured = economy.SetupComplete || await db.TransactionHistories.AnyAsync(x => x.GuildId == guildId);

        return new EconomySetupResponse
        {
            CurrencyEmoji = guildConfig.CurrencyEmoji,
            RewardAmount = guildConfig.RewardAmount,
            RewardTimeoutSeconds = guildConfig.RewardTimeoutSeconds,
            SetupComplete = economy.SetupComplete,
            Configured = configured
        };
    }

    #endregion

    #region Configuration

    /// <summary>
    ///     Gets a guild's economy settings, creating defaults on first access.
    /// </summary>
    /// <param name="guildId">The guild to read.</param>
    /// <returns>The guild's economy configuration.</returns>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(ulong guildId)
    {
        return Ok(await configService.GetConfigAsync(guildId));
    }

    /// <summary>
    ///     Applies a partial update to a guild's economy settings.
    /// </summary>
    /// <param name="guildId">The guild to update.</param>
    /// <param name="request">The fields to change. Omitted fields are left alone.</param>
    /// <returns>The configuration after the update.</returns>
    [HttpPatch("config")]
    public async Task<IActionResult> UpdateConfig(ulong guildId, [FromBody] UpdateEconomyConfigRequest request)
    {
        var current = await configService.GetConfigAsync(guildId);

        (long Min, long Max)? Pair(long? min, long? max, long currentMin, long currentMax)
        {
            return min.HasValue || max.HasValue ? (min ?? currentMin, max ?? currentMax) : null;
        }

        var rangeError = CurrencySetupRules.ValidateRanges(
            Pair(request.WorkMinReward, request.WorkMaxReward, current.WorkMinReward, current.WorkMaxReward),
            Pair(request.CrimeMinReward, request.CrimeMaxReward, current.CrimeMinReward, current.CrimeMaxReward),
            Pair(request.CrimeFineMin, request.CrimeFineMax, current.CrimeFineMin, current.CrimeFineMax),
            Pair(request.MinBet, request.MaxBet, current.MinBet, current.MaxBet));

        if (rangeError is not null)
            return BadRequest(rangeError);

        var updated = await configService.UpdateAsync(guildId, config =>
        {
            if (request.GamblingEnabled.HasValue) config.GamblingEnabled = request.GamblingEnabled.Value;
            if (request.MinBet.HasValue) config.MinBet = Math.Max(1, request.MinBet.Value);
            if (request.MaxBet.HasValue) config.MaxBet = Math.Max(0, request.MaxBet.Value);
            if (request.PayoutMultiplier.HasValue)
                config.PayoutMultiplier = Math.Clamp(request.PayoutMultiplier.Value, 0.1, 5.0);
            if (request.GameCooldownSeconds.HasValue)
                config.GameCooldownSeconds = Math.Clamp(request.GameCooldownSeconds.Value, 0, 86400);
            if (request.LossLimitPerDay.HasValue) config.LossLimitPerDay = Math.Max(0, request.LossLimitPerDay.Value);

            if (request.PayEnabled.HasValue) config.PayEnabled = request.PayEnabled.Value;
            if (request.PayTaxPercent.HasValue)
                config.PayTaxPercent = Math.Clamp(request.PayTaxPercent.Value, 0, 100);
            if (request.PayCooldownSeconds.HasValue)
                config.PayCooldownSeconds = Math.Clamp(request.PayCooldownSeconds.Value, 0, 86400);
            if (request.PayMinimum.HasValue) config.PayMinimum = Math.Max(1, request.PayMinimum.Value);

            if (request.BankEnabled.HasValue) config.BankEnabled = request.BankEnabled.Value;
            if (request.BankCapacity.HasValue) config.BankCapacity = Math.Max(0, request.BankCapacity.Value);
            if (request.BankInterestPercent.HasValue)
                config.BankInterestPercent = Math.Clamp(request.BankInterestPercent.Value, 0, 100);
            if (request.BankInterestHours.HasValue)
                config.BankInterestHours = Math.Clamp(request.BankInterestHours.Value, 1, 720);

            if (request.RobEnabled.HasValue) config.RobEnabled = request.RobEnabled.Value;
            if (request.RobSuccessChance.HasValue)
                config.RobSuccessChance = Math.Clamp(request.RobSuccessChance.Value, 0, 100);
            if (request.RobMaxStealPercent.HasValue)
                config.RobMaxStealPercent = Math.Clamp(request.RobMaxStealPercent.Value, 1, 100);
            if (request.RobFinePercent.HasValue)
                config.RobFinePercent = Math.Clamp(request.RobFinePercent.Value, 0, 100);
            if (request.RobMinimumWallet.HasValue)
                config.RobMinimumWallet = Math.Max(0, request.RobMinimumWallet.Value);
            if (request.RobCooldownSeconds.HasValue)
                config.RobCooldownSeconds = Math.Clamp(request.RobCooldownSeconds.Value, 0, 86400);

            if (request.WorkEnabled.HasValue) config.WorkEnabled = request.WorkEnabled.Value;
            if (request.WorkMinReward.HasValue) config.WorkMinReward = Math.Max(0, request.WorkMinReward.Value);
            if (request.WorkMaxReward.HasValue) config.WorkMaxReward = Math.Max(0, request.WorkMaxReward.Value);
            if (request.WorkCooldownSeconds.HasValue)
                config.WorkCooldownSeconds = Math.Clamp(request.WorkCooldownSeconds.Value, 0, 86400);

            if (request.CrimeEnabled.HasValue) config.CrimeEnabled = request.CrimeEnabled.Value;
            if (request.CrimeMinReward.HasValue) config.CrimeMinReward = Math.Max(0, request.CrimeMinReward.Value);
            if (request.CrimeMaxReward.HasValue) config.CrimeMaxReward = Math.Max(0, request.CrimeMaxReward.Value);
            if (request.CrimeSuccessChance.HasValue)
                config.CrimeSuccessChance = Math.Clamp(request.CrimeSuccessChance.Value, 0, 100);
            if (request.CrimeFineMin.HasValue) config.CrimeFineMin = Math.Max(0, request.CrimeFineMin.Value);
            if (request.CrimeFineMax.HasValue) config.CrimeFineMax = Math.Max(0, request.CrimeFineMax.Value);
            if (request.CrimeCooldownSeconds.HasValue)
                config.CrimeCooldownSeconds = Math.Clamp(request.CrimeCooldownSeconds.Value, 0, 86400);

            if (request.DailyStreakEnabled.HasValue) config.DailyStreakEnabled = request.DailyStreakEnabled.Value;
            if (request.DailyStreakBonus.HasValue)
                config.DailyStreakBonus = Math.Max(0, request.DailyStreakBonus.Value);
            if (request.DailyStreakMaxBonus.HasValue)
                config.DailyStreakMaxBonus = Math.Max(0, request.DailyStreakMaxBonus.Value);
        });

        return Ok(updated);
    }

    /// <summary>
    ///     Restores a guild's economy settings to their defaults.
    /// </summary>
    /// <param name="guildId">The guild to reset.</param>
    /// <returns>The freshly defaulted configuration.</returns>
    [HttpPost("config/reset")]
    public async Task<IActionResult> ResetConfig(ulong guildId)
    {
        return Ok(await configService.ResetAsync(guildId));
    }

    #endregion

    #region Shop

    /// <summary>
    ///     Gets a guild's shop items, including hidden ones, with ownership and revenue totals.
    /// </summary>
    /// <param name="guildId">The guild to list.</param>
    /// <returns>Every shop item the guild has defined.</returns>
    [HttpGet("shop")]
    public async Task<IActionResult> GetShop(ulong guildId)
    {
        var items = await shopService.GetItemsAsync(guildId, true);
        var guild = client.GetGuild(guildId);
        var designNames = (await designs.GetDesignsAsync(guildId)).ToDictionary(d => d.Id, d => d.Name);

        await using var db = await dbFactory.CreateConnectionAsync();

        var totals = await db.UserInventoryItems
            .Where(x => x.GuildId == guildId)
            .GroupBy(x => x.ShopItemId)
            .Select(g => new
            {
                ShopItemId = g.Key, Owned = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.TotalPaid)
            })
            .ToListAsync();

        var byItem = totals.ToDictionary(x => x.ShopItemId);

        var response = items.Select(item =>
        {
            byItem.TryGetValue(item.Id, out var totalsForItem);

            return new ShopItemResponse
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                ItemType = item.ItemType,
                RoleId = item.RoleId,
                RoleName = item.RoleId.HasValue ? guild?.GetRole(item.RoleId.Value)?.Name : null,
                TextContent = item.TextContent,
                DesignId = item.DesignId,
                DesignName = item.DesignId.HasValue ? designNames.GetValueOrDefault(item.DesignId.Value) : null,
                Stock = item.Stock,
                MaxPerUser = item.MaxPerUser,
                RequiredRoleId = item.RequiredRoleId,
                RequiredRoleName = item.RequiredRoleId.HasValue
                    ? guild?.GetRole(item.RequiredRoleId.Value)?.Name
                    : null,
                Consumable = item.Consumable,
                BuyMessage = item.BuyMessage,
                UseMessage = item.UseMessage,
                UseOnBuy = item.UseOnBuy,
                Enabled = item.Enabled,
                SortOrder = item.SortOrder,
                Owned = totalsForItem?.Owned ?? 0,
                Revenue = totalsForItem?.Revenue ?? 0
            };
        }).ToList();

        return Ok(response);
    }

    /// <summary>
    ///     Creates a shop item.
    /// </summary>
    /// <param name="guildId">The guild to add to.</param>
    /// <param name="request">The item to create.</param>
    /// <returns>The created item, or a conflict if the name is taken.</returns>
    [HttpPost("shop")]
    public async Task<IActionResult> CreateShopItem(ulong guildId, [FromBody] ShopItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");
        if (request.Price < 0)
            return BadRequest("Price cannot be negative.");
        if (await ValidateItemTypeAsync(guildId, request) is { } typeError)
            return BadRequest(typeError);

        var isDesign = request.ItemType == (int)ShopItemType.Design;
        var created = await shopService.AddItemAsync(new ShopItem
        {
            GuildId = guildId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Price = request.Price,
            ItemType = request.ItemType,
            RoleId = request.RoleId,
            TextContent = request.TextContent,
            DesignId = isDesign ? request.DesignId : null,
            Stock = request.Stock < 0 ? -1 : request.Stock,
            MaxPerUser = isDesign ? 1 : Math.Max(0, request.MaxPerUser),
            RequiredRoleId = request.RequiredRoleId,
            Consumable = !isDesign && request.Consumable,
            BuyMessage = request.BuyMessage,
            UseMessage = request.UseMessage,
            UseOnBuy = !isDesign && request.UseOnBuy,
            Enabled = request.Enabled,
            SortOrder = request.SortOrder
        });

        return created is null ? Conflict($"An item named '{request.Name}' already exists.") : Ok(created);
    }

    /// <summary>
    ///     Replaces an existing shop item's settings.
    /// </summary>
    /// <param name="guildId">The guild the item belongs to.</param>
    /// <param name="name">The current name of the item.</param>
    /// <param name="request">The new settings.</param>
    /// <returns>The updated item, or not found if no such item exists.</returns>
    [HttpPut("shop/{name}")]
    public async Task<IActionResult> UpdateShopItem(ulong guildId, string name, [FromBody] ShopItemRequest request)
    {
        if (request.Price < 0)
            return BadRequest("Price cannot be negative.");
        if (await ValidateItemTypeAsync(guildId, request) is { } typeError)
            return BadRequest(typeError);

        var isDesign = request.ItemType == (int)ShopItemType.Design;
        var updated = await shopService.UpdateItemAsync(guildId, name, item =>
        {
            item.Name = string.IsNullOrWhiteSpace(request.Name) ? item.Name : request.Name.Trim();
            item.Description = request.Description;
            item.Price = request.Price;
            item.ItemType = request.ItemType;
            item.RoleId = request.RoleId;
            item.TextContent = request.TextContent;
            item.DesignId = isDesign ? request.DesignId : null;
            item.Stock = request.Stock < 0 ? -1 : request.Stock;
            item.MaxPerUser = isDesign ? 1 : Math.Max(0, request.MaxPerUser);
            item.RequiredRoleId = request.RequiredRoleId;
            item.Consumable = !isDesign && request.Consumable;
            item.BuyMessage = request.BuyMessage;
            item.UseMessage = request.UseMessage;
            item.UseOnBuy = !isDesign && request.UseOnBuy;
            item.Enabled = request.Enabled;
            item.SortOrder = request.SortOrder;
        });

        return updated is null ? NotFound($"No item named '{name}'.") : Ok(updated);
    }

    /// <summary>
    ///     Deletes a shop item and every inventory entry referencing it.
    /// </summary>
    /// <param name="guildId">The guild the item belongs to.</param>
    /// <param name="name">The name of the item to delete.</param>
    /// <returns>No content on success, or not found if no such item exists.</returns>
    [HttpDelete("shop/{name}")]
    public async Task<IActionResult> DeleteShopItem(ulong guildId, string name)
    {
        return await shopService.RemoveItemAsync(guildId, name)
            ? NoContent()
            : NotFound($"No item named '{name}'.");
    }

    /// <summary>
    ///     Gets a user's shop inventory.
    /// </summary>
    /// <param name="guildId">The guild to look in.</param>
    /// <param name="userId">The owning user.</param>
    /// <returns>The items the user owns and how many of each.</returns>
    [HttpGet("inventory/{userId}")]
    public async Task<IActionResult> GetInventory(ulong guildId, ulong userId)
    {
        var entries = await shopService.GetInventoryAsync(guildId, userId);

        return Ok(entries.Select(x => new
        {
            x.Item.Id,
            x.Item.Name,
            x.Item.Description,
            x.Item.ItemType,
            x.Item.Consumable,
            x.Quantity,
            x.TotalPaid
        }));
    }

    /// <summary>
    ///     Gives a member copies of a shop item, or creates the item hidden when it does not exist yet.
    /// </summary>
    /// <param name="guildId">The guild the inventory belongs to.</param>
    /// <param name="userId">The receiving user.</param>
    /// <param name="request">The item and amount.</param>
    /// <param name="spawn">Whether to create the item out of thin air instead of using shop stock.</param>
    /// <returns>No content on success, or an error when the item is missing or out of stock.</returns>
    [HttpPost("inventory/{userId}/give")]
    public async Task<IActionResult> GiveInventoryItem(ulong guildId, ulong userId,
        [FromBody] InventoryAdjustRequest request, [FromQuery] bool spawn = false)
    {
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.ItemName))
            return BadRequest("An item name and a positive amount are required.");

        if (spawn)
        {
            var spawned = await shopService.SpawnItemAsync(guildId, userId, request.ItemName.Trim(), request.Amount);
            return spawned is null ? Conflict("The item could not be created.") : NoContent();
        }

        var item = await shopService.GetItemAsync(guildId, request.ItemName.Trim());

        if (item is null)
            return NotFound($"No item named '{request.ItemName}'.");

        return await shopService.GrantItemAsync(guildId, userId, item, request.Amount, true)
            ? NoContent()
            : Conflict("The item is out of stock.");
    }

    /// <summary>
    ///     Takes copies of an item out of a member's inventory.
    /// </summary>
    /// <param name="guildId">The guild the inventory belongs to.</param>
    /// <param name="userId">The owning user.</param>
    /// <param name="request">The item and amount.</param>
    /// <returns>How many copies were taken, or not found if the item does not exist.</returns>
    [HttpPost("inventory/{userId}/take")]
    public async Task<IActionResult> TakeInventoryItem(ulong guildId, ulong userId,
        [FromBody] InventoryAdjustRequest request)
    {
        var item = await shopService.GetItemAsync(guildId, request.ItemName?.Trim() ?? string.Empty);

        if (item is null)
            return NotFound($"No item named '{request.ItemName}'.");

        return Ok(new
        {
            Taken = await shopService.TakeItemAsync(guildId, userId, item, request.Amount)
        });
    }

    private async Task<string?> ValidateItemTypeAsync(ulong guildId, ShopItemRequest request)
    {
        if (!Enum.IsDefined((ShopItemType)request.ItemType))
            return "Item type must be 0, 1, 2, 3 or 4.";
        if (request.ItemType != (int)ShopItemType.Design)
            return null;
        if (request.DesignId is not { } designId)
            return "Pick the design this item grants.";

        return await designs.GetDesignAsync(guildId, designId) is null ? "That design doesn't exist." : null;
    }

    #endregion

    #region Boosts

    /// <summary>
    ///     Gets a guild's scheduled earning boosts.
    /// </summary>
    /// <param name="guildId">The guild to list.</param>
    /// <returns>The guild's boosts.</returns>
    [HttpGet("boosts")]
    public async Task<IActionResult> GetBoosts(ulong guildId)
    {
        return Ok(await boostService.GetBoostsAsync(guildId));
    }

    /// <summary>
    ///     Creates a scheduled earning boost.
    /// </summary>
    /// <param name="guildId">The guild to add to.</param>
    /// <param name="request">The boost to create.</param>
    /// <returns>The created boost, or a conflict if the name is taken.</returns>
    [HttpPost("boosts")]
    public async Task<IActionResult> CreateBoost(ulong guildId, [FromBody] EconomyBoostRequest request)
    {
        var problem = ValidateBoost(request);

        if (problem is not null)
            return BadRequest(problem);

        var created = await boostService.AddBoostAsync(new EconomyBoost
        {
            GuildId = guildId,
            Name = request.Name.Trim(),
            PercentChange = request.PercentChange,
            Commands = request.Commands.Trim().ToLowerInvariant(),
            ScheduleType = request.ScheduleType,
            StartsAt = request.StartsAt,
            DurationMinutes = request.DurationMinutes,
            Enabled = request.Enabled
        });

        return created is null ? Conflict($"A boost named '{request.Name}' already exists.") : Ok(created);
    }

    /// <summary>
    ///     Replaces a scheduled earning boost's settings.
    /// </summary>
    /// <param name="guildId">The guild the boost belongs to.</param>
    /// <param name="name">The current name of the boost.</param>
    /// <param name="request">The new settings.</param>
    /// <returns>The updated boost, or not found if no such boost exists.</returns>
    [HttpPut("boosts/{name}")]
    public async Task<IActionResult> UpdateBoost(ulong guildId, string name, [FromBody] EconomyBoostRequest request)
    {
        var problem = ValidateBoost(request);

        if (problem is not null)
            return BadRequest(problem);

        var updated = await boostService.UpdateBoostAsync(guildId, name, boost =>
        {
            boost.Name = request.Name.Trim();
            boost.PercentChange = request.PercentChange;
            boost.Commands = request.Commands.Trim().ToLowerInvariant();
            boost.ScheduleType = request.ScheduleType;
            boost.StartsAt = request.StartsAt;
            boost.DurationMinutes = request.DurationMinutes;
            boost.Enabled = request.Enabled;
        });

        return updated is null ? NotFound($"No boost named '{name}'.") : Ok(updated);
    }

    /// <summary>
    ///     Deletes a scheduled earning boost.
    /// </summary>
    /// <param name="guildId">The guild the boost belongs to.</param>
    /// <param name="name">The name of the boost.</param>
    /// <returns>No content on success, or not found if no such boost exists.</returns>
    [HttpDelete("boosts/{name}")]
    public async Task<IActionResult> DeleteBoost(ulong guildId, string name)
    {
        return await boostService.RemoveBoostAsync(guildId, name)
            ? NoContent()
            : NotFound($"No boost named '{name}'.");
    }

    private static string? ValidateBoost(EconomyBoostRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Name is required.";
        if (request.PercentChange is < -100 or > 1000 || request.PercentChange == 0)
            return "Percent must be between -100 and 1000, and not 0.";
        if (request.DurationMinutes is < 1 or > 525600)
            return "Duration must be between 1 minute and 365 days.";
        if (request.ScheduleType is < 0 or > 3)
            return "Schedule must be 0, 1, 2 or 3.";

        var commands = request.Commands ?? string.Empty;
        request.Commands = commands;

        var unknown = commands
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(x => !EconomyBoostService.BoostableCommands.Contains(x, StringComparer.OrdinalIgnoreCase));

        return unknown is null ? null : $"Unknown command '{unknown}'.";
    }

    #endregion
}