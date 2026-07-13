using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Twitch.Common;
using TwitchLib.Api;

namespace Mewdeko.Modules.Twitch.Services;

/// <summary>
///     Core Twitch integration service. Dispatches EventSub chat commands, polls stream state via Helix, and raises events for
///     subscriptions, cheers, raids, and go-live/offline transitions.
/// </summary>
public class TwitchService : INService, IReadyExecutor
{
    private readonly ConcurrentDictionary<string, ulong> channelToGuild = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> chatActivityCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly DiscordShardedClient client;
    private readonly TwitchCommandHandler commandHandler;
    private readonly IBotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<string, bool> liveState = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<TwitchService> logger;

    private readonly ConcurrentDictionary<string, TwitchSessionStats> sessionStats =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly TwitchApiClient twitchApiClient;
    private TwitchAPI? helixApi;

    private CancellationTokenSource? pollCts;
    private CancellationTokenSource? timerCts;

    /// <summary>
    ///     Initializes the service with required dependencies.
    /// </summary>
    /// <param name="creds">Bot credentials providing Twitch client ID and secret.</param>
    /// <param name="dbFactory">Factory for creating database connections.</param>
    /// <param name="logger">Logger for this service.</param>
    /// <param name="commandHandler">Handler that dispatches Twitch chat commands.</param>
    /// <param name="twitchApiClient">Client for modern Twitch OAuth, EventSub, and Helix chat APIs.</param>
    /// <param name="client">The Discord client instance, used to deliver go-live notifications.</param>
    public TwitchService(
        IBotCredentials creds,
        IDataConnectionFactory dbFactory,
        ILogger<TwitchService> logger,
        TwitchCommandHandler commandHandler,
        TwitchApiClient twitchApiClient,
        DiscordShardedClient client)
    {
        this.creds = creds;
        this.dbFactory = dbFactory;
        this.logger = logger;
        this.commandHandler = commandHandler;
        this.twitchApiClient = twitchApiClient;
        this.client = client;

        StreamOnline += SendGoLiveNotificationAsync;
        StreamOffline += SendGoOfflineNotificationAsync;
        NewSub += SendSubNotificationAsync;
        Raid += SendRaidNotificationAsync;
    }

    /// <summary>
    ///     Called when the Discord bot is ready. Loads cloud-chatbot configuration and starts live-state polling.
    /// </summary>
    public async Task OnReadyAsync()
    {
        if (string.IsNullOrWhiteSpace(creds.TwitchClientId) ||
            string.IsNullOrWhiteSpace(creds.TwitchClientSecret))
        {
            logger.LogInformation("TwitchClientId or TwitchClientSecret not set; Twitch Helix API disabled");
            return;
        }

        await LoadConfigsForModernAsync();

        StartLivePoll();
        StartTimerLoop();
    }

    /// <summary>
    ///     Raised when a monitored Twitch channel transitions from offline to live.
    /// </summary>
    public event Func<TwitchStreamOnlineArgs, Task>? StreamOnline;

    /// <summary>
    ///     Raised when a monitored Twitch channel transitions from live to offline.
    /// </summary>
    public event Func<TwitchStreamOfflineArgs, Task>? StreamOffline;

    /// <summary>
    ///     Raised when a new subscription or resub occurs in a monitored channel.
    /// </summary>
    public event Func<TwitchNewSubArgs, Task>? NewSub;

    /// <summary>
    ///     Raised when a raid arrives in a monitored channel.
    /// </summary>
    public event Func<TwitchRaidArgs, Task>? Raid;

    /// <summary>
    ///     Loads enabled Twitch channel configs for the EventSub/Helix cloud-chatbot path.
    /// </summary>
    public async Task LoadConfigsForModernAsync()
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var configs = await conn.TwitchGuildConfigs
            .Where(c => c.Enabled)
            .ToListAsync();

        channelToGuild.Clear();
        foreach (var cfg in configs)
            channelToGuild[cfg.TwitchChannel] = cfg.GuildId;

        helixApi = new TwitchAPI
        {
            Helix =
            {
                Settings =
                {
                    ClientId = creds.TwitchClientId, Secret = creds.TwitchClientSecret
                }
            }
        };

