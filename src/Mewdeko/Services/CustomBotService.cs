using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.OwnerOnly.Common;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Impl;

namespace Mewdeko.Services;

/// <summary>
///     A guild a custom bot is in.
/// </summary>
/// <param name="Id">The guild id.</param>
/// <param name="Name">The guild name.</param>
/// <param name="Icon">The icon URL, or null.</param>
/// <param name="Assigned">Whether the guild's work is handled by this custom bot.</param>
public sealed record CustomBotGuildView(
    string Id,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? Icon,
    bool Assigned);

/// <summary>
///     A custom bot as the dashboard shows it.
/// </summary>
public sealed class CustomBotView
{
    /// <summary>Gets the BotInstance id.</summary>
    public int InstanceId { get; init; }

    /// <summary>Gets the bot's Discord id.</summary>
    public string BotId { get; init; } = "";

    /// <summary>Gets the bot's name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Gets the avatar URL.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Avatar { get; init; }

    /// <summary>Gets running, stopped, starting, error or expired.</summary>
    public string Status { get; init; } = "stopped";

    /// <summary>Gets the guilds the bot is in.</summary>
    public List<CustomBotGuildView> Guilds { get; init; } = [];

    /// <summary>Gets when the bot was created.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public DateTime? CreatedAt { get; init; }

    /// <summary>Gets when its slot expires, or null for never.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Gets the invite URL.</summary>
    public string InviteUrl { get; init; } = "";

    /// <summary>Gets the owning user, filled in for owner listings only.</summary>
    public string? OwnerUserId { get; init; }
}

/// <summary>
///     A slot as the dashboard shows it.
/// </summary>
/// <param name="Id">The slot id.</param>
/// <param name="Source">kofi or manual.</param>
/// <param name="ExpiresAt">When it expires, or null for never.</param>
/// <param name="InstanceId">The custom bot using it, or null.</param>
/// <param name="UserId">The owning user, filled in for owner listings only.</param>
/// <param name="KofiEmail">The purchase email, filled in for owner listings only.</param>
public sealed record CustomBotSlotView(
    int Id,
    string Source,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    DateTime? ExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    int? InstanceId,
    string? UserId = null,
    string? KofiEmail = null);

/// <summary>
///     The outcome of a Ko-fi webhook delivery.
/// </summary>
public enum KofiResult
{
    /// <summary>The verification token did not match or is not configured.</summary>
    Rejected,

    /// <summary>The payload could not be read.</summary>
    Invalid,

    /// <summary>The transaction was already processed.</summary>
    Duplicate,

    /// <summary>The payment was stored and granted nothing.</summary>
    Stored,

    /// <summary>The payment was stored and granted or extended a slot.</summary>
    Granted
}

/// <summary>
///     Runs paid custom bots: Ko-fi purchases grant slots, a buyer submits a bot token, and a container running
///     this bot with that token starts against the shared database.
/// </summary>
public class CustomBotService : INService, IReadyExecutor
{
    private const string DiscordApi = "https://discord.com/api/v10";
    private const string NoSlot = "no free slot";
    private static readonly TimeSpan GuildCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ExpiryInterval = TimeSpan.FromMinutes(10);

    private static readonly string[] StrippedKeys =
    [
        "Token", "KofiVerificationToken", "KofiShopItemCodes", "CustomBotImage", "CustomBotNetwork",
        "CustomBotDataPath", "CustomBotHostDataPath", "CustomBotPortStart", "CustomBotPortEnd",
        "CustomBotEncryptionKey", "CustomBotGraceDays", "CustomBotDaysPerPayment"
    ];

    private readonly BotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly DockerService docker;
    private readonly IHttpClientFactory factory;

    private readonly ConcurrentDictionary<int, (DateTime At, List<(ulong Id, string Name, string? Icon)> Guilds)>
        guildCache = new();

    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly ILogger<CustomBotService> logger;
    private readonly GuildOwnershipService ownership;
    private readonly DiscordShardedClient client;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CustomBotService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="creds">The bot credentials and custom bot settings.</param>
    /// <param name="docker">The Docker Engine client.</param>
    /// <param name="ownership">Guild ownership.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="factory">The HTTP client factory.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="logger">The logger.</param>
    public CustomBotService(IDataConnectionFactory dbFactory, BotCredentials creds, DockerService docker,
        GuildOwnershipService ownership, DiscordShardedClient client, IHttpClientFactory factory,
        EventHandler eventHandler, ILogger<CustomBotService> logger)
    {
        this.dbFactory = dbFactory;
        this.creds = creds;
        this.docker = docker;
        this.ownership = ownership;
        this.client = client;
        this.factory = factory;
        this.logger = logger;
        eventHandler.Subscribe("LeftGuild", "CustomBotService", OnLeftGuild);
    }

    /// <summary>
    ///     Gets whether this instance runs the custom bot machinery. Only the master does.
    /// </summary>
    public bool Enabled => ownership.HandlesGlobalWork;

