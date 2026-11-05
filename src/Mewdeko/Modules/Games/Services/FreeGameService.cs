using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
///     A game that is free to keep right now.
/// </summary>
/// <param name="Store">The store name.</param>
/// <param name="Key">The store's own identifier for the game.</param>
/// <param name="Title">The game's title.</param>
/// <param name="Url">The store page.</param>
/// <param name="ImageUrl">A cover image, if the store has one.</param>
/// <param name="EndsAt">When the offer ends, if the store says.</param>
public record FreeGame(string Store, string Key, string Title, string Url, string? ImageUrl, DateTime? EndsAt);

/// <summary>
///     Posts alerts when games become free on the Epic Games Store, Steam and GOG.
/// </summary>
public partial class FreeGameService : INService, IReadyExecutor
{
    /// <summary>
    ///     The Epic Games Store name used in alerts and settings.
    /// </summary>
    public const string Epic = "Epic Games Store";

    /// <summary>
    ///     The Steam name used in alerts and settings.
    /// </summary>
    public const string Steam = "Steam";

    /// <summary>
    ///     The GOG name used in alerts and settings.
    /// </summary>
    public const string Gog = "GOG";

    private const string SeedKey = "__seeded__";
    private const string UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/124.0 Safari/537.36";

    private const string EpicUrl =
        "https://store-site-backend-static.ak.epicgames.com/freeGamesPromotions?locale=en-US&country=US&allowCountries=US";

    private const string SteamUrl =
        "https://store.steampowered.com/search/results/?query&start=0&count=50&maxprice=free&specials=1&json=1&cc=us&l=english";

