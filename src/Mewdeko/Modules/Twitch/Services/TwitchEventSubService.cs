using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Twitch.Common;

namespace Mewdeko.Modules.Twitch.Services;

/// <summary>Manages cloud-chatbot EventSub webhook subscriptions and notifications.</summary>
public class TwitchEventSubService : INService, IReadyExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IBotCredentials credentials;
    private readonly IDataConnectionFactory dbFactory;
    private readonly DiscordShardedClient discordClient;
    private readonly ILogger<TwitchEventSubService> logger;
    private readonly ConcurrentDictionary<string, DateTime> processedMessages = new();
    private readonly TwitchApiClient twitchApiClient;
    private readonly TwitchService twitchService;

    /// <summary>Initializes the cloud-chatbot EventSub service.</summary>
    public TwitchEventSubService(
        IBotCredentials credentials,
        IDataConnectionFactory dbFactory,
        DiscordShardedClient discordClient,
        ILogger<TwitchEventSubService> logger,
        TwitchApiClient twitchApiClient,
        TwitchService twitchService)
    {
        this.credentials = credentials;
        this.dbFactory = dbFactory;
        this.discordClient = discordClient;
        this.logger = logger;
        this.twitchApiClient = twitchApiClient;
        this.twitchService = twitchService;
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        if (!IsConfigured())
        {
            logger.LogInformation("Twitch cloud EventSub disabled: dashboard URL or signing secret is not configured");
            return;
        }

        for (var attempt = 0; attempt < 12; attempt++)
        {
            if (discordClient.CurrentUser is not null)
            {
                await ReconcileSubscriptionsAsync();
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        logger.LogWarning("Twitch EventSub could not start because the Discord bot identity is unavailable");
    }

    /// <summary>Creates webhook subscriptions for every enabled, authorized Twitch channel.</summary>
    public async Task ReconcileSubscriptionsAsync()
    {
        if (!IsConfigured()) return;
        if (!TryGetCallbackUrl(out var callbackUrl))
        {
            logger.LogWarning("Cannot reconcile Twitch EventSub before the Discord bot identity is available");
            return;
        }

        var appToken = await twitchApiClient.GetAppAccessTokenAsync(
            credentials.TwitchClientId, credentials.TwitchClientSecret);
        if (string.IsNullOrWhiteSpace(appToken)) return;

        await using var db = await dbFactory.CreateConnectionAsync();
        var bot = await db.TwitchBotAccounts.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        if (bot is null)
        {
            logger.LogInformation("Twitch cloud EventSub disabled: no bot account is authorized");
            return;
        }

        var configs = await db.TwitchGuildConfigs
            .Where(x => x.Enabled && x.UseEventSub && x.TwitchUserId != null)
            .ToListAsync();

        foreach (var config in configs)
        {
            await SubscribeAsync(db, appToken, callbackUrl, config, "channel.chat.message",
                new Dictionary<string, string>
                {
                    ["broadcaster_user_id"] = config.TwitchUserId!, ["user_id"] = bot.TwitchUserId
                });
            await SubscribeAsync(db, appToken, callbackUrl, config, "channel.subscribe", BroadcasterCondition(config));
            await SubscribeAsync(db, appToken, callbackUrl, config, "channel.subscription.message",
                BroadcasterCondition(config));
            await SubscribeAsync(db, appToken, callbackUrl, config, "channel.raid", new Dictionary<string, string>
            {
                ["to_broadcaster_user_id"] = config.TwitchUserId!
            });

            if (await db.TwitchChannelAuthorizations.AnyAsync(x => x.GuildId == config.GuildId))
                await SubscribeAsync(db, appToken, callbackUrl, config,
                    "channel.channel_points_custom_reward_redemption.add", BroadcasterCondition(config));
        }
    }

    /// <summary>Verifies a Twitch webhook signature and rejects stale replay attempts.</summary>
    public bool VerifySignature(string messageId, string timestamp, string signature, string body)
    {
        if (!DateTimeOffset.TryParse(timestamp, out var sentAt) ||
            DateTimeOffset.UtcNow - sentAt > TimeSpan.FromMinutes(10) ||
            sentAt - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(1))
            return false;

        var bytes = Encoding.UTF8.GetBytes(messageId + timestamp + body);
        var secret = Encoding.UTF8.GetBytes(credentials.TwitchEventSubSecret);
        var expected = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(secret, bytes)).ToLowerInvariant();
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(signature);
        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    /// <summary>Registers a webhook message ID and returns false for an already-processed delivery.</summary>
    public bool TryRegisterMessage(string messageId)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        foreach (var item in processedMessages.Where(x => x.Value < cutoff))
            processedMessages.TryRemove(item.Key, out _);
        return processedMessages.TryAdd(messageId, DateTime.UtcNow);
    }

    /// <summary>Gets Twitch's verification challenge and the subscription ID it belongs to.</summary>
    public static (string? Challenge, string? SubscriptionId) GetVerificationInfo(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var challenge = root.TryGetProperty("challenge", out var challengeElement)
            ? challengeElement.GetString()
            : null;
        var subscriptionId = root.TryGetProperty("subscription", out var subscription) &&
                             subscription.TryGetProperty("id", out var idElement)
            ? idElement.GetString()
            : null;
        return (challenge, subscriptionId);
    }

    /// <summary>
    ///     Marks a webhook subscription as enabled once Twitch's verification challenge succeeds.
    ///     Without this, the stored status stays <c>webhook_callback_verification_pending</c> forever,
    ///     so the next reconcile treats an already-live subscription as stale and churns it.
    /// </summary>
    public async Task MarkSubscriptionEnabledAsync(string subscriptionId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var existing = await db.TwitchEventSubSubscriptions
            .FirstOrDefaultAsync(x => x.TwitchSubscriptionId == subscriptionId);
        if (existing is null) return;

        existing.Status = "enabled";
        existing.LastUpdatedAt = DateTime.UtcNow;
        await db.UpdateAsync(existing);
    }

    /// <summary>Processes a verified EventSub notification.</summary>
    public async Task ProcessNotificationAsync(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var subscription = root.GetProperty("subscription");
            var type = subscription.GetProperty("type").GetString() ?? "";
            var evt = root.GetProperty("event");

            switch (type)
            {
                case "channel.chat.message":
                    var chat = evt.Deserialize<TwitchEventSubChatMessageEvent>(JsonOptions);
                    if (chat is not null) await twitchService.HandleEventSubChatMessageAsync(chat);
                    break;
                case "channel.channel_points_custom_reward_redemption.add":
                    await twitchService.HandleChannelPointRedemptionAsync(ParseRedemption(evt));
                    break;
                case "channel.subscribe":
                case "channel.subscription.message":
                    await twitchService.HandleEventSubSubscriptionAsync(
                        GetString(evt, "broadcaster_user_login"), GetString(evt, "user_login"),
                        GetString(evt, "user_name"), GetString(evt, "tier"),
                        evt.TryGetProperty("is_gift", out var isGift) && isGift.GetBoolean());
                    break;
                case "channel.raid":
                    await twitchService.HandleEventSubRaidAsync(
                        GetString(evt, "to_broadcaster_user_login"), GetString(evt, "from_broadcaster_user_name"),
                        evt.TryGetProperty("viewers", out var viewers) ? viewers.GetInt32() : 0);
                    break;
            }

            await RecordNotificationAsync(evt, type, body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process Twitch EventSub notification");
        }
    }

    /// <summary>Marks a verified EventSub revocation in persistent subscription state.</summary>
    public async Task ProcessRevocationAsync(string body)
    {
        using var document = JsonDocument.Parse(body);
        var subscription = document.RootElement.GetProperty("subscription");
        var id = GetString(subscription, "id");
        var status = GetString(subscription, "status");
        await using var db = await dbFactory.CreateConnectionAsync();
        var existing = await db.TwitchEventSubSubscriptions
            .FirstOrDefaultAsync(x => x.TwitchSubscriptionId == id);
        if (existing is not null)
        {
            existing.Status = status;
            existing.LastUpdatedAt = DateTime.UtcNow;
            await db.UpdateAsync(existing);
        }

        logger.LogWarning("Twitch EventSub subscription {SubscriptionId} revoked with status {Status}", id, status);
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(credentials.TwitchClientId) &&
               !string.IsNullOrWhiteSpace(credentials.TwitchClientSecret) &&
               !string.IsNullOrWhiteSpace(credentials.DashboardUrl) &&
               !string.IsNullOrWhiteSpace(credentials.TwitchEventSubSecret) &&
               Uri.TryCreate(credentials.DashboardUrl, UriKind.Absolute, out var callback) &&
               callback.Scheme == Uri.UriSchemeHttps &&
               credentials.TwitchEventSubSecret.Length is >= 10 and <= 100;
    }

    private async Task SubscribeAsync(MewdekoDb db, string appToken, string callbackUrl, TwitchGuildConfig config,
        string type, Dictionary<string, string> condition)
    {
        var existingSubscriptions = await db.TwitchEventSubSubscriptions.Where(x =>
            x.GuildId == config.GuildId && x.SubscriptionType == type &&
            x.TransportMethod == "webhook" &&
            (x.Status == "enabled" || x.Status == "webhook_callback_verification_pending")).ToListAsync();
        var pendingCutoff = DateTime.UtcNow.AddMinutes(-10);
        if (existingSubscriptions.Any(x => x.CallbackUrl == callbackUrl &&
                                           (x.Status == "enabled" ||
                                            x.Status == "webhook_callback_verification_pending" &&
                                            x.LastUpdatedAt >= pendingCutoff)))
            return;

        foreach (var stale in existingSubscriptions)
        {
            if (!await twitchApiClient.DeleteEventSubSubscriptionAsync(
                    credentials.TwitchClientId, appToken, stale.TwitchSubscriptionId))
                return;
            stale.Status = "callback_changed";
            stale.LastUpdatedAt = DateTime.UtcNow;
            await db.UpdateAsync(stale);
        }

        var subscription = await twitchApiClient.CreateWebhookSubscriptionAsync(
            credentials.TwitchClientId, appToken, type, "1", condition,
            callbackUrl, credentials.TwitchEventSubSecret);
        if (subscription is null) return;

        var stored = await db.TwitchEventSubSubscriptions
            .FirstOrDefaultAsync(x => x.TwitchSubscriptionId == subscription.Id);
        if (stored is null)
        {
            await db.InsertAsync(new TwitchEventSubSubscription
            {
                GuildId = config.GuildId,
                TwitchSubscriptionId = subscription.Id,
                SubscriptionType = subscription.Type,
                Version = subscription.Version,
                Status = subscription.Status,
                TransportMethod = "webhook",
                SessionId = null,
                CallbackUrl = callbackUrl,
                Cost = subscription.Cost,
                DateAdded = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            stored.Status = subscription.Status;
            stored.TransportMethod = "webhook";
            stored.SessionId = null;
            stored.CallbackUrl = callbackUrl;
            stored.LastUpdatedAt = DateTime.UtcNow;
            await db.UpdateAsync(stored);
        }

        logger.LogInformation("Created Twitch {Type} webhook subscription for guild {GuildId}", type, config.GuildId);
    }

    private static Dictionary<string, string> BroadcasterCondition(TwitchGuildConfig config)
    {
        return new Dictionary<string, string>
        {
            ["broadcaster_user_id"] = config.TwitchUserId!
        };
    }

    private bool TryGetCallbackUrl(out string callbackUrl)
    {
        callbackUrl = "";
        if (discordClient.CurrentUser is null) return false;
        callbackUrl = $"{credentials.DashboardUrl.TrimEnd('/')}/api/twitch/eventsub/{discordClient.CurrentUser.Id}";
        return true;
    }

    private static TwitchChannelPointRedemptionArgs ParseRedemption(JsonElement evt)
    {
        return new TwitchChannelPointRedemptionArgs
        {
            BroadcasterUserLogin = GetString(evt, "broadcaster_user_login"),
            UserLogin = GetString(evt, "user_login"),
            UserName = GetString(evt, "user_name"),
            RewardTitle = evt.TryGetProperty("reward", out var reward) ? GetString(reward, "title") : "",
            UserInput = GetString(evt, "user_input")
        };
    }

    private async Task RecordNotificationAsync(JsonElement evt, string eventType, string rawPayload)
    {
        var broadcasterId = GetString(evt, "broadcaster_user_id");
        if (string.IsNullOrWhiteSpace(broadcasterId))
            broadcasterId = GetString(evt, "to_broadcaster_user_id");
        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.TwitchGuildConfigs.FirstOrDefaultAsync(x => x.TwitchUserId == broadcasterId);
        if (config is not null)
            await twitchService.RecordEventHistoryAsync(config.GuildId, eventType, "eventsub",
                "Received EventSub notification", true, rawPayload: rawPayload);
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) ? value.GetString() ?? "" : "";
    }
}