    /// <summary>
    ///     Starts the expiry loop on the master instance.
    /// </summary>
    public Task OnReadyAsync()
    {
        if (!ownership.HandlesGlobalWork)
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(ExpiryInterval);
            do
            {
                try
                {
                    await RunExpiryAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Custom bot expiry pass failed");
                }
            } while (await timer.WaitForNextTickAsync());
        });
        return Task.CompletedTask;
    }

    private async Task OnLeftGuild(SocketGuild guild)
    {
        if (ownership.HandlesGlobalWork || !ownership.IsMine(guild.Id))
            return;

        try
        {
            await ownership.ReleaseAsync(guild.Id);
            logger.LogInformation("Released guild {GuildId} after this custom bot left it", guild.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to release guild {GuildId} after leaving it", guild.Id);
        }
    }

    private byte[] Key
    {
        get
        {
            var secret = !string.IsNullOrWhiteSpace(creds.CustomBotEncryptionKey)
                ? creds.CustomBotEncryptionKey
                : $"{creds.ApiKey}:{creds.JwtSecret}";
            return CustomBotCrypto.DeriveKey(secret);
        }
    }

    /// <summary>
    ///     Verifies and applies a Ko-fi webhook delivery.
    /// </summary>
    /// <param name="dataJson">The JSON in the <c>data</c> form field.</param>
    public async Task<KofiResult> HandleKofiAsync(string? dataJson)
    {
        var payload = KofiPayload.TryParse(dataJson);
        if (payload is null)
            return KofiResult.Invalid;

        if (!KofiPayload.VerifyToken(payload.VerificationToken, creds.KofiVerificationToken))
            return KofiResult.Rejected;

        var (extends, newSlots) = payload.Grant(creds.KofiShopItemCodes);
        var days = Math.Max(1, creds.CustomBotDaysPerPayment);
        var email = payload.Email.ToLowerInvariant();
        int? restoreSlot = null;

        await lifecycle.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            if (await db.KofiPayments.AnyAsync(x => x.KofiTransactionId == payload.TransactionId))
                return KofiResult.Duplicate;

            var knownUser = await db.CustomBotSlots
                .Where(x => x.UserId != null && x.KofiEmail != null && x.KofiEmail.ToLower() == email)
                .Select(x => x.UserId)
                .FirstOrDefaultAsync();

            int? slotId = null;
            if (extends)
            {
                var slot = await FindMembershipSlotAsync(db, email);
                if (slot is null)
                {
                    slotId = await db.InsertWithInt32IdentityAsync(new CustomBotSlot
                    {
                        UserId = knownUser,
                        KofiEmail = payload.Email,
                        Source = CustomBotExpiry.KofiSource,
                        KofiTransactionId = payload.TransactionId,
                        ExpiresAt = CustomBotExpiry.Extend(null, payload.PaidAt, days)
                    });
                }
                else
                {
                    slot.ExpiresAt = CustomBotExpiry.Extend(slot.ExpiresAt, payload.PaidAt, days);
                    await db.UpdateAsync(slot);
                    slotId = slot.Id;
                    if (slot.InstanceId != null && slot.StoppedAt != null)
                        restoreSlot = slot.Id;
                }
            }
            else
            {
                for (var i = 0; i < newSlots; i++)
                {
                    var id = await db.InsertWithInt32IdentityAsync(new CustomBotSlot
                    {
                        UserId = knownUser,
                        KofiEmail = payload.Email,
                        Source = CustomBotExpiry.KofiSource,
                        KofiTransactionId = i == 0 ? payload.TransactionId : null,
                        ExpiresAt = CustomBotExpiry.Extend(null, payload.PaidAt, days)
                    });
                    slotId ??= id;
                }
            }

            await db.InsertAsync(new KofiPayment
            {
                KofiTransactionId = payload.TransactionId,
                Email = payload.Email,
                Type = payload.Type,
                Amount = payload.Amount,
                IsSubscriptionPayment = payload.IsSubscriptionPayment,
                IsFirstSubscriptionPayment = payload.IsFirstSubscriptionPayment,
                SlotId = slotId,
                PaidAt = payload.PaidAt
            });
        }
        catch (Exception ex) when (ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return KofiResult.Duplicate;
        }
        finally
        {
            lifecycle.Release();
        }

        if (restoreSlot is { } restore)
            await RestoreAsync(restore);

        return extends || newSlots > 0 ? KofiResult.Granted : KofiResult.Stored;
    }

    private static async Task<CustomBotSlot?> FindMembershipSlotAsync(MewdekoDb db, string email)
    {
        var slotId = await db.KofiPayments
            .Where(x => x.Email.ToLower() == email && x.SlotId != null &&
                        (x.IsSubscriptionPayment || x.Type == "Subscription"))
            .OrderByDescending(x => x.PaidAt)
            .Select(x => x.SlotId)
            .FirstOrDefaultAsync();

        return slotId is { } id ? await db.CustomBotSlots.FirstOrDefaultAsync(x => x.Id == id) : null;
    }

    /// <summary>
    ///     Links a purchase to a Discord user when the email and transaction id match a stored payment.
    /// </summary>
    /// <param name="userId">The user claiming it.</param>
    /// <param name="email">The purchase email.</param>
    /// <param name="transactionId">The Ko-fi transaction id.</param>
    /// <returns>Null on success, otherwise the error message.</returns>
    public async Task<string?> ClaimAsync(ulong userId, string? email, string? transactionId)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(transactionId))
            return "no matching purchase";

        var lowered = email.Trim().ToLowerInvariant();
        var txn = transactionId.Trim();
        await using var db = await dbFactory.CreateConnectionAsync();
        var payment = await db.KofiPayments.FirstOrDefaultAsync(x =>
            x.KofiTransactionId == txn && x.Email.ToLower() == lowered && x.SlotId != null);
        if (payment?.SlotId is not { } slotId)
            return "no matching purchase";

        var slot = await db.CustomBotSlots.FirstOrDefaultAsync(x => x.Id == slotId);
        if (slot is null)
            return "no matching purchase";

        if (slot.UserId != null && slot.UserId != userId)
            return "already claimed";

        await db.CustomBotSlots
            .Where(x => x.UserId == null && x.KofiEmail != null && x.KofiEmail.ToLower() == lowered)
            .Set(x => x.UserId, userId)
            .UpdateAsync();
        return null;
    }

    /// <summary>
    ///     Grants a manual slot to a user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="days">How many days it lasts, or null for never expiring.</param>
    public async Task<CustomBotSlotView> GrantManualSlotAsync(ulong userId, int? days)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var slot = new CustomBotSlot
        {
            UserId = userId,
            Source = CustomBotExpiry.ManualSource,
            ExpiresAt = days is { } d ? DateTime.UtcNow.AddDays(d) : null
        };
        slot.Id = await db.InsertWithInt32IdentityAsync(slot);
        return ToView(slot);
    }

    /// <summary>
    ///     Gets a user's slots and bots.
    /// </summary>
    /// <param name="userId">The user.</param>
    public async Task<(List<CustomBotSlotView> Slots, List<CustomBotView> Bots)> GetUserAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var slots = await db.CustomBotSlots.Where(x => x.UserId == userId).OrderBy(x => x.Id).ToListAsync();
        var instances = await db.BotInstances.Where(x => x.IsCustom && x.OwnerUserId == userId).ToListAsync();
        var bots = new List<CustomBotView>();
        foreach (var instance in instances)
            bots.Add(await BuildViewAsync(db, instance, true));

        return (slots.Select(ToView).ToList(), bots);
    }

    /// <summary>
    ///     Gets every slot and custom bot, for owners.
    /// </summary>
    public async Task<(List<CustomBotSlotView> Slots, List<CustomBotView> Bots)> GetAllAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var slots = await db.CustomBotSlots.OrderBy(x => x.Id).ToListAsync();
        var instances = await db.BotInstances.Where(x => x.IsCustom).ToListAsync();
        var bots = new List<CustomBotView>();
        foreach (var instance in instances)
            bots.Add(await BuildViewAsync(db, instance, false));

        return (slots.Select(s => ToView(s) with
        {
            UserId = s.UserId?.ToString(), KofiEmail = s.KofiEmail
        }).ToList(), bots);
    }

    /// <summary>
    ///     Finds which custom bot runs a guild.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <returns>The instance id, name and avatar, or nulls when the main bot runs it.</returns>
    public async Task<(int? InstanceId, string? Name, string? Avatar)> GetGuildBotAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await (from o in db.GuildOwnerships
            join i in db.BotInstances on o.InstanceId equals i.Id
            where o.GuildId == guildId && i.IsCustom
            select i).FirstOrDefaultAsync();
        return row is null ? (null, null, null) : (row.Id, row.BotName, NullIfEmpty(row.BotAvatar));
    }

    private static CustomBotSlotView ToView(CustomBotSlot slot)
    {
        return new CustomBotSlotView(slot.Id, slot.Source, slot.ExpiresAt, slot.InstanceId);
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private async Task<CustomBotView> BuildViewAsync(MewdekoDb db, BotInstance instance, bool withGuilds)
    {
        var slot = await db.CustomBotSlots.FirstOrDefaultAsync(x => x.InstanceId == instance.Id);
        var secret = await db.CustomBotSecrets.FirstOrDefaultAsync(x => x.InstanceId == instance.Id);
        var status = await StatusAsync(slot, secret);

        var guilds = new List<CustomBotGuildView>();
        if (withGuilds && secret is not null)
        {
            var assigned = (await db.GuildOwnerships.Where(x => x.InstanceId == instance.Id)
                .Select(x => x.GuildId).ToListAsync()).ToHashSet();
            var token = CustomBotCrypto.Decrypt(secret.EncryptedToken, Key);
            if (token is not null)
            {
                var list = await GetBotGuildsAsync(instance.Id, token, false);
                guilds = list.Select(g => new CustomBotGuildView(g.Id.ToString(), g.Name, g.Icon,
                    assigned.Contains(g.Id))).ToList();
            }
        }

        return new CustomBotView
        {
            InstanceId = instance.Id,
            BotId = instance.BotId.ToString(),
            Name = instance.BotName,
            Avatar = NullIfEmpty(instance.BotAvatar),
            Status = status,
            Guilds = guilds,
            CreatedAt = instance.DateAdded,
            ExpiresAt = slot?.ExpiresAt,
            InviteUrl = CustomBotInvite.Url(instance.BotId),
            OwnerUserId = instance.OwnerUserId?.ToString()
        };
    }

    private bool IsExpired(CustomBotSlot? slot)
    {
        return slot is not null && (slot.StoppedAt != null || CustomBotExpiry.ShouldStop(slot.ExpiresAt,
            slot.Source, creds.CustomBotGraceDays, creds.CustomBotDaysPerPayment, DateTime.UtcNow));
    }

    private async Task<string> StatusAsync(CustomBotSlot? slot, CustomBotSecret? secret)
    {
        if (IsExpired(slot))
            return "expired";

        if (secret is null)
            return "error";

        var container = await docker.FindContainerAsync(secret.ContainerName);
        return container?.State switch
        {
            "running" => "running",
            "created" or "restarting" => "starting",
            "exited" or "paused" => "stopped",
            _ => "error"
        };
    }

    private HttpClient CreateDiscordClient(string token)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", token);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DiscordBot (https://mewdeko.tech, 1.0)");
        return http;
    }

    /// <summary>
    ///     Checks a bot token with Discord.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The bot's id, name and avatar URL, or the error message.</returns>
    public async Task<((ulong Id, string Name, string Avatar)? Bot, string? Error)> ValidateTokenAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
            return (null, "invalid token");

        try
        {
            using var http = CreateDiscordClient(token.Trim());
            using var response = await http.GetAsync($"{DiscordApi}/users/@me");
            if (!response.IsSuccessStatusCode)
                return (null, "invalid token");

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (!root.TryGetProperty("bot", out var bot) || bot.ValueKind != JsonValueKind.True)
                return (null, "not a bot");

            if (!ulong.TryParse(root.GetProperty("id").GetString(), out var id))
                return (null, "invalid token");

            var name = root.TryGetProperty("username", out var n) ? n.GetString() ?? "" : "";
            var avatar = root.TryGetProperty("avatar", out var a) && a.ValueKind == JsonValueKind.String
                ? $"https://cdn.discordapp.com/avatars/{id}/{a.GetString()}.png"
                : "";
            return ((id, name, avatar), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Could not validate a custom bot token");
            return (null, "invalid token");
        }
    }

    private async Task<List<(ulong Id, string Name, string? Icon)>> GetBotGuildsAsync(int instanceId, string token,
        bool fresh)
    {
        if (!fresh && guildCache.TryGetValue(instanceId, out var cached) &&
            DateTime.UtcNow - cached.At < GuildCacheTtl)
            return cached.Guilds;

        var result = new List<(ulong, string, string?)>();
        try
        {
            using var http = CreateDiscordClient(token);
            ulong after = 0;
            while (true)
            {
                using var response = await http.GetAsync($"{DiscordApi}/users/@me/guilds?limit=200&after={after}");
                if (!response.IsSuccessStatusCode)
                    break;

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var count = 0;
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    count++;
                    if (!ulong.TryParse(item.GetProperty("id").GetString(), out var id))
                        continue;

                    after = Math.Max(after, id);
                    var icon = item.TryGetProperty("icon", out var i) && i.ValueKind == JsonValueKind.String
                        ? $"https://cdn.discordapp.com/icons/{id}/{i.GetString()}.png"
                        : null;
                    result.Add((id, item.GetProperty("name").GetString() ?? "", icon));
                }

                if (count < 200)
                    break;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Could not list guilds for custom bot {Instance}", instanceId);
        }

        guildCache[instanceId] = (DateTime.UtcNow, result);
        return result;
    }

    /// <summary>
    ///     Creates a custom bot from a token in one of the user's free slots and starts its container.
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <param name="token">The bot token.</param>
    /// <param name="slotId">The slot to use.</param>
    public async Task<(CustomBotView? Bot, string? Error)> CreateBotAsync(ulong userId, string? token, int slotId)
    {
        var (bot, error) = await ValidateTokenAsync(token);
        if (bot is null)
            return (null, error);

        token = token!.Trim();
        await lifecycle.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var slot = await db.CustomBotSlots.FirstOrDefaultAsync(x => x.Id == slotId && x.UserId == userId);
            if (slot is null || slot.InstanceId != null || IsExpired(slot))
                return (null, NoSlot);

            if (await db.BotInstances.AnyAsync(x => x.BotId == bot.Value.Id))
                return (null, "already registered");

            var setup = await ResolveSetupAsync();
            if (setup.Error is not null)
                return (null, setup.Error);

            var used = await db.CustomBotSecrets.Select(x => x.ApiPort).ToListAsync();
            var port = CustomBotPorts.Pick(creds.CustomBotPortStart, creds.CustomBotPortEnd, used);
            if (port is null)
                return (null, "no free port, contact the owner");

            var instanceId = await db.InsertWithInt32IdentityAsync(new BotInstance
            {
                BotName = bot.Value.Name,
                BotAvatar = bot.Value.Avatar,
                BotId = bot.Value.Id,
                Host = "pending",
                Port = port.Value,
                IsActive = true,
                IsCustom = true,
                OwnerUserId = userId,
                DateAdded = DateTime.UtcNow,
                LastStatusUpdate = DateTime.UtcNow
            });

            var containerName = ContainerNameFor(instanceId);
            var launch = await LaunchAsync(setup, instanceId, userId, containerName, port.Value, token);
            if (launch is not null)
            {
                await db.BotInstances.Where(x => x.Id == instanceId).DeleteAsync();
                return (null, launch);
            }

            await db.BotInstances.Where(x => x.Id == instanceId)
                .Set(x => x.Host, containerName)
                .UpdateAsync();
            await db.InsertAsync(new CustomBotSecret
            {
                InstanceId = instanceId,
                EncryptedToken = CustomBotCrypto.Encrypt(token, Key),
                ApiPort = port.Value,
                ContainerName = containerName
            });
            await db.CustomBotSlots.Where(x => x.Id == slotId)
                .Set(x => x.InstanceId, instanceId)
                .Set(x => x.StoppedAt, (DateTime?)null)
                .UpdateAsync();

            var instance = await db.BotInstances.FirstAsync(x => x.Id == instanceId);
            return (await BuildViewAsync(db, instance, true), null);
        }
        finally
        {
            lifecycle.Release();
        }
    }

    private static string ContainerNameFor(int instanceId)
    {
        return $"mewdeko-custom-{instanceId}";
    }

    private sealed record Setup(string Image, string Network, string DataPath, string HostDataPath, string? Error);

    private async Task<Setup> ResolveSetupAsync()
    {
        var overview = await docker.GetOverviewAsync();
        if (overview.Availability != DockerAvailability.Available)
            return new Setup("", "", "", "", overview.Message ?? "Docker is not available");

        var self = overview.Containers.FirstOrDefault(c => c.IsSelf);
        JsonElement? inspected = self is null ? null : await docker.InspectContainerAsync(self.Id);

        var image = creds.CustomBotImage;
        if (string.IsNullOrWhiteSpace(image))
            image = self is null
                ? "sylveondeko/mewdeko:nightly"
                : inspected?.GetProperty("Config").GetProperty("Image").GetString() ?? self.Image;

        var network = creds.CustomBotNetwork;
        if (string.IsNullOrWhiteSpace(network) && inspected is { } info &&
            info.TryGetProperty("NetworkSettings", out var settings) &&
            settings.TryGetProperty("Networks", out var networks))
            network = networks.EnumerateObject().Select(x => x.Name)
                .FirstOrDefault(x => x is not ("bridge" or "host" or "none")) ?? "";

        if (string.IsNullOrWhiteSpace(network))
            return new Setup(image, "", "", "", "no Docker network, set CustomBotNetwork");

        var dataPath = string.IsNullOrWhiteSpace(creds.CustomBotDataPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "custombots")
            : creds.CustomBotDataPath;
        var hostPath = creds.CustomBotHostDataPath;
        if (string.IsNullOrWhiteSpace(hostPath))
        {
            var mounts = new List<(string Source, string Destination)>();
            if (inspected is { } i && i.TryGetProperty("Mounts", out var m) && m.ValueKind == JsonValueKind.Array)
            {
                foreach (var mount in m.EnumerateArray())
                {
                    if (mount.TryGetProperty("Source", out var s) && mount.TryGetProperty("Destination", out var d))
                        mounts.Add((s.GetString() ?? "", d.GetString() ?? ""));
                }
            }

            hostPath = MapHostPath(dataPath, mounts);
        }

        return new Setup(image, network, dataPath, hostPath, null);
    }

    /// <summary>
    ///     Translates a path inside this container to the path the Docker host sees, using the container's mounts.
    ///     A path under no mount is returned as is.
    /// </summary>
    /// <param name="dataPath">The path inside this container.</param>
    /// <param name="mounts">The container's bind mount sources and destinations.</param>
    public static string MapHostPath(string dataPath, IEnumerable<(string Source, string Destination)> mounts)
    {
        var best = mounts
            .Where(m => m.Destination.Length > 0 && (dataPath == m.Destination ||
                                                    dataPath.StartsWith(m.Destination.TrimEnd('/') + "/",
                                                        StringComparison.Ordinal)))
            .OrderByDescending(m => m.Destination.Length)
            .Cast<(string Source, string Destination)?>()
            .FirstOrDefault();

        if (best is not { } found)
            return dataPath;

        return found.Source.TrimEnd('/') + dataPath[found.Destination.TrimEnd('/').Length..];
    }

    private string CredentialsDirectory(Setup setup, string containerName)
    {
        return Path.Combine(setup.DataPath, containerName);
    }

    private JsonObject BuildChildCredentials(string token, int port, string containerName)
    {
        JsonObject? node = null;
        var path = Path.Combine(Directory.GetCurrentDirectory(), "credentials.json");
        try
        {
            if (File.Exists(path))
                node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "Could not read credentials.json to copy for a custom bot");
        }

        node ??= new JsonObject();
        node["PsqlConnectionString"] = creds.PsqlConnectionString;
        node["RedisConnections"] = creds.RedisConnections;
        node["ApiKey"] = creds.ApiKey;
        node["JwtSecret"] = creds.JwtSecret;
        node["OwnerIds"] = new JsonArray(creds.OwnerIds.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
        if (!string.IsNullOrWhiteSpace(creds.DiscordClientId))
            node["DiscordClientId"] = creds.DiscordClientId;
        if (!string.IsNullOrWhiteSpace(creds.DiscordClientSecret))
            node["DiscordClientSecret"] = creds.DiscordClientSecret;
        if (!string.IsNullOrWhiteSpace(creds.DashboardUrl))
            node["DashboardUrl"] = creds.DashboardUrl;

        foreach (var key in StrippedKeys)
            node.Remove(key);

        node["Token"] = token;
        node["IsMasterInstance"] = false;
        node["IsApiEnabled"] = true;
        node["ApiPort"] = port;
        node["InstanceApiHost"] = containerName;
        node["TotalShards"] = 1;
        return node;
    }

    private void WriteCredentials(Setup setup, string containerName, string token, int port)
    {
        var directory = CredentialsDirectory(setup, containerName);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "credentials.json");
        var json = BuildChildCredentials(token, port, containerName)
            .ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });
        File.WriteAllText(file, json);
    }

    private async Task<string?> LaunchAsync(Setup setup, int instanceId, ulong userId, string containerName, int port,
        string token)
    {
        try
        {
            WriteCredentials(setup, containerName, token, port);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not write credentials for custom bot {Instance}", instanceId);
            return "could not write the bot's credentials, check CustomBotDataPath";
        }

        var pulled = await docker.EnsureImageAsync(setup.Image);
        if (!pulled.Success)
            return pulled.Message ?? "could not get the bot image";

        var hostFile = Path.Combine(setup.HostDataPath, containerName, "credentials.json");
        var spec = new
        {
            Image = setup.Image,
            Labels = new Dictionary<string, string>
            {
                ["mewdeko.custombot"] = instanceId.ToString(),
                ["mewdeko.owner"] = userId.ToString()
            },
            HostConfig = new
            {
                Binds = new[]
                {
                    $"{hostFile}:/app/credentials.json:ro", $"{containerName}-data:/app/data"
                },
                RestartPolicy = new
                {
                    Name = "unless-stopped"
                },
                NetworkMode = setup.Network
            },
            NetworkingConfig = new
            {
                EndpointsConfig = new Dictionary<string, object>
                {
                    [setup.Network] = new
                    {
                        Aliases = new[]
                        {
                            containerName
                        }
                    }
                }
            }
        };

        await docker.RemoveContainerAsync(containerName);
        var (id, error) = await docker.CreateContainerAsync(containerName, spec);
        if (id is null)
            return error ?? "could not create the container";

        var started = await docker.RunActionAsync(new DockerContainerInfo
        {
            Id = id
        }, "start");
        if (!started.Success)
        {
            await docker.RemoveContainerAsync(id);
            return started.Message ?? "could not start the container";
        }

        return null;
    }

    private async Task<(BotInstance? Instance, CustomBotSecret? Secret, CustomBotSlot? Slot)> LoadOwnedAsync(
        MewdekoDb db, ulong userId, int instanceId)
    {
        var instance = await db.BotInstances.FirstOrDefaultAsync(x =>
            x.Id == instanceId && x.IsCustom && x.OwnerUserId == userId);
        if (instance is null)
            return (null, null, null);

        var secret = await db.CustomBotSecrets.FirstOrDefaultAsync(x => x.InstanceId == instanceId);
        var slot = await db.CustomBotSlots.FirstOrDefaultAsync(x => x.InstanceId == instanceId);
        return (instance, secret, slot);
    }

    /// <summary>
    ///     Starts, stops or restarts a user's bot.
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <param name="instanceId">The BotInstance id.</param>
    /// <param name="action">start, stop or restart.</param>
    /// <returns>Null on success, otherwise the error message.</returns>
    public async Task<string?> ControlAsync(ulong userId, int instanceId, string action)
    {
        if (action is not ("start" or "stop" or "restart"))
            return "unknown action";

        await using var db = await dbFactory.CreateConnectionAsync();
        var (instance, secret, slot) = await LoadOwnedAsync(db, userId, instanceId);
        if (instance is null || secret is null)
            return "not found";

        if (action != "stop" && IsExpired(slot))
            return "this bot's slot has expired";

        var container = await docker.FindContainerAsync(secret.ContainerName);
        if (container is null)
            return "container is missing";

        var result = await docker.RunActionAsync(container, action);
        return result.Success ? null : result.Message ?? "docker refused";
    }

    /// <summary>
    ///     Removes a user's bot: releases its guilds, deletes the container and secret, and frees the slot.
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <param name="instanceId">The BotInstance id.</param>
    /// <returns>Null on success, otherwise the error message.</returns>
    public async Task<string?> RemoveAsync(ulong userId, int instanceId)
    {
        await lifecycle.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var (instance, _, _) = await LoadOwnedAsync(db, userId, instanceId);
            if (instance is null)
                return "not found";

            return await RemoveCoreAsync(db, instanceId);
        }
        finally
        {
            lifecycle.Release();
        }
    }

    private async Task<string?> RemoveCoreAsync(MewdekoDb db, int instanceId)
    {
        await ReleaseAllGuildsAsync(db, instanceId);

        var secret = await db.CustomBotSecrets.FirstOrDefaultAsync(x => x.InstanceId == instanceId);
        var name = secret?.ContainerName ?? ContainerNameFor(instanceId);
        var removed = await docker.RemoveContainerAsync(name);
        if (!removed.Success)
            return removed.Message ?? "could not remove the container";

        await docker.RemoveVolumeAsync($"{name}-data");
        try
        {
            var setup = await ResolveSetupAsync();
            var directory = CredentialsDirectory(setup, name);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete credentials folder for custom bot {Instance}", instanceId);
        }

        await db.CustomBotSlots.Where(x => x.InstanceId == instanceId)
            .Set(x => x.InstanceId, (int?)null)
            .Set(x => x.StoppedAt, (DateTime?)null)
            .UpdateAsync();
        await db.CustomBotSecrets.Where(x => x.InstanceId == instanceId).DeleteAsync();
        await db.BotInstances.Where(x => x.Id == instanceId).DeleteAsync();
        guildCache.TryRemove(instanceId, out _);
        return null;
    }

    private async Task ReleaseAllGuildsAsync(MewdekoDb db, int instanceId)
    {
        var guilds = await db.GuildOwnerships.Where(x => x.InstanceId == instanceId).Select(x => x.GuildId)
            .ToListAsync();
        foreach (var guild in guilds)
            await ownership.ReleaseAsync(guild);
    }

    /// <summary>
    ///     Replaces a bot's token and recreates its container.
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <param name="instanceId">The BotInstance id.</param>
    /// <param name="token">The new token.</param>
    /// <returns>Null on success, otherwise the error message.</returns>
    public async Task<string?> ReplaceTokenAsync(ulong userId, int instanceId, string? token)
    {
        var (bot, error) = await ValidateTokenAsync(token);
        if (bot is null)
            return error;

        token = token!.Trim();
        await lifecycle.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var (instance, secret, slot) = await LoadOwnedAsync(db, userId, instanceId);
            if (instance is null || secret is null)
                return "not found";

            if (IsExpired(slot))
                return "this bot's slot has expired";

            if (await db.BotInstances.AnyAsync(x => x.BotId == bot.Value.Id && x.Id != instanceId))
                return "already registered";

            var setup = await ResolveSetupAsync();
            if (setup.Error is not null)
                return setup.Error;

            if (instance.BotId != bot.Value.Id)
                await ReleaseAllGuildsAsync(db, instanceId);

            var launch = await LaunchAsync(setup, instanceId, userId, secret.ContainerName, secret.ApiPort, token);
            if (launch is not null)
                return launch;

            await db.CustomBotSecrets.Where(x => x.InstanceId == instanceId)
                .Set(x => x.EncryptedToken, CustomBotCrypto.Encrypt(token, Key))
                .UpdateAsync();
            await db.BotInstances.Where(x => x.Id == instanceId)
                .Set(x => x.BotId, bot.Value.Id)
                .Set(x => x.BotName, bot.Value.Name)
                .Set(x => x.BotAvatar, bot.Value.Avatar)
                .UpdateAsync();
            guildCache.TryRemove(instanceId, out _);
            return null;
        }
        finally
        {
            lifecycle.Release();
        }
    }

    /// <summary>
    ///     Reads recent container logs for the owner with secrets masked.
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <param name="instanceId">The BotInstance id.</param>
    /// <param name="tail">How many lines.</param>
    /// <returns>The lines, or null when the bot is not the user's or has no container.</returns>
    public async Task<List<string>?> GetLogsAsync(ulong userId, int instanceId, int tail)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var (instance, secret, _) = await LoadOwnedAsync(db, userId, instanceId);
        if (instance is null || secret is null)
            return null;

        var container = await docker.FindContainerAsync(secret.ContainerName);
        if (container is null)
            return [];

        var chunk = await docker.GetLogsAsync(container, Math.Clamp(tail, 1, 500), null);
        if (chunk is null)
            return [];

        var secrets = new[]
        {
            CustomBotCrypto.Decrypt(secret.EncryptedToken, Key), creds.ApiKey, creds.JwtSecret,
            creds.DiscordClientSecret, creds.PsqlConnectionString, creds.RedisConnections,
            creds.CustomBotEncryptionKey, creds.KofiVerificationToken
        };
        return chunk.Lines.Select(l => CustomBotRedactor.Redact(l.Text, secrets)).ToList();
    }

    /// <summary>
    ///     Gives a guild to a user's custom bot. The user has to manage the guild and the bot has to be in it.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="instanceId">The BotInstance id.</param>
    /// <param name="guildId">The guild id.</param>
    /// <returns>Null on success, otherwise the error message.</returns>
    public async Task<string?> AssignGuildAsync(ulong userId, int instanceId, ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var (instance, secret, slot) = await LoadOwnedAsync(db, userId, instanceId);
        if (instance is null || secret is null)
            return "not found";

        if (IsExpired(slot))
            return "this bot's slot has expired";

        if (!CanManageGuild(userId, guildId))
            return "you need Manage Server in that server";

        var token = CustomBotCrypto.Decrypt(secret.EncryptedToken, Key);
        if (token is null)
            return "stored token cannot be read";

        var guilds = await GetBotGuildsAsync(instanceId, token, true);
        if (guilds.All(g => g.Id != guildId))
            return "the bot is not in that server, invite it first";

        await ownership.AssignAsync(guildId, instanceId);
        return null;
    }

    /// <summary>
    ///     Returns a guild to the main bot.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="instanceId">The BotInstance id.</param>
    /// <param name="guildId">The guild id.</param>
    /// <returns>Null on success, otherwise the error message.</returns>
    public async Task<string?> ReleaseGuildAsync(ulong userId, int instanceId, ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var (instance, _, _) = await LoadOwnedAsync(db, userId, instanceId);
        if (instance is null)
            return "not found";

        if (!await db.GuildOwnerships.AnyAsync(x => x.GuildId == guildId && x.InstanceId == instanceId))
            return "that server is not assigned to this bot";

        await ownership.ReleaseAsync(guildId);
        return null;
    }

    private bool CanManageGuild(ulong userId, ulong guildId)
    {
        if (creds.IsOwner(userId))
            return true;

        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);
        if (guild is null || user is null)
            return false;

        return guild.OwnerId == userId || user.GuildPermissions.Administrator || user.GuildPermissions.ManageGuild;
    }

    /// <summary>
    ///     Stops bots whose slot ran out, deletes ones stopped long enough, and restores renewed ones.
    /// </summary>
    public async Task RunExpiryAsync()
    {
        await lifecycle.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var slots = await db.CustomBotSlots.Where(x => x.InstanceId != null).ToListAsync();
            var now = DateTime.UtcNow;
            foreach (var slot in slots)
            {
                var instanceId = slot.InstanceId!.Value;
                var due = CustomBotExpiry.ShouldStop(slot.ExpiresAt, slot.Source, creds.CustomBotGraceDays,
                    creds.CustomBotDaysPerPayment, now);

                if (slot.StoppedAt != null && !due)
                {
                    await RestoreCoreAsync(db, slot);
                }
                else if (slot.StoppedAt != null && CustomBotExpiry.ShouldRemove(slot.StoppedAt, now))
                {
                    logger.LogInformation("Removing custom bot {Instance} after its retention period", instanceId);
                    await RemoveCoreAsync(db, instanceId);
                }
                else if (slot.StoppedAt == null && due)
                {
                    await StopForExpiryAsync(db, slot);
                }
            }
        }
        finally
        {
            lifecycle.Release();
        }
    }

    private async Task StopForExpiryAsync(MewdekoDb db, CustomBotSlot slot)
    {
        var instanceId = slot.InstanceId!.Value;
        logger.LogInformation("Stopping custom bot {Instance}, its slot expired", instanceId);
        await ReleaseAllGuildsAsync(db, instanceId);

        var secret = await db.CustomBotSecrets.FirstOrDefaultAsync(x => x.InstanceId == instanceId);
        var container = secret is null ? null : await docker.FindContainerAsync(secret.ContainerName);
        if (container is not null)
            await docker.RunActionAsync(container, "stop");

        await db.CustomBotSlots.Where(x => x.Id == slot.Id)
            .Set(x => x.StoppedAt, DateTime.UtcNow)
            .UpdateAsync();
    }

    private async Task RestoreAsync(int slotId)
    {
        await lifecycle.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var slot = await db.CustomBotSlots.FirstOrDefaultAsync(x => x.Id == slotId);
            if (slot?.InstanceId != null && slot.StoppedAt != null)
                await RestoreCoreAsync(db, slot);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not restore custom bot for slot {Slot}", slotId);
        }
        finally
        {
            lifecycle.Release();
        }
    }

    private async Task RestoreCoreAsync(MewdekoDb db, CustomBotSlot slot)
    {
        var instanceId = slot.InstanceId!.Value;
        logger.LogInformation("Restoring custom bot {Instance}, its slot was renewed", instanceId);
        var secret = await db.CustomBotSecrets.FirstOrDefaultAsync(x => x.InstanceId == instanceId);
        var container = secret is null ? null : await docker.FindContainerAsync(secret.ContainerName);
        if (container is not null)
            await docker.RunActionAsync(container, "start");

        await db.CustomBotSlots.Where(x => x.Id == slot.Id)
            .Set(x => x.StoppedAt, (DateTime?)null)
            .UpdateAsync();
    }
}