    private const string GogUrl =
        "https://catalog.gog.com/v1/catalog?limit=48&price=between%3A0%2C0&discounted=eq%3Atrue&order=desc%3Atrending&productType=in%3Agame%2Cpack&page=1&countryCode=US&locale=en-US&currencyCode=USD";

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IHttpClientFactory httpFactory;
    private readonly ILogger<FreeGameService> logger;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FreeGameService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="httpFactory">The HTTP client factory.</param>
    /// <param name="strings">The generated bot strings.</param>
    /// <param name="logger">The logger instance.</param>
    public FreeGameService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        IHttpClientFactory httpFactory, GeneratedBotStrings strings, ILogger<FreeGameService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.httpFactory = httpFactory;
        this.strings = strings;
        this.logger = logger;
    }

    /// <summary>
    ///     Starts the loop that checks the stores.
    /// </summary>
    public Task OnReadyAsync()
    {
        _ = Task.Run(RunLoop);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Gets the alert channels of a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The alert channels.</returns>
    public async Task<List<FreeGameAlert>> GetAlertsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.FreeGameAlerts.Where(x => x.GuildId == guildId).OrderBy(x => x.Id).ToListAsync();
    }

    /// <summary>
    ///     Adds or changes an alert channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel alerts are posted in.</param>
    /// <param name="roleId">The role to ping, or null for none.</param>
    /// <param name="epic">Whether Epic games are announced, or null to keep the current choice.</param>
    /// <param name="steam">Whether Steam games are announced, or null to keep the current choice.</param>
    /// <param name="gog">Whether GOG games are announced, or null to keep the current choice.</param>
    /// <param name="changeRole">Whether the role should be changed, so null can clear it.</param>
    /// <returns>The saved alert.</returns>
    public async Task<FreeGameAlert> SaveAlertAsync(ulong guildId, ulong channelId, ulong? roleId, bool? epic = null,
        bool? steam = null, bool? gog = null, bool changeRole = true)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var alert = await db.FreeGameAlerts.FirstOrDefaultAsync(x => x.GuildId == guildId && x.ChannelId == channelId);
        if (alert is null)
        {
            alert = new FreeGameAlert
            {
                GuildId = guildId, ChannelId = channelId, RoleId = roleId, Epic = epic ?? true,
                Steam = steam ?? true, Gog = gog ?? true, DateAdded = DateTime.UtcNow
            };
            alert.Id = await db.InsertWithInt32IdentityAsync(alert);
            return alert;
        }

        if (changeRole)
            alert.RoleId = roleId;
        alert.Epic = epic ?? alert.Epic;
        alert.Steam = steam ?? alert.Steam;
        alert.Gog = gog ?? alert.Gog;
        await db.UpdateAsync(alert);
        return alert;
    }

    /// <summary>
    ///     Removes an alert channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>True when the channel had alerts.</returns>
    public async Task<bool> RemoveAlertAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.FreeGameAlerts.Where(x => x.GuildId == guildId && x.ChannelId == channelId).DeleteAsync() > 0;
    }

    /// <summary>
    ///     Gets every game that is free right now, from the stores that answer.
    /// </summary>
    /// <returns>The free games.</returns>
    public async Task<List<FreeGame>> GetCurrentAsync()
    {
        var all = await FetchAllAsync();
        return all.Values.Where(x => x is not null).SelectMany(x => x!).ToList();
    }

    private async Task RunLoop()
    {
        await Task.Delay(TimeSpan.FromMinutes(1));
        while (true)
        {
            try
            {
                await CheckAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Free game check failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(30));
        }
    }

    private async Task CheckAsync()
    {
        var fetched = await FetchAllAsync();
        await using var db = await dbFactory.CreateConnectionAsync();
        var known = (await db.FreeGameAnnounced.ToListAsync()).Select(x => (x.Store, x.GameKey)).ToHashSet();

        foreach (var (store, games) in fetched)
        {
            if (games is null)
                continue;

            if (!known.Any(x => x.Store == store))
            {
                await db.InsertAsync(new FreeGameAnnounced
                {
                    Store = store, GameKey = SeedKey, DateAdded = DateTime.UtcNow
                });
                foreach (var game in games)
                    await db.InsertAsync(new FreeGameAnnounced
                    {
                        Store = store, GameKey = game.Key, DateAdded = DateTime.UtcNow
                    });
                continue;
            }

            foreach (var game in games.Where(x => !known.Contains((x.Store, x.Key))))
            {
                await AnnounceAsync(game);
                await db.InsertAsync(new FreeGameAnnounced
                {
                    Store = game.Store, GameKey = game.Key, DateAdded = DateTime.UtcNow
                });
            }
        }
    }

    private async Task AnnounceAsync(FreeGame game)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var alerts = await db.FreeGameAlerts.ToListAsync();
        foreach (var alert in alerts.Where(x => game.Store switch
                 {
                     Epic => x.Epic,
                     Steam => x.Steam,
                     _ => x.Gog
                 }))
        {
            try
            {
                if (client.GetChannel(alert.ChannelId) is not SocketTextChannel channel)
                    continue;

                var guildId = channel.Guild.Id;
                var embed = new EmbedBuilder()
                    .WithOkColor(guildId)
                    .WithTitle(strings.FreeGameTitle(guildId, game.Title, game.Store))
                    .WithUrl(game.Url);
                if (game.ImageUrl is not null)
                    embed.WithImageUrl(game.ImageUrl);
                if (game.EndsAt is { } ends)
                    embed.WithDescription(strings.FreeGameEnds(guildId,
                        new DateTimeOffset(DateTime.SpecifyKind(ends, DateTimeKind.Utc)).ToUnixTimeSeconds()));

                var role = alert.RoleId is { } roleId ? channel.Guild.GetRole(roleId) : null;
                if (role is null)
                {
                    await channel.SendMessageAsync(embed: embed.Build());
                    continue;
                }

                await RolePinger.SendAsync(channel.Guild, channel, role, () => channel.SendMessageAsync(role.Mention,
                    embed: embed.Build(), allowedMentions: RolePinger.MentionsFor(role.Id)));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not post a free game alert in {ChannelId}", alert.ChannelId);
            }
        }
    }

    private async Task<Dictionary<string, List<FreeGame>?>> FetchAllAsync()
    {
        var epic = FetchAsync(FetchEpicAsync);
        var steam = FetchAsync(FetchSteamAsync);
        var gog = FetchAsync(FetchGogAsync);
        return new Dictionary<string, List<FreeGame>?>
        {
            [Epic] = await epic,
            [Steam] = await steam,
            [Gog] = await gog
        };
    }

    private async Task<List<FreeGame>?> FetchAsync(Func<HttpClient, Task<List<FreeGame>>> fetch)
    {
        try
        {
            using var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            return await fetch(http);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Free game fetch failed");
            return null;
        }
    }

    private static async Task<List<FreeGame>> FetchEpicAsync(HttpClient http)
    {
        using var doc = JsonDocument.Parse(await http.GetStringAsync(EpicUrl));
        var games = new List<FreeGame>();
        var elements = doc.RootElement.GetProperty("data").GetProperty("Catalog").GetProperty("searchStore")
            .GetProperty("elements");
        var now = DateTime.UtcNow;

        foreach (var element in elements.EnumerateArray())
        {
            if (!element.TryGetProperty("promotions", out var promotions) || promotions.ValueKind != JsonValueKind.Object)
                continue;
            if (element.GetProperty("price").GetProperty("totalPrice").GetProperty("originalPrice").GetInt64() == 0)
                continue;

            DateTime? ends = null;
            var live = false;
            foreach (var group in promotions.GetProperty("promotionalOffers").EnumerateArray())
            foreach (var offer in group.GetProperty("promotionalOffers").EnumerateArray())
            {
                var start = offer.GetProperty("startDate").GetDateTime().ToUniversalTime();
                var end = offer.GetProperty("endDate").GetDateTime().ToUniversalTime();
                if (offer.GetProperty("discountSetting").GetProperty("discountPercentage").GetInt32() != 0 ||
                    start > now || end < now)
                    continue;
                live = true;
                ends = end;
            }

            if (!live)
                continue;

            var slug = element.TryGetProperty("offerMappings", out var mappings) &&
                       mappings.ValueKind == JsonValueKind.Array && mappings.GetArrayLength() > 0
                ? mappings[0].GetProperty("pageSlug").GetString()
                : element.GetProperty("productSlug").GetString();
            if (string.IsNullOrEmpty(slug))
                continue;

            string? image = null;
            foreach (var key in element.GetProperty("keyImages").EnumerateArray())
            {
                var type = key.GetProperty("type").GetString();
                if (type == "OfferImageWide" || (image is null && type == "Thumbnail"))
                    image = key.GetProperty("url").GetString();
            }

            games.Add(new FreeGame(Epic, element.GetProperty("id").GetString()!,
                element.GetProperty("title").GetString()!, $"https://store.epicgames.com/en-US/p/{slug}", image, ends));
        }

        return games;
    }

    private static async Task<List<FreeGame>> FetchSteamAsync(HttpClient http)
    {
        using var doc = JsonDocument.Parse(await http.GetStringAsync(SteamUrl));
        var games = new List<FreeGame>();
        foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            var match = SteamAppId().Match(item.GetProperty("logo").GetString() ?? "");
            if (!match.Success)
                continue;

            var id = match.Groups[1].Value;
            games.Add(new FreeGame(Steam, id, item.GetProperty("name").GetString()!,
                $"https://store.steampowered.com/app/{id}",
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{id}/header.jpg", null));
        }

        return games;
    }

    private static async Task<List<FreeGame>> FetchGogAsync(HttpClient http)
    {
        using var doc = JsonDocument.Parse(await http.GetStringAsync(GogUrl));
        var games = new List<FreeGame>();
        foreach (var product in doc.RootElement.GetProperty("products").EnumerateArray())
        {
            var amount = product.GetProperty("price").GetProperty("finalMoney").GetProperty("amount").GetString();
            if (!decimal.TryParse(amount, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var price) || price != 0)
                continue;

            games.Add(new FreeGame(Gog, product.GetProperty("id").GetString()!,
                product.GetProperty("title").GetString()!, product.GetProperty("storeLink").GetString()!,
                product.TryGetProperty("coverHorizontal", out var cover) ? cover.GetString() : null, null));
        }

        return games;
    }

    [GeneratedRegex(@"/apps/(\d+)/")]
    private static partial Regex SteamAppId();
}