        logger.LogInformation("Loaded {Count} Twitch channel config(s) for EventSub/Helix", channelToGuild.Count);
    }

    /// <summary>
    ///     Enables a Twitch channel for the given guild and persists the config to the database.
    /// </summary>
    /// <param name="guildId">The Discord guild ID that owns this config.</param>
    /// <param name="twitchChannel">Twitch channel name to join (leading # is stripped).</param>
    /// <param name="commandPrefix">Command prefix to use in this channel.</param>
    public async Task JoinChannelAsync(ulong guildId, string twitchChannel, string commandPrefix = "!")
    {
        twitchChannel = twitchChannel.ToLowerInvariant().TrimStart('#');
        commandPrefix = NormalizePrefix(commandPrefix);

        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (existing is null)
        {
            await conn.InsertAsync(new TwitchGuildConfig
            {
                GuildId = guildId,
                TwitchChannel = twitchChannel,
                CommandPrefix = commandPrefix,
                Enabled = true,
                DateAdded = DateTime.UtcNow
            });
        }
        else
        {
            existing.TwitchChannel = twitchChannel;
            existing.CommandPrefix = commandPrefix;
            existing.Enabled = true;
            await conn.UpdateAsync(existing);
        }

        channelToGuild[twitchChannel] = guildId;

        logger.LogInformation("Guild {GuildId} enabled Twitch channel #{Channel}", guildId, twitchChannel);
    }

    /// <summary>
    ///     Enables a previously configured Twitch integration without changing channel settings.
    /// </summary>
    public async Task<bool> SetEnabledAsync(ulong guildId, bool enabled)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (existing is null) return false;

        existing.Enabled = enabled;
        await conn.UpdateAsync(existing);

        if (enabled)
            channelToGuild[existing.TwitchChannel] = guildId;
        else
            channelToGuild.TryRemove(existing.TwitchChannel, out _);

        return true;
    }

    /// <summary>
    ///     Disables the Twitch config for the given guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID whose config should be disabled.</param>
    public async Task LeaveChannelAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (existing is null) return;

        var channel = existing.TwitchChannel;
        existing.Enabled = false;
        await conn.UpdateAsync(existing);
        channelToGuild.TryRemove(channel, out _);

        logger.LogInformation("Guild {GuildId} disabled Twitch channel #{Channel}", guildId, channel);
    }

    /// <summary>
    ///     Sends a chat message to a Twitch channel using the cloud-chatbot app access token.
    /// </summary>
    /// <param name="channel">The Twitch channel name.</param>
    /// <param name="message">The message to send.</param>
    public async Task SendMessageAsync(string channel, string message)
    {
        if (!await TrySendModernMessageAsync(channel, message))
            logger.LogWarning("Failed to send cloud-chatbot message to #{Channel}", channel);
    }

    private async Task<bool> TrySendModernMessageAsync(string channel, string message)
    {
        if (string.IsNullOrWhiteSpace(creds.TwitchClientId) ||
            string.IsNullOrWhiteSpace(creds.TwitchClientSecret))
            return false;

        channel = channel.TrimStart('#').ToLowerInvariant();
        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.TwitchChannel == channel && c.Enabled);
        if (config?.TwitchUserId is null)
            return false;

        var bot = await conn.TwitchBotAccounts.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        if (bot is null)
            return false;

        var appToken = await twitchApiClient.GetAppAccessTokenAsync(creds.TwitchClientId, creds.TwitchClientSecret);
        if (string.IsNullOrWhiteSpace(appToken))
            return false;

        return await twitchApiClient.SendChatMessageAsync(
            creds.TwitchClientId,
            appToken,
            config.TwitchUserId,
            bot.TwitchUserId,
            message);
    }

    private async Task<bool> RefreshChannelTokenIfNeededAsync(MewdekoDb conn, TwitchChannelAuthorization channel)
    {
        if (channel.TokenExpiresAt is null || channel.TokenExpiresAt > DateTime.UtcNow.AddMinutes(5))
            return true;

        var refreshed = await twitchApiClient.RefreshTokenAsync(
            channel.RefreshToken,
            creds.TwitchClientId,
            creds.TwitchClientSecret);

        if (refreshed is null)
            return false;

        channel.AccessToken = refreshed.AccessToken;
        channel.RefreshToken = refreshed.RefreshToken;
        channel.Scopes = string.Join(' ', refreshed.Scopes);
        channel.TokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(0, refreshed.ExpiresIn));
        channel.LastRefreshedAt = DateTime.UtcNow;
        await conn.UpdateAsync(channel);
        return true;
    }

    /// <summary>
    ///     Dispatches a modern EventSub chat message through the existing Twitch command processor.
    /// </summary>
    public async Task HandleEventSubChatMessageAsync(TwitchEventSubChatMessageEvent chatEvent)
    {
        var channel = chatEvent.BroadcasterUserLogin.ToLowerInvariant();
        if (!channelToGuild.TryGetValue(channel, out var guildId))
        {
            await LoadConfigsForModernAsync();
            if (!channelToGuild.TryGetValue(channel, out guildId))
                return;
        }

        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (config is null || !config.Enabled || !config.UseEventSub) return;

        config.LastEventAt = DateTime.UtcNow;
        await conn.UpdateAsync(config);

        var prefix = config.CommandPrefix;
        var badges = chatEvent.Badges
            .Select(x => x.SetId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var ctx = new TwitchCommandContext(
            chatEvent.ChatterUserLogin,
            chatEvent.ChatterUserName,
            channel,
            chatEvent.Message.Text,
            guildId,
            prefix,
            chatEvent.MessageId,
            badges)
        {
            LinkedDiscordUserId = await GetLinkedDiscordUserAsync(guildId, chatEvent.ChatterUserLogin),
            ChannelLanguage = config.Language
        };

        TrackChatMessage(ctx);
        await SyncLinkedUserRolesAsync(ctx);

        if (!chatEvent.Message.Text.StartsWith(prefix, StringComparison.Ordinal)) return;

        await commandHandler.ExecuteAsync(ctx);
    }

    /// <summary>Dispatches an EventSub subscription event through the existing notification pipeline.</summary>
    public async Task HandleEventSubSubscriptionAsync(
        string channel, string username, string displayName, string tier, bool isGift)
    {
        channel = channel.ToLowerInvariant();
        if (!channelToGuild.TryGetValue(channel, out var guildId) || NewSub is null) return;
        await NewSub(new TwitchNewSubArgs
        {
            Channel = channel,
            Username = username,
            DisplayName = displayName,
            SubPlan = tier,
            IsGift = isGift,
            GuildId = guildId
        });
    }

    /// <summary>Dispatches an EventSub raid through the existing notification pipeline.</summary>
    public async Task HandleEventSubRaidAsync(string channel, string raiderDisplayName, int viewerCount)
    {
        channel = channel.ToLowerInvariant();
        if (!channelToGuild.TryGetValue(channel, out var guildId) || Raid is null) return;
        await Raid(new TwitchRaidArgs
        {
            Channel = channel, RaiderDisplayName = raiderDisplayName, ViewerCount = viewerCount, GuildId = guildId
        });
    }

    /// <summary>
    ///     Returns the stored guild config for the specified guild, or <see langword="null" /> if not configured.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to look up.</param>
    public async Task<TwitchGuildConfig?> GetConfigAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
    }

    /// <summary>
    ///     Sets the Discord channel and optional message template used for go-live notifications.
    ///     Pass <c>0</c> for <paramref name="channelId" /> to clear the notification channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to update.</param>
    /// <param name="channelId">The Discord channel ID, or <c>0</c> to clear.</param>
    /// <param name="message">
    ///     Optional message template. Supports %streamer%, %title%, %game%, %url% placeholders.
    /// </param>
    public async Task SetGoLiveChannelAsync(ulong guildId, ulong channelId, string? message)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (existing is null) return;

        existing.GoLiveChannelId = channelId == 0 ? null : channelId;
        existing.GoLiveMessage = message;
        await conn.UpdateAsync(existing);
    }

    /// <summary>
    ///     Sets the Discord channel and optional message template used for Twitch sub notifications.
    /// </summary>
    public async Task SetSubNotificationChannelAsync(ulong guildId, ulong channelId, string? message)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (existing is null) return;

        existing.SubNotificationChannelId = channelId == 0 ? null : channelId;
        existing.SubNotificationMessage = message;
        await conn.UpdateAsync(existing);
    }

    /// <summary>
    ///     Sets the Discord channel and optional message template used for Twitch raid notifications.
    /// </summary>
    public async Task SetRaidNotificationChannelAsync(ulong guildId, ulong channelId, string? message)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (existing is null) return;

        existing.RaidNotificationChannelId = channelId == 0 ? null : channelId;
        existing.RaidNotificationMessage = message;
        await conn.UpdateAsync(existing);
    }

    /// <summary>
    ///     Sets or clears the Discord channel used for Twitch stream recap posts.
    /// </summary>
    public async Task SetStreamRecapChannelAsync(ulong guildId, ulong channelId, bool enabled)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (existing is null) return;

        existing.StreamRecapChannelId = channelId == 0 ? null : channelId;
        existing.StreamRecapEnabled = enabled && channelId != 0;
        await conn.UpdateAsync(existing);
    }

    /// <summary>
    ///     Sets the schedule message used by the Twitch <c>schedule</c> chat command.
    /// </summary>
    public async Task SetScheduleMessageAsync(ulong guildId, string? message)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (existing is null) return;

        existing.ScheduleMessage = string.IsNullOrWhiteSpace(message) ? null : message;
        await conn.UpdateAsync(existing);
    }

    /// <summary>
    ///     Sets the socials message used by the Twitch <c>socials</c> chat command.
    /// </summary>
    public async Task SetSocialsMessageAsync(ulong guildId, string? message)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (existing is null) return;

        existing.SocialsMessage = string.IsNullOrWhiteSpace(message) ? null : message;
        await conn.UpdateAsync(existing);
    }

    /// <summary>
    ///     Gets the configured Twitch schedule message.
    /// </summary>
    public async Task<string?> GetScheduleMessageAsync(ulong guildId)
    {
        var config = await GetConfigAsync(guildId);
        return config?.ScheduleMessage;
    }

    /// <summary>
    ///     Gets the configured Twitch socials message.
    /// </summary>
    public async Task<string?> GetSocialsMessageAsync(ulong guildId)
    {
        var config = await GetConfigAsync(guildId);
        return config?.SocialsMessage;
    }

    /// <summary>
    ///     Records a Twitch event processing result for dashboard diagnostics.
    /// </summary>
    public async Task RecordEventHistoryAsync(
        ulong guildId,
        string eventType,
        string source,
        string message,
        bool succeeded,
        string? error = null,
        string? rawPayload = null)
    {
        if (guildId == 0)
            return;

        await using var conn = await dbFactory.CreateConnectionAsync();
        await conn.InsertAsync(new TwitchEventHistory
        {
            GuildId = guildId,
            EventType = eventType,
            Source = source,
            Succeeded = succeeded,
            Message = message.Length > 500 ? message[..500] : message,
            Error = string.IsNullOrWhiteSpace(error) ? null : error,
            RawPayload = string.IsNullOrWhiteSpace(rawPayload) ? null : rawPayload,
            DateAdded = DateTime.UtcNow
        });

        var cutoff = DateTime.UtcNow.AddDays(-14);
        await conn.TwitchEventHistory
            .Where(x => x.GuildId == guildId && x.DateAdded < cutoff)
            .DeleteAsync();
    }

    /// <summary>
    ///     Lists recent Twitch event processing history for a guild.
    /// </summary>
    public async Task<List<TwitchEventHistory>> GetEventHistoryAsync(ulong guildId, int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchEventHistory
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.DateAdded)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    ///     Returns Twitch OAuth and EventSub health details for dashboard diagnostics.
    /// </summary>
    public async Task<TwitchHealthSnapshot> GetHealthSnapshotAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        var bot = await conn.TwitchBotAccounts.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        var channel = await conn.TwitchChannelAuthorizations.FirstOrDefaultAsync(x => x.GuildId == guildId);
        var subscriptions = await conn.TwitchEventSubSubscriptions
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.LastUpdatedAt)
            .ToListAsync();

        return new TwitchHealthSnapshot
        {
            HasConfig = config is not null,
            Enabled = config?.Enabled ?? false,
            TwitchChannel = config?.TwitchChannel,
            EventSubEnabled = config?.UseEventSub ?? false,
            HasBotAccount = bot is not null,
            HasChannelAuthorization = channel is not null,
            BotMissingScopes = GetMissingScopes(bot?.Scopes, TwitchOAuthScopes.Bot),
            ChannelMissingScopes = GetMissingScopes(channel?.Scopes, TwitchOAuthScopes.Channel),
            BotTokenExpiresAt = bot?.TokenExpiresAt,
            ChannelTokenExpiresAt = channel?.TokenExpiresAt,
            LastEventAt = config?.LastEventAt,
            Subscriptions = subscriptions
        };
    }

    /// <summary>
    ///     Returns documented Twitch template variables grouped by feature area.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> GetVariableDocs()
    {
        return new Dictionary<string, string[]>
        {
            ["custom_commands"] =
            [
                "%user%", "%display%", "%channel%", "%args%", "%target%", "%discord%", "%random:a|b|c%",
                "%count:name%", "%stream%"
            ],
            ["timers"] = ["%channel%", "%url%", "%stream%", "%count:name%", "%random:a|b|c%"],
            ["go_live"] = ["%streamer%", "%title%", "%game%", "%url%", "%viewers%"],
            ["sub"] = ["%user%", "%display%", "%channel%", "%tier%"],
            ["raid"] = ["%raider%", "%channel%", "%viewers%"],
            ["redemption"] = ["%user%", "%display%", "%channel%", "%reward%", "%input%", "%url%"]
        };
    }

    /// <summary>
    ///     Adds a Twitch quote for a guild.
    /// </summary>
    public async Task<TwitchQuote> AddQuoteAsync(ulong guildId, string text, string? author, string? addedBy)
    {
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Quote text cannot be empty.", nameof(text));

        await using var conn = await dbFactory.CreateConnectionAsync();
        var quote = new TwitchQuote
        {
            GuildId = guildId,
            Text = text,
            Author = string.IsNullOrWhiteSpace(author) ? null : author.Trim().TrimStart('@'),
            AddedBy = string.IsNullOrWhiteSpace(addedBy) ? null : addedBy.Trim().TrimStart('@'),
            DateAdded = DateTime.UtcNow
        };
        quote.Id = await conn.InsertWithInt32IdentityAsync(quote);
        return quote;
    }

    /// <summary>
    ///     Removes a Twitch quote by ID.
    /// </summary>
    public async Task<bool> RemoveQuoteAsync(ulong guildId, int quoteId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchQuotes
            .Where(x => x.GuildId == guildId && x.Id == quoteId)
            .DeleteAsync() > 0;
    }

    /// <summary>
    ///     Lists Twitch quotes for a guild, optionally filtered by search text.
    /// </summary>
    public async Task<List<TwitchQuote>> GetQuotesAsync(ulong guildId, string? search = null, int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        await using var conn = await dbFactory.CreateConnectionAsync();
        var query = conn.TwitchQuotes.Where(x => x.GuildId == guildId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLowerInvariant();
            query = query.Where(x => x.Text.ToLower().Contains(lowered) ||
                                     x.Author != null && x.Author.ToLower().Contains(lowered));
        }

        return await query
            .OrderByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets a Twitch quote by ID, or a random quote when no ID is provided.
    /// </summary>
    public async Task<TwitchQuote?> GetQuoteAsync(ulong guildId, int? quoteId = null)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        if (quoteId.HasValue)
            return await conn.TwitchQuotes.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Id == quoteId.Value);

        var quotes = await conn.TwitchQuotes.Where(x => x.GuildId == guildId).ToListAsync();
        return quotes.Count == 0 ? null : quotes[Random.Shared.Next(quotes.Count)];
    }

    /// <summary>
    ///     Renders a custom command response with sample arguments without sending it to Twitch chat.
    /// </summary>
    public async Task<string?> PreviewCustomCommandAsync(ulong guildId, string name, string? args)
    {
        name = NormalizeCommandName(name);
        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        var command = await conn.TwitchCustomCommands
            .FirstOrDefaultAsync(c => c.GuildId == guildId && c.Name == name);

        if (config is null || command is null)
            return null;

        var text = string.IsNullOrWhiteSpace(args)
            ? config.CommandPrefix + name
            : $"{config.CommandPrefix}{name} {args}";
        var ctx = new TwitchCommandContext(
            "dashboardtester",
            "Dashboard Tester",
            config.TwitchChannel,
            text,
            guildId,
            config.CommandPrefix,
            null,
            ["broadcaster"])
        {
            Args = string.IsNullOrWhiteSpace(args)
                ? []
                : args.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            ChannelLanguage = config.Language
        };

        return await ExpandCommandVariablesAsync(ctx, command.Response);
    }

    /// <summary>
    ///     Sends a dashboard-generated go-live test notification through the configured event template.
    /// </summary>
    public async Task TestGoLiveNotificationAsync(ulong guildId)
    {
        var config = await GetConfigAsync(guildId);
        if (config is null)
            throw new InvalidOperationException("No Twitch configuration exists for this server.");

        await SendGoLiveNotificationAsync(new TwitchStreamOnlineArgs
        {
            BroadcasterUserId = config.TwitchUserId ?? "",
            BroadcasterUserLogin = string.IsNullOrWhiteSpace(config.TwitchChannel) ? "mewdeko" : config.TwitchChannel,
            BroadcasterUserName = config.TwitchDisplayName ?? config.TwitchChannel,
            StreamId = "dashboard-test",
            StartedAt = DateTime.UtcNow,
            Title = "Dashboard test stream",
            GameName = "Just Chatting",
            ViewerCount = 123,
            GuildId = guildId
        });
    }

    /// <summary>
    ///     Sends a dashboard-generated subscription test notification through the configured event template.
    /// </summary>
    public async Task TestSubNotificationAsync(ulong guildId)
    {
        var config = await GetConfigAsync(guildId);
        if (config is null)
            throw new InvalidOperationException("No Twitch configuration exists for this server.");

        await SendSubNotificationAsync(new TwitchNewSubArgs
        {
            Channel = config.TwitchChannel,
            Username = "dashboardtester",
            DisplayName = "Dashboard Tester",
            SubPlan = "1000",
            IsGift = false,
            GuildId = guildId
        });
    }

    /// <summary>
    ///     Sends a dashboard-generated raid test notification through the configured event template.
    /// </summary>
    public async Task TestRaidNotificationAsync(ulong guildId)
    {
        var config = await GetConfigAsync(guildId);
        if (config is null)
            throw new InvalidOperationException("No Twitch configuration exists for this server.");

        await SendRaidNotificationAsync(new TwitchRaidArgs
        {
            Channel = config.TwitchChannel, RaiderDisplayName = "DashboardRaid", ViewerCount = 42, GuildId = guildId
        });
    }

    /// <summary>
    ///     Builds the default go-live embed for a stream, matching the look of the generic
    ///     stream-notifications module so both systems feel consistent.
    /// </summary>
    private static EmbedBuilder GetGoLiveEmbed(TwitchStreamOnlineArgs stream)
    {
        var url = $"https://twitch.tv/{stream.BroadcasterUserLogin}";
        var embed = new EmbedBuilder()
            .WithTitle(stream.BroadcasterUserName)
            .WithUrl(url)
            .WithDescription(url)
            .AddField("Status", "🟢 Online", true)
            .AddField("Viewers", stream.ViewerCount.ToString("N0"), true)
            .WithColor(Mewdeko.OkColor);

        if (!string.IsNullOrWhiteSpace(stream.Title))
            embed.WithAuthor(stream.Title);

        if (!string.IsNullOrWhiteSpace(stream.GameName))
            embed.AddField("Playing", stream.GameName, true);

        if (!string.IsNullOrWhiteSpace(stream.ThumbnailUrl))
        {
            var preview = stream.ThumbnailUrl.Replace("{width}", "1280").Replace("{height}", "720");
            embed.WithImageUrl($"{preview}?dv={new MewdekoRandom().Next()}");
        }

        return embed;
    }

    /// <summary>
    ///     Sends the configured go-live notification to the guild's chosen Discord channel.
    ///     Supports the same <c>%streamer%</c>/<c>%title%</c>/<c>%game%</c>/<c>%url%</c> placeholders
    ///     documented on <c>/twitch golive-channel</c>, plus SmartEmbed JSON for full custom embeds.
    /// </summary>
    private async Task SendGoLiveNotificationAsync(TwitchStreamOnlineArgs stream)
    {
        sessionStats[GetSessionKey(stream.GuildId, stream.BroadcasterUserLogin)] = new TwitchSessionStats
        {
            StartedAt = stream.StartedAt,
            Title = stream.Title,
            GameName = stream.GameName,
            PeakViewers = stream.ViewerCount
        };

        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == stream.GuildId);
        if (config?.GoLiveChannelId is null)
            return;

        var textChannel = client.GetGuild(stream.GuildId)?.GetTextChannel(config.GoLiveChannelId.Value);
        if (textChannel is null)
        {
            logger.LogWarning(
                "[TwitchService] Could not find go-live channel {ChannelId} in guild {GuildId}",
                config.GoLiveChannelId, stream.GuildId);
            return;
        }

        var url = $"https://twitch.tv/{stream.BroadcasterUserLogin}";
        var replacer = new ReplacementBuilder()
            .WithOverride("%streamer%", () => stream.BroadcasterUserName)
            .WithOverride("%title%", () => stream.Title)
            .WithOverride("%game%", () => stream.GameName)
            .WithOverride("%url%", () => url)
            .WithOverride("%viewers%", () => stream.ViewerCount.ToString("N0"))
            .Build();

        if (!string.IsNullOrWhiteSpace(config.GoLiveMessage))
        {
            var processed = replacer.Replace(config.GoLiveMessage);

            if (SmartEmbed.TryParse(processed ?? string.Empty, stream.GuildId, out var embed, out var plainText,
                    out var components))
            {
                await textChannel.SendMessageAsync(plainText, embeds: embed, components: components?.Build());
                return;
            }

            await textChannel.EmbedAsync(GetGoLiveEmbed(stream), processed);
            return;
        }

        await textChannel.EmbedAsync(GetGoLiveEmbed(stream));
    }

    /// <summary>
    ///     Sends a plain "gone offline" notice when the guild has opted in via the go-live channel.
    /// </summary>
    private async Task SendGoOfflineNotificationAsync(TwitchStreamOfflineArgs stream)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == stream.GuildId);

        if (config?.GoLiveChannelId is not null)
        {
            var textChannel = client.GetGuild(stream.GuildId)?.GetTextChannel(config.GoLiveChannelId.Value);
            if (textChannel is not null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(stream.BroadcasterUserName)
                    .WithDescription($"https://twitch.tv/{stream.BroadcasterUserLogin}")
                    .AddField("Status", "🔴 Offline", true)
                    .WithColor(Mewdeko.ErrorColor);

                await textChannel.EmbedAsync(embed);
            }
        }

        await SendStreamRecapAsync(config, stream);
    }

    private async Task SendStreamRecapAsync(TwitchGuildConfig? config, TwitchStreamOfflineArgs stream)
    {
        if (config is not { StreamRecapEnabled: true, StreamRecapChannelId: not null })
            return;

        var textChannel = client.GetGuild(stream.GuildId)?.GetTextChannel(config.StreamRecapChannelId.Value);
        if (textChannel is null)
            return;

        sessionStats.TryRemove(GetSessionKey(stream.GuildId, stream.BroadcasterUserLogin), out var stats);
        stats ??= new TwitchSessionStats
        {
            StartedAt = DateTime.UtcNow
        };

        var duration = DateTime.UtcNow - stats.StartedAt;
        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle($"{stream.BroadcasterUserName} stream recap")
            .WithDescription($"https://twitch.tv/{stream.BroadcasterUserLogin}")
            .AddField("Duration", $"{(int)duration.TotalHours}h {duration.Minutes}m", true)
            .AddField("Peak Viewers", stats.PeakViewers.ToString("N0"), true)
            .AddField("Chat Messages", stats.ChatMessages.ToString("N0"), true)
            .AddField("Subs", stats.Subs.ToString("N0"), true)
            .AddField("Raids", stats.Raids.ToString("N0"), true);

        if (!string.IsNullOrWhiteSpace(stats.Title))
            embed.AddField("Title", stats.Title);
        if (!string.IsNullOrWhiteSpace(stats.GameName))
            embed.AddField("Category", stats.GameName, true);

        await textChannel.EmbedAsync(embed);
    }

    private async Task SendSubNotificationAsync(TwitchNewSubArgs sub)
    {
        if (sessionStats.TryGetValue(GetSessionKey(sub.GuildId, sub.Channel), out var stats))
            stats.Subs++;

        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == sub.GuildId);
        if (config?.SubNotificationChannelId is null)
            return;

        var textChannel = client.GetGuild(sub.GuildId)?.GetTextChannel(config.SubNotificationChannelId.Value);
        if (textChannel is null)
            return;

        var message = config.SubNotificationMessage;
        if (string.IsNullOrWhiteSpace(message))
            message = sub.IsGift
                ? "%display% received a gifted sub on %channel%!"
                : "%display% subscribed to %channel%!";

        message = message
            .Replace("%user%", sub.Username, StringComparison.OrdinalIgnoreCase)
            .Replace("%display%", sub.DisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace("%channel%", sub.Channel, StringComparison.OrdinalIgnoreCase)
            .Replace("%tier%", sub.SubPlan, StringComparison.OrdinalIgnoreCase);

        await textChannel.SendMessageAsync(message);
    }

    private async Task SendRaidNotificationAsync(TwitchRaidArgs raid)
    {
        if (sessionStats.TryGetValue(GetSessionKey(raid.GuildId, raid.Channel), out var stats))
            stats.Raids++;

        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == raid.GuildId);
        if (config?.RaidNotificationChannelId is null)
            return;

        var textChannel = client.GetGuild(raid.GuildId)?.GetTextChannel(config.RaidNotificationChannelId.Value);
        if (textChannel is null)
            return;

        var message = config.RaidNotificationMessage;
        if (string.IsNullOrWhiteSpace(message))
            message = "%raider% raided %channel% with %viewers% viewers!";

        message = message
            .Replace("%raider%", raid.RaiderDisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace("%channel%", raid.Channel, StringComparison.OrdinalIgnoreCase)
            .Replace("%viewers%", raid.ViewerCount.ToString("N0"), StringComparison.OrdinalIgnoreCase);

        await textChannel.SendMessageAsync(message);
    }

    /// <summary>
    ///     Sets or clears the language override for a guild's Twitch channel.
    ///     When set, Twitch chat responses use this locale instead of the guild's Discord locale.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to update.</param>
    /// <param name="languageTag">A BCP-47 language tag, or <see langword="null" /> to clear the override.</param>
    public async Task SetChannelLanguageAsync(ulong guildId, string? languageTag)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (existing is null) return;

        existing.Language = languageTag;
        await conn.UpdateAsync(existing);
    }

    /// <summary>
    ///     Creates or updates a database-backed Twitch chat command for a guild.
    /// </summary>
    public async Task<TwitchCustomCommand> UpsertCustomCommandAsync(
        ulong guildId,
        string name,
        string response,
        TwitchCommandPermission permission = TwitchCommandPermission.Everyone,
        int cooldownSeconds = 0,
        bool enabled = true)
    {
        name = NormalizeCommandName(name);
        cooldownSeconds = Math.Clamp(cooldownSeconds, 0, 86400);

        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchCustomCommands
            .FirstOrDefaultAsync(c => c.GuildId == guildId && c.Name == name);

        if (existing is null)
        {
            var command = new TwitchCustomCommand
            {
                GuildId = guildId,
                Name = name,
                Response = response,
                PermissionLevel = (int)permission,
                CooldownSeconds = cooldownSeconds,
                Enabled = enabled,
                DateAdded = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };
            command.Id = await conn.InsertWithInt32IdentityAsync(command);
            return command;
        }

        existing.Response = response;
        existing.PermissionLevel = (int)permission;
        existing.CooldownSeconds = cooldownSeconds;
        existing.Enabled = enabled;
        existing.LastUpdatedAt = DateTime.UtcNow;
        await conn.UpdateAsync(existing);
        return existing;
    }

    /// <summary>
    ///     Removes a database-backed Twitch chat command for a guild.
    /// </summary>
    public async Task<bool> RemoveCustomCommandAsync(ulong guildId, string name)
    {
        name = NormalizeCommandName(name);
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchCustomCommands
            .Where(c => c.GuildId == guildId && c.Name == name)
            .DeleteAsync() > 0;
    }

    /// <summary>
    ///     Lists database-backed Twitch chat commands for a guild.
    /// </summary>
    public async Task<List<TwitchCustomCommand>> GetCustomCommandsAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchCustomCommands
            .Where(c => c.GuildId == guildId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>
    ///     Executes a database-backed Twitch chat command if it exists.
    /// </summary>
    public async Task<bool> TryExecuteCustomCommandAsync(TwitchCommandContext ctx, string name)
    {
        name = NormalizeCommandName(name);
        await using var conn = await dbFactory.CreateConnectionAsync();
        var command = await conn.TwitchCustomCommands
            .FirstOrDefaultAsync(c => c.GuildId == ctx.GuildId && c.Name == name);

        if (command is null)
            return false;

        if (!command.Enabled)
            return true;

        if ((int)ctx.PermissionLevel < command.PermissionLevel)
            return true;

        if (command.CooldownSeconds > 0 &&
            command.LastUsedAt is { } lastUsed &&
            lastUsed.AddSeconds(command.CooldownSeconds) > DateTime.UtcNow)
            return true;

        command.UseCount++;
        command.LastUsedAt = DateTime.UtcNow;
        await conn.UpdateAsync(command);

        var response = await ExpandCommandVariablesAsync(ctx, command.Response);

        await SendMessageAsync(ctx.TwitchChannel, response);
        return true;
    }

    /// <summary>
    ///     Returns a shoutout line for a Twitch channel.
    /// </summary>
    public async Task<string?> GetShoutoutAsync(string login)
    {
        if (helixApi is null)
            return null;

        login = NormalizeCommandName(login);
        var userResponse = await helixApi.Helix.Users.GetUsersAsync(logins: [login]);
        var user = userResponse.Users.FirstOrDefault();
        if (user is null)
            return null;

        var streamResponse = await helixApi.Helix.Streams.GetStreamsAsync(userLogins: [login]);
        var stream = streamResponse.Streams.FirstOrDefault();
        if (stream is null)
            return $"Go check out {user.DisplayName} at https://twitch.tv/{user.Login}";

        return
            $"Go check out {user.DisplayName} at https://twitch.tv/{user.Login} - last seen streaming {stream.GameName}: {stream.Title}";
    }

    /// <summary>
    ///     Gets a Twitch counter value.
    /// </summary>
    public async Task<int?> GetCounterAsync(ulong guildId, string name)
    {
        name = NormalizeCommandName(name);
        await using var conn = await dbFactory.CreateConnectionAsync();
        var counter = await conn.TwitchCounters.FirstOrDefaultAsync(c => c.GuildId == guildId && c.Name == name);
        return counter?.Value;
    }

    /// <summary>
    ///     Adds a delta to a Twitch counter and creates it if needed.
    /// </summary>
    public async Task<int> AddCounterAsync(ulong guildId, string name, int delta)
    {
        name = NormalizeCommandName(name);
        await using var conn = await dbFactory.CreateConnectionAsync();
        var counter = await conn.TwitchCounters.FirstOrDefaultAsync(c => c.GuildId == guildId && c.Name == name);
        if (counter is null)
        {
            counter = new TwitchCounter
            {
                GuildId = guildId,
                Name = name,
                Value = delta,
                DateAdded = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };
            counter.Id = await conn.InsertWithInt32IdentityAsync(counter);
            return counter.Value;
        }

        counter.Value += delta;
        counter.LastUpdatedAt = DateTime.UtcNow;
        await conn.UpdateAsync(counter);
        return counter.Value;
    }

    /// <summary>
    ///     Sets a Twitch counter to a specific value and creates it if needed.
    /// </summary>
    public async Task<int> SetCounterAsync(ulong guildId, string name, int value)
    {
        name = NormalizeCommandName(name);
        await using var conn = await dbFactory.CreateConnectionAsync();
        var counter = await conn.TwitchCounters.FirstOrDefaultAsync(c => c.GuildId == guildId && c.Name == name);
        if (counter is null)
        {
            counter = new TwitchCounter
            {
                GuildId = guildId,
                Name = name,
                Value = value,
                DateAdded = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };
            counter.Id = await conn.InsertWithInt32IdentityAsync(counter);
            return counter.Value;
        }

        counter.Value = value;
        counter.LastUpdatedAt = DateTime.UtcNow;
        await conn.UpdateAsync(counter);
        return counter.Value;
    }

    /// <summary>
    ///     Lists Twitch counters for a guild.
    /// </summary>
    public async Task<List<TwitchCounter>> GetCountersAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchCounters
            .Where(c => c.GuildId == guildId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>
    ///     Creates or updates a Twitch role sync mapping.
    /// </summary>
    public async Task<TwitchRoleSyncMapping> UpsertRoleSyncMappingAsync(
        ulong guildId,
        TwitchPermissionLevel permissionLevel,
        ulong roleId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchRoleSyncMappings
            .FirstOrDefaultAsync(x => x.GuildId == guildId &&
                                      x.PermissionLevel == (int)permissionLevel &&
                                      x.RoleId == roleId);

        if (existing is null)
        {
            var mapping = new TwitchRoleSyncMapping
            {
                GuildId = guildId,
                PermissionLevel = (int)permissionLevel,
                RoleId = roleId,
                Enabled = true,
                DateAdded = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };
            mapping.Id = await conn.InsertWithInt32IdentityAsync(mapping);
            return mapping;
        }

        existing.Enabled = true;
        existing.LastUpdatedAt = DateTime.UtcNow;
        await conn.UpdateAsync(existing);
        return existing;
    }

    /// <summary>
    ///     Removes a Twitch role sync mapping.
    /// </summary>
    public async Task<bool> RemoveRoleSyncMappingAsync(ulong guildId, TwitchPermissionLevel permissionLevel,
        ulong roleId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchRoleSyncMappings
            .Where(x => x.GuildId == guildId &&
                        x.PermissionLevel == (int)permissionLevel &&
                        x.RoleId == roleId)
            .DeleteAsync() > 0;
    }

    /// <summary>
    ///     Lists Twitch role sync mappings for a guild.
    /// </summary>
    public async Task<List<TwitchRoleSyncMapping>> GetRoleSyncMappingsAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchRoleSyncMappings
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.PermissionLevel)
            .ToListAsync();
    }

    /// <summary>
    ///     Creates or updates a Twitch channel point redemption action.
    /// </summary>
    public async Task<TwitchRedemptionAction> UpsertRedemptionActionAsync(
        ulong guildId,
        string rewardTitle,
        string? twitchResponse,
        ulong? discordChannelId,
        string? discordMessage)
    {
        rewardTitle = rewardTitle.Trim();
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchRedemptionActions
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RewardTitle.ToLower() == rewardTitle.ToLower());

        if (existing is null)
        {
            var action = new TwitchRedemptionAction
            {
                GuildId = guildId,
                RewardTitle = rewardTitle,
                TwitchResponse = string.IsNullOrWhiteSpace(twitchResponse) ? null : twitchResponse,
                DiscordChannelId = discordChannelId == 0 ? null : discordChannelId,
                DiscordMessage = string.IsNullOrWhiteSpace(discordMessage) ? null : discordMessage,
                Enabled = true,
                DateAdded = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };
            action.Id = await conn.InsertWithInt32IdentityAsync(action);
            return action;
        }

        existing.TwitchResponse = string.IsNullOrWhiteSpace(twitchResponse) ? null : twitchResponse;
        existing.DiscordChannelId = discordChannelId == 0 ? null : discordChannelId;
        existing.DiscordMessage = string.IsNullOrWhiteSpace(discordMessage) ? null : discordMessage;
        existing.Enabled = true;
        existing.LastUpdatedAt = DateTime.UtcNow;
        await conn.UpdateAsync(existing);
        return existing;
    }

    /// <summary>
    ///     Removes a Twitch channel point redemption action.
    /// </summary>
    public async Task<bool> RemoveRedemptionActionAsync(ulong guildId, string rewardTitle)
    {
        rewardTitle = rewardTitle.Trim();
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchRedemptionActions
            .Where(x => x.GuildId == guildId && x.RewardTitle.ToLower() == rewardTitle.ToLower())
            .DeleteAsync() > 0;
    }

    /// <summary>
    ///     Lists Twitch channel point redemption actions for a guild.
    /// </summary>
    public async Task<List<TwitchRedemptionAction>> GetRedemptionActionsAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchRedemptionActions
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.RewardTitle)
            .ToListAsync();
    }

    /// <summary>
    ///     Handles a Twitch channel point redemption event.
    /// </summary>
    public async Task HandleChannelPointRedemptionAsync(TwitchChannelPointRedemptionArgs redemption)
    {
        var guildId = redemption.GuildId;
        if (guildId == 0 &&
            !channelToGuild.TryGetValue(redemption.BroadcasterUserLogin, out guildId))
        {
            await LoadConfigsForModernAsync();
            if (!channelToGuild.TryGetValue(redemption.BroadcasterUserLogin, out guildId))
                return;
        }

        await using var conn = await dbFactory.CreateConnectionAsync();
        var action = await conn.TwitchRedemptionActions
            .FirstOrDefaultAsync(x => x.GuildId == guildId &&
                                      x.Enabled &&
                                      x.RewardTitle.ToLower() == redemption.RewardTitle.ToLower());

        if (action is null)
            return;

        var twitchMessage = ApplyRedemptionVariables(action.TwitchResponse, redemption);
        if (!string.IsNullOrWhiteSpace(twitchMessage))
            await SendMessageAsync(redemption.BroadcasterUserLogin, twitchMessage);

        if (action.DiscordChannelId is not null)
        {
            var channel = client.GetGuild(guildId)?.GetTextChannel(action.DiscordChannelId.Value);
            var discordMessage = ApplyRedemptionVariables(action.DiscordMessage, redemption);
            if (channel is not null && !string.IsNullOrWhiteSpace(discordMessage))
                await channel.SendMessageAsync(discordMessage);
        }
    }

    /// <summary>
    ///     Returns a concise health summary for the Twitch integration in a guild.
    /// </summary>
    public async Task<string> GetStatusSummaryAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        var bot = await conn.TwitchBotAccounts.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        var channel = await conn.TwitchChannelAuthorizations.FirstOrDefaultAsync(x => x.GuildId == guildId);
        var commandCount = await conn.TwitchCustomCommands.CountAsync(x => x.GuildId == guildId);
        var roleSyncCount = await conn.TwitchRoleSyncMappings.CountAsync(x => x.GuildId == guildId && x.Enabled);
        var redemptionCount = await conn.TwitchRedemptionActions.CountAsync(x => x.GuildId == guildId && x.Enabled);

        if (config is null)
            return "No Twitch configuration exists for this server.";

        var eventSubState = config.UseEventSub ? "enabled" : "disabled";
        var lastEvent = config.LastEventAt.HasValue ? $"{config.LastEventAt.Value:u}" : "never";
        var botExpiry = bot?.TokenExpiresAt is null ? "unknown" : $"{bot.TokenExpiresAt.Value:u}";
        var channelExpiry = channel?.TokenExpiresAt is null ? "unknown" : $"{channel.TokenExpiresAt.Value:u}";

        return $"""
                Channel: #{config.TwitchChannel}
                Enabled: {config.Enabled}
                EventSub: {eventSubState}, last event {lastEvent}
                Bot account: {(bot is null ? "not connected" : bot.TwitchUsername)}, token expires {botExpiry}
                Channel auth: {(channel is null ? "not connected" : channel.TwitchUsername)}, token expires {channelExpiry}
                Custom commands: {commandCount}
                Role sync mappings: {roleSyncCount}
                Redemption actions: {redemptionCount}
                Recaps: {(config.StreamRecapEnabled ? "enabled" : "disabled")}
                """;
    }

    /// <summary>
    ///     Updates the configured Twitch channel title and/or category.
    /// </summary>
    public async Task<bool> UpdateStreamInfoAsync(ulong guildId, string? title, string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(categoryName))
            return false;

        await using var conn = await dbFactory.CreateConnectionAsync();
        var channel = await conn.TwitchChannelAuthorizations.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (channel is null)
            return false;

        if (!await RefreshChannelTokenIfNeededAsync(conn, channel))
            return false;

        string? gameId = null;
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            gameId = await twitchApiClient.SearchCategoryIdAsync(creds.TwitchClientId, channel.AccessToken,
                categoryName);
            if (string.IsNullOrWhiteSpace(gameId))
                return false;
        }

        return await twitchApiClient.UpdateChannelInformationAsync(
            creds.TwitchClientId,
            channel.AccessToken,
            channel.TwitchUserId,
            title,
            gameId);
    }

    /// <summary>
    ///     Creates a clip for the configured Twitch channel.
    /// </summary>
    public async Task<string?> CreateClipAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var channel = await conn.TwitchChannelAuthorizations.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (channel is null)
            return null;

        if (!await RefreshChannelTokenIfNeededAsync(conn, channel))
            return null;

        return await twitchApiClient.CreateClipAsync(creds.TwitchClientId, channel.AccessToken, channel.TwitchUserId);
    }

    /// <summary>
    ///     Sends a dashboard-authored message to the configured Twitch channel.
    /// </summary>
    public async Task<bool> SendDashboardChatMessageAsync(ulong guildId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is null || string.IsNullOrWhiteSpace(config.TwitchChannel))
            return false;

        try
        {
            await SendMessageAsync(config.TwitchChannel, message);
            await RecordEventHistoryAsync(guildId, "chat.send", "dashboard", "Sent dashboard chat message", true);
            return true;
        }
        catch (Exception ex)
        {
            await RecordEventHistoryAsync(guildId, "chat.send", "dashboard", "Failed dashboard chat message", false,
                ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     Creates a Twitch stream marker for the configured channel.
    /// </summary>
    public async Task<bool> CreateStreamMarkerAsync(ulong guildId, string? description)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var channel = await conn.TwitchChannelAuthorizations.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (channel is null)
            return false;

        if (!await RefreshChannelTokenIfNeededAsync(conn, channel))
            return false;

        var created = await twitchApiClient.CreateStreamMarkerAsync(
            creds.TwitchClientId,
            channel.AccessToken,
            channel.TwitchUserId,
            description);
        await RecordEventHistoryAsync(guildId, "marker.create", "dashboard",
            created ? "Created stream marker" : "Failed to create stream marker", created);
        return created;
    }

    /// <summary>
    ///     Creates a Twitch poll for the configured channel.
    /// </summary>
    public async Task<bool> CreatePollAsync(ulong guildId, string title, IReadOnlyCollection<string> choices,
        int durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(title) || choices.Count < 2)
            return false;

        var cleanedChoices = choices
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        if (cleanedChoices.Length < 2)
            return false;

        await using var conn = await dbFactory.CreateConnectionAsync();
        var channel = await conn.TwitchChannelAuthorizations.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (channel is null)
            return false;

        if (!await RefreshChannelTokenIfNeededAsync(conn, channel))
            return false;

        var created = await twitchApiClient.CreatePollAsync(
            creds.TwitchClientId,
            channel.AccessToken,
            channel.TwitchUserId,
            title.Trim(),
            cleanedChoices,
            durationSeconds);
        await RecordEventHistoryAsync(guildId, "poll.create", "dashboard",
            created ? $"Created poll {title.Trim()}" : $"Failed to create poll {title.Trim()}", created);
        return created;
    }

    /// <summary>
    ///     Times out or bans a Twitch user from the configured channel.
    /// </summary>
    public async Task<bool> ModerateUserAsync(ulong guildId, string username, int? durationSeconds, string? reason)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        await using var conn = await dbFactory.CreateConnectionAsync();
        var channel = await conn.TwitchChannelAuthorizations.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (channel is null)
            return false;

        if (!await RefreshChannelTokenIfNeededAsync(conn, channel))
            return false;

        var user = await twitchApiClient.GetUserByLoginAsync(creds.TwitchClientId, channel.AccessToken, username);
        if (user is null)
            return false;

        var moderated = await twitchApiClient.BanUserAsync(
            creds.TwitchClientId,
            channel.AccessToken,
            channel.TwitchUserId,
            channel.TwitchUserId,
            user.Id,
            durationSeconds,
            reason);
        var action = durationSeconds.HasValue ? "timeout" : "ban";
        await RecordEventHistoryAsync(guildId, $"moderation.{action}", "dashboard",
            moderated ? $"{action} applied to {user.Login}" : $"Failed to {action} {username}", moderated);
        return moderated;
    }

    /// <summary>
    ///     Removes a ban or timeout from a Twitch user in the configured channel.
    /// </summary>
    public async Task<bool> UnmoderateUserAsync(ulong guildId, string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        await using var conn = await dbFactory.CreateConnectionAsync();
        var channel = await conn.TwitchChannelAuthorizations.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (channel is null)
            return false;

        if (!await RefreshChannelTokenIfNeededAsync(conn, channel))
            return false;

        var user = await twitchApiClient.GetUserByLoginAsync(creds.TwitchClientId, channel.AccessToken, username);
        if (user is null)
            return false;

        var removed = await twitchApiClient.UnbanUserAsync(
            creds.TwitchClientId,
            channel.AccessToken,
            channel.TwitchUserId,
            channel.TwitchUserId,
            user.Id);
        await RecordEventHistoryAsync(guildId, "moderation.unban", "dashboard",
            removed ? $"Removed ban/timeout for {user.Login}" : $"Failed to remove ban/timeout for {username}",
            removed);
        return removed;
    }

    /// <summary>
    ///     Deletes a Twitch chat message by message ID.
    /// </summary>
    public async Task<bool> DeleteChatMessageAsync(ulong guildId, string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return false;

        await using var conn = await dbFactory.CreateConnectionAsync();
        var channel = await conn.TwitchChannelAuthorizations.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (channel is null)
            return false;

        if (!await RefreshChannelTokenIfNeededAsync(conn, channel))
            return false;

        var deleted = await twitchApiClient.DeleteChatMessageAsync(
            creds.TwitchClientId,
            channel.AccessToken,
            channel.TwitchUserId,
            channel.TwitchUserId,
            messageId);
        await RecordEventHistoryAsync(guildId, "moderation.delete_message", "dashboard",
            deleted ? $"Deleted chat message {messageId}" : $"Failed to delete chat message {messageId}", deleted);
        return deleted;
    }

    /// <summary>
    ///     Creates or updates a Twitch raid target suggestion.
    /// </summary>
    public async Task<TwitchRaidTarget> UpsertRaidTargetAsync(ulong guildId, string twitchLogin, string? note)
    {
        twitchLogin = NormalizeCommandName(twitchLogin);
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchRaidTargets
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.TwitchLogin == twitchLogin);

        if (existing is null)
        {
            var target = new TwitchRaidTarget
            {
                GuildId = guildId,
                TwitchLogin = twitchLogin,
                Note = string.IsNullOrWhiteSpace(note) ? null : note,
                Enabled = true,
                DateAdded = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };
            target.Id = await conn.InsertWithInt32IdentityAsync(target);
            return target;
        }

        existing.Note = string.IsNullOrWhiteSpace(note) ? null : note;
        existing.Enabled = true;
        existing.LastUpdatedAt = DateTime.UtcNow;
        await conn.UpdateAsync(existing);
        return existing;
    }

    /// <summary>
    ///     Removes a Twitch raid target suggestion.
    /// </summary>
    public async Task<bool> RemoveRaidTargetAsync(ulong guildId, string twitchLogin)
    {
        twitchLogin = NormalizeCommandName(twitchLogin);
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchRaidTargets
            .Where(x => x.GuildId == guildId && x.TwitchLogin == twitchLogin)
            .DeleteAsync() > 0;
    }

    /// <summary>
    ///     Lists configured Twitch raid target suggestions.
    /// </summary>
    public async Task<List<TwitchRaidTarget>> GetRaidTargetsAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchRaidTargets
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.TwitchLogin)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets a random enabled Twitch raid target suggestion.
    /// </summary>
    public async Task<TwitchRaidTarget?> GetRandomRaidTargetAsync(ulong guildId)
    {
        var targets = await GetRaidTargetsAsync(guildId);
        var enabled = targets.Where(x => x.Enabled).ToArray();
        return enabled.Length == 0 ? null : enabled[Random.Shared.Next(enabled.Length)];
    }

    /// <summary>
    ///     Creates or updates a repeating Twitch chat message timer.
    /// </summary>
    public async Task<TwitchTimer> UpsertTimerAsync(
        ulong guildId,
        string name,
        string messages,
        int intervalMinutes,
        int minChatMessages,
        bool onlineOnly,
        bool randomizeMessages,
        bool enabled)
    {
        name = NormalizeCommandName(name);
        intervalMinutes = Math.Clamp(intervalMinutes, 1, 1440);
        minChatMessages = Math.Clamp(minChatMessages, 0, 10000);
        messages = NormalizeTimerMessages(messages);

        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchTimers
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == name);

        if (existing is null)
        {
            var timer = new TwitchTimer
            {
                GuildId = guildId,
                Name = name,
                Messages = messages,
                IntervalMinutes = intervalMinutes,
                MinChatMessages = minChatMessages,
                OnlineOnly = onlineOnly,
                RandomizeMessages = randomizeMessages,
                Enabled = enabled,
                DateAdded = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };
            timer.Id = await conn.InsertWithInt32IdentityAsync(timer);
            return timer;
        }

        existing.Messages = messages;
        existing.IntervalMinutes = intervalMinutes;
        existing.MinChatMessages = minChatMessages;
        existing.OnlineOnly = onlineOnly;
        existing.RandomizeMessages = randomizeMessages;
        existing.Enabled = enabled;
        existing.LastUpdatedAt = DateTime.UtcNow;
        await conn.UpdateAsync(existing);
        return existing;
    }

    /// <summary>
    ///     Enables or disables a repeating Twitch chat message timer.
    /// </summary>
    public async Task<bool> SetTimerEnabledAsync(ulong guildId, string name, bool enabled)
    {
        name = NormalizeCommandName(name);
        await using var conn = await dbFactory.CreateConnectionAsync();
        var timer = await conn.TwitchTimers.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == name);
        if (timer is null)
            return false;

        timer.Enabled = enabled;
        timer.LastUpdatedAt = DateTime.UtcNow;
        await conn.UpdateAsync(timer);
        return true;
    }

    /// <summary>
    ///     Removes a repeating Twitch chat message timer.
    /// </summary>
    public async Task<bool> RemoveTimerAsync(ulong guildId, string name)
    {
        name = NormalizeCommandName(name);
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchTimers
            .Where(x => x.GuildId == guildId && x.Name == name)
            .DeleteAsync() > 0;
    }

    /// <summary>
    ///     Lists repeating Twitch chat message timers for a guild.
    /// </summary>
    public async Task<List<TwitchTimer>> GetTimersAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchTimers
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    /// <summary>
    ///     Sends a repeating Twitch chat message timer immediately for dashboard or slash-command testing.
    /// </summary>
    public async Task<string?> TestTimerAsync(ulong guildId, string name)
    {
        name = NormalizeCommandName(name);
        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Enabled);
        var timer = await conn.TwitchTimers.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == name);
        if (config is null || timer is null)
            return null;

        var message = await RenderTimerMessageAsync(config, timer);
        if (string.IsNullOrWhiteSpace(message))
            return null;

        await SendMessageAsync(config.TwitchChannel, message);
        await RecordEventHistoryAsync(guildId, "timer.test", "dashboard", $"Sent timer {timer.Name}", true);
        return message;
    }

    /// <summary>
    ///     Generates a short-lived code that a Twitch chatter can claim from Discord.
    /// </summary>
    public async Task<string> GenerateLinkCodeAsync(ulong guildId, string twitchUsername)
    {
        twitchUsername = twitchUsername.ToLowerInvariant();
        await using var conn = await dbFactory.CreateConnectionAsync();
        await conn.TwitchLinkCodes
            .Where(c => c.GuildId == guildId && c.TwitchUsername == twitchUsername && c.ClaimedAt == null)
            .DeleteAsync();

        string code;
        do
        {
            code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        } while (await conn.TwitchLinkCodes.AnyAsync(c => c.Code == code && c.ClaimedAt == null));

        await conn.InsertAsync(new TwitchLinkCode
        {
            GuildId = guildId,
            TwitchUsername = twitchUsername,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            DateAdded = DateTime.UtcNow
        });

        return code;
    }

    /// <summary>
    ///     Claims a Twitch self-link code for the specified Discord user.
    /// </summary>
    public async Task<(bool Success, string? TwitchUsername, string ErrorKey)> ClaimLinkCodeAsync(
        ulong guildId,
        ulong discordUserId,
        string code)
    {
        code = code.Trim();
        await using var conn = await dbFactory.CreateConnectionAsync();
        var linkCode = await conn.TwitchLinkCodes
            .FirstOrDefaultAsync(c => c.GuildId == guildId && c.Code == code && c.ClaimedAt == null);

        if (linkCode is null)
            return (false, null, "twitch_claim_invalid");

        if (linkCode.ExpiresAt <= DateTime.UtcNow)
            return (false, null, "twitch_claim_expired");

        await LinkAccountAsync(guildId, discordUserId, linkCode.TwitchUsername);
        linkCode.ClaimedAt = DateTime.UtcNow;
        await conn.UpdateAsync(linkCode);
        return (true, linkCode.TwitchUsername, "");
    }

    /// <summary>
    ///     Returns a compact status string for the configured Twitch stream.
    /// </summary>
    public async Task<string?> GetStreamSummaryAsync(ulong guildId)
    {
        var config = await GetConfigAsync(guildId);
        if (config is null || string.IsNullOrWhiteSpace(config.TwitchChannel) || helixApi is null)
            return null;

        var response = await helixApi.Helix.Streams.GetStreamsAsync(userLogins: [config.TwitchChannel]);
        var stream = response.Streams.FirstOrDefault();
        var url = $"https://twitch.tv/{config.TwitchChannel}";

        if (stream is null)
            return $"{config.TwitchChannel} is offline. {url}";

        var duration = DateTime.UtcNow - stream.StartedAt;
        return
            $"{stream.UserName} is live: {stream.Title} | {stream.GameName} | {stream.ViewerCount:N0} viewers | {duration.Hours + duration.Days * 24}h {duration.Minutes}m | {url}";
    }

    /// <summary>
    ///     Links a Discord user to a Twitch username within a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="discordUserId">The Discord user ID.</param>
    /// <param name="twitchUsername">The Twitch login name (will be lowercased).</param>
    public async Task LinkAccountAsync(ulong guildId, ulong discordUserId, string twitchUsername)
    {
        twitchUsername = twitchUsername.ToLowerInvariant();
        await using var conn = await dbFactory.CreateConnectionAsync();
        var existing = await conn.TwitchAccountLinks
            .FirstOrDefaultAsync(l => l.GuildId == guildId && l.DiscordUserId == discordUserId);

        if (existing is null)
        {
            await conn.InsertAsync(new TwitchAccountLink
            {
                GuildId = guildId,
                DiscordUserId = discordUserId,
                TwitchUsername = twitchUsername,
                DateAdded = DateTime.UtcNow
            });
        }
        else
        {
            existing.TwitchUsername = twitchUsername;
            await conn.UpdateAsync(existing);
        }
    }

    /// <summary>
    ///     Removes the Twitch account link for a Discord user in a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="discordUserId">The Discord user ID whose link should be removed.</param>
    public async Task UnlinkAccountAsync(ulong guildId, ulong discordUserId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        await conn.TwitchAccountLinks
            .Where(l => l.GuildId == guildId && l.DiscordUserId == discordUserId)
            .DeleteAsync();
    }

    /// <summary>
    ///     Looks up the Discord user ID linked to a Twitch username in a guild.
    ///     Returns <see langword="null" /> if no link exists.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="twitchUsername">The Twitch login name to look up.</param>
    public async Task<ulong?> GetLinkedDiscordUserAsync(ulong guildId, string twitchUsername)
    {
        twitchUsername = twitchUsername.ToLowerInvariant();
        await using var conn = await dbFactory.CreateConnectionAsync();
        var link = await conn.TwitchAccountLinks
            .FirstOrDefaultAsync(l => l.GuildId == guildId && l.TwitchUsername == twitchUsername);
        return link?.DiscordUserId;
    }

    /// <summary>
    ///     Looks up the Twitch username linked to a Discord user in a guild.
    ///     Returns <see langword="null" /> if no link exists.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="discordUserId">The Discord user ID to look up.</param>
    public async Task<string?> GetLinkedTwitchUsernameAsync(ulong guildId, ulong discordUserId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var link = await conn.TwitchAccountLinks
            .FirstOrDefaultAsync(l => l.GuildId == guildId && l.DiscordUserId == discordUserId);
        return link?.TwitchUsername;
    }

    /// <summary>
    ///     Returns all Discord-to-Twitch account links for the given guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    public async Task<List<TwitchAccountLink>> GetAllLinksAsync(ulong guildId)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        return await conn.TwitchAccountLinks
            .Where(l => l.GuildId == guildId)
            .ToListAsync();
    }

    /// <summary>
    ///     Validates and normalizes a Twitch command prefix.
    /// </summary>
    public static string NormalizePrefix(string prefix)
    {
        prefix = prefix.Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be empty.", nameof(prefix));

        if (prefix.Length > 5)
            throw new ArgumentException("Prefix cannot be longer than 5 characters.", nameof(prefix));

        return prefix;
    }

    /// <summary>
    ///     Validates and normalizes a Twitch chat command name.
    /// </summary>
    public static string NormalizeCommandName(string name)
    {
        name = name.Trim().TrimStart('!').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command name cannot be empty.", nameof(name));

        if (name.Length > 32 || name.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
            throw new ArgumentException("Command names can only use letters, numbers, hyphens, and underscores.",
                nameof(name));

        return name;
    }

    private void StartLivePoll()
    {
        pollCts = new CancellationTokenSource();
        _ = Task.Run(() => LivePollLoopAsync(pollCts.Token));
    }

    private void StartTimerLoop()
    {
        if (timerCts is not null)
            return;

        timerCts = new CancellationTokenSource();
        _ = Task.Run(() => TimerLoopAsync(timerCts.Token));
    }

    private async Task TimerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessTimersAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing Twitch timers");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ProcessTimersAsync(CancellationToken ct)
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var timers = await conn.TwitchTimers
            .Where(x => x.Enabled)
            .ToListAsync(ct);

        if (timers.Count == 0)
            return;

        var guildIds = timers.Select(x => x.GuildId).Distinct().ToArray();
        var configs = await conn.TwitchGuildConfigs
            .Where(x => x.Enabled && guildIds.Contains(x.GuildId))
            .ToListAsync(ct);
        var configByGuild = configs.ToDictionary(x => x.GuildId);

        foreach (var timer in timers)
        {
            if (!configByGuild.TryGetValue(timer.GuildId, out var config))
                continue;

            if (timer.LastSentAt is not null &&
                timer.LastSentAt.Value.AddMinutes(timer.IntervalMinutes) > DateTime.UtcNow)
                continue;

            if (timer.OnlineOnly && !liveState.GetValueOrDefault(config.TwitchChannel, false))
                continue;

            var activityKey = GetSessionKey(timer.GuildId, config.TwitchChannel);
            var chatCount = chatActivityCounts.GetValueOrDefault(activityKey, 0);
            if (timer.MinChatMessages > 0 && chatCount - timer.LastChatMessageCount < timer.MinChatMessages)
                continue;

            var message = await RenderTimerMessageAsync(config, timer);
            if (string.IsNullOrWhiteSpace(message))
                continue;

            await SendMessageAsync(config.TwitchChannel, message);
            timer.LastSentAt = DateTime.UtcNow;
            timer.LastChatMessageCount = chatCount;
            timer.LastUpdatedAt = DateTime.UtcNow;
            await conn.UpdateAsync(timer, token: ct);
            await RecordEventHistoryAsync(timer.GuildId, "timer.sent", "timer", $"Sent timer {timer.Name}", true);
        }
    }

    private async Task LivePollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollLiveStateAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during Twitch live-state poll");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    private async Task PollLiveStateAsync()
    {
        if (helixApi is null || channelToGuild.IsEmpty) return;

        var logins = channelToGuild.Keys.ToList();
        if (logins.Count == 0) return;

        var token = await helixApi.Auth.GetAccessTokenAsync();
        if (token is null) return;

        var response = await helixApi.Helix.Streams.GetStreamsAsync(userLogins: logins);
        var liveLogins = new HashSet<string>(
            response.Streams.Select(s => s.UserLogin.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var login in logins)
        {
            var wasLive = liveState.GetValueOrDefault(login, false);
            var isLive = liveLogins.Contains(login);

            if (!wasLive && isLive)
            {
                liveState[login] = true;
                var stream = response.Streams.First(s =>
                    s.UserLogin.Equals(login, StringComparison.OrdinalIgnoreCase));

                if (StreamOnline is not null && channelToGuild.TryGetValue(login, out var guildId))
                {
                    sessionStats[GetSessionKey(guildId, login)] = new TwitchSessionStats
                    {
                        StartedAt = stream.StartedAt,
                        Title = stream.Title,
                        GameName = stream.GameName,
                        PeakViewers = stream.ViewerCount
                    };

                    await StreamOnline(new TwitchStreamOnlineArgs
                    {
                        BroadcasterUserId = stream.UserId,
                        BroadcasterUserLogin = stream.UserLogin,
                        BroadcasterUserName = stream.UserName,
                        StreamId = stream.Id,
                        StartedAt = stream.StartedAt,
                        Title = stream.Title,
                        GameName = stream.GameName,
                        ViewerCount = stream.ViewerCount,
                        ThumbnailUrl = stream.ThumbnailUrl,
                        GuildId = guildId
                    });
                }
            }
            else if (wasLive && isLive && channelToGuild.TryGetValue(login, out var guildId))
            {
                var stream = response.Streams.First(s =>
                    s.UserLogin.Equals(login, StringComparison.OrdinalIgnoreCase));
                var stats = sessionStats.GetOrAdd(GetSessionKey(guildId, login), _ => new TwitchSessionStats
                {
                    StartedAt = stream.StartedAt
                });
                stats.Title = stream.Title;
                stats.GameName = stream.GameName;
                stats.PeakViewers = Math.Max(stats.PeakViewers, stream.ViewerCount);
            }
            else if (wasLive && !isLive)
            {
                liveState[login] = false;

                if (StreamOffline is not null && channelToGuild.TryGetValue(login, out var offlineGuildId))
                {
                    await StreamOffline(new TwitchStreamOfflineArgs
                    {
                        BroadcasterUserLogin = login, BroadcasterUserName = login, GuildId = offlineGuildId
                    });
                }
            }
        }
    }

    private async Task<string> ExpandCommandVariablesAsync(TwitchCommandContext ctx, string response)
    {
        var args = string.Join(' ', ctx.Args);
        var target = ctx.Args.FirstOrDefault() ?? "";
        var discord = ctx.LinkedDiscordUserId.HasValue ? ctx.LinkedDiscordUserId.Value.ToString() : "";

        response = response
            .Replace("%user%", ctx.Username, StringComparison.OrdinalIgnoreCase)
            .Replace("%display%", ctx.DisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace("%channel%", ctx.TwitchChannel, StringComparison.OrdinalIgnoreCase)
            .Replace("%args%", args, StringComparison.OrdinalIgnoreCase)
            .Replace("%target%", target, StringComparison.OrdinalIgnoreCase)
            .Replace("%discord%", discord, StringComparison.OrdinalIgnoreCase);

        return await ExpandSharedTemplateTokensAsync(response, ctx.GuildId);
    }

    /// <summary>
    ///     Expands the %random:a|b|c%, %count:name%, and %stream% tokens shared by custom commands and timers.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="guildId">The guild ID used for counter and stream lookups.</param>
    /// <returns>The expanded text.</returns>
    private async Task<string> ExpandSharedTemplateTokensAsync(string text, ulong guildId)
    {
        text = Regex.Replace(text, @"%random:([^%]+)%", match =>
        {
            var options = match.Groups[1].Value
                .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return options.Length == 0 ? "" : options[Random.Shared.Next(options.Length)];
        }, RegexOptions.IgnoreCase);

        var counterMatches = Regex.Matches(text, @"%count:([a-zA-Z0-9_-]+)%", RegexOptions.IgnoreCase);
        foreach (Match match in counterMatches)
        {
            var counterName = match.Groups[1].Value;
            var value = await GetCounterAsync(guildId, counterName) ?? 0;
            text = text.Replace(match.Value, value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        if (text.Contains("%stream%", StringComparison.OrdinalIgnoreCase))
        {
            var summary = await GetStreamSummaryAsync(guildId) ?? "";
            text = text.Replace("%stream%", summary, StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    private static string? ApplyRedemptionVariables(string? template, TwitchChannelPointRedemptionArgs redemption)
    {
        if (string.IsNullOrWhiteSpace(template))
            return null;

        return template
            .Replace("%user%", redemption.UserLogin, StringComparison.OrdinalIgnoreCase)
            .Replace("%display%", redemption.UserName, StringComparison.OrdinalIgnoreCase)
            .Replace("%channel%", redemption.BroadcasterUserLogin, StringComparison.OrdinalIgnoreCase)
            .Replace("%reward%", redemption.RewardTitle, StringComparison.OrdinalIgnoreCase)
            .Replace("%input%", redemption.UserInput, StringComparison.OrdinalIgnoreCase)
            .Replace("%url%", $"https://twitch.tv/{redemption.BroadcasterUserLogin}",
                StringComparison.OrdinalIgnoreCase);
    }

    private async Task SyncLinkedUserRolesAsync(TwitchCommandContext ctx)
    {
        if (!ctx.LinkedDiscordUserId.HasValue)
            return;

        var guild = client.GetGuild(ctx.GuildId);
        var user = guild?.GetUser(ctx.LinkedDiscordUserId.Value);
        if (guild is null || user is null)
            return;

        var mappings = await GetRoleSyncMappingsAsync(ctx.GuildId);
        foreach (var mapping in mappings.Where(x => x.Enabled))
        {
            var shouldHave = (int)ctx.PermissionLevel >= mapping.PermissionLevel;
            var hasRole = user.Roles.Any(x => x.Id == mapping.RoleId);
            try
            {
                if (shouldHave && !hasRole)
                    await user.AddRoleAsync(mapping.RoleId);
                else if (!shouldHave && hasRole)
                    await user.RemoveRoleAsync(mapping.RoleId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to sync Twitch role {RoleId} for Discord user {UserId} in guild {GuildId}",
                    mapping.RoleId, user.Id, ctx.GuildId);
            }
        }
    }

    private void TrackChatMessage(TwitchCommandContext ctx)
    {
        var key = GetSessionKey(ctx.GuildId, ctx.TwitchChannel);
        chatActivityCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
        if (sessionStats.TryGetValue(key, out var stats))
            stats.ChatMessages++;
    }

    private async Task<string?> RenderTimerMessageAsync(TwitchGuildConfig config, TwitchTimer timer)
    {
        var messages = SplitTimerMessages(timer.Messages);
        if (messages.Length == 0)
            return null;

        var index = timer.RandomizeMessages
            ? Random.Shared.Next(messages.Length)
            : Math.Abs(timer.LastMessageIndex) % messages.Length;

        if (!timer.RandomizeMessages)
            timer.LastMessageIndex = (index + 1) % messages.Length;

        var message = messages[index]
            .Replace("%channel%", config.TwitchChannel, StringComparison.OrdinalIgnoreCase)
            .Replace("%url%", $"https://twitch.tv/{config.TwitchChannel}", StringComparison.OrdinalIgnoreCase);

        return await ExpandSharedTemplateTokensAsync(message, config.GuildId);
    }

    private static string NormalizeTimerMessages(string messages)
    {
        var normalized = string.Join('\n', SplitTimerMessages(messages));
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("At least one timer message is required.", nameof(messages));

        return normalized;
    }

    private static string[] SplitTimerMessages(string messages)
    {
        return messages
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static string[] GetMissingScopes(string? grantedScopes, IEnumerable<string> requiredScopes)
    {
        var granted = (grantedScopes ?? "")
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requiredScopes.Where(scope => !granted.Contains(scope)).ToArray();
    }

    private static string GetSessionKey(ulong guildId, string channel)
    {
        return $"{guildId}:{channel.TrimStart('#').ToLowerInvariant()}";
    }

    /// <summary>
    ///     Snapshot of Twitch integration health for a guild.
    /// </summary>
    public sealed class TwitchHealthSnapshot
    {
        /// <summary>Gets or sets whether a Twitch guild configuration exists.</summary>
        public bool HasConfig { get; set; }

        /// <summary>Gets or sets whether the Twitch integration is enabled.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets the configured Twitch channel.</summary>
        public string? TwitchChannel { get; set; }

        /// <summary>Gets or sets whether EventSub is enabled.</summary>
        public bool EventSubEnabled { get; set; }

        /// <summary>Gets or sets whether a bot account token is stored.</summary>
        public bool HasBotAccount { get; set; }

        /// <summary>Gets or sets whether a broadcaster channel token is stored.</summary>
        public bool HasChannelAuthorization { get; set; }

        /// <summary>Gets or sets bot account scopes that are missing.</summary>
        public string[] BotMissingScopes { get; set; } = [];

        /// <summary>Gets or sets broadcaster channel scopes that are missing.</summary>
        public string[] ChannelMissingScopes { get; set; } = [];

        /// <summary>Gets or sets when the bot token expires.</summary>
        public DateTime? BotTokenExpiresAt { get; set; }

        /// <summary>Gets or sets when the channel token expires.</summary>
        public DateTime? ChannelTokenExpiresAt { get; set; }

        /// <summary>Gets or sets when the latest EventSub event was processed.</summary>
        public DateTime? LastEventAt { get; set; }

        /// <summary>Gets or sets recent EventSub subscription records.</summary>
        public List<TwitchEventSubSubscription> Subscriptions { get; set; } = [];
    }

    private sealed class TwitchSessionStats
    {
        public DateTime StartedAt { get; set; }
        public string Title { get; set; } = "";
        public string GameName { get; set; } = "";
        public int PeakViewers { get; set; }
        public int ChatMessages { get; set; }
        public int Subs { get; set; }
        public int Raids { get; set; }
    }
}