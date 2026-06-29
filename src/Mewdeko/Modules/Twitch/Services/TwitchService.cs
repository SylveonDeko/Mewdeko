using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Twitch.Common;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;

namespace Mewdeko.Modules.Twitch.Services;

/// <summary>
///     Core Twitch integration service. Manages the IRC client connection, joins configured channels,
///     dispatches incoming chat commands, polls stream state via Helix, and raises events for
///     subscriptions, cheers, raids, and go-live/offline transitions.
/// </summary>
public class TwitchService : INService, IReadyExecutor
{
    private readonly ConcurrentDictionary<string, ulong> channelToGuild = new(StringComparer.OrdinalIgnoreCase);
    private readonly TwitchCommandHandler commandHandler;
    private readonly IBotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<string, bool> liveState = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<TwitchService> logger;
    private TwitchAPI? helixApi;

    private TwitchClient? ircClient;
    private CancellationTokenSource? pollCts;

    /// <summary>
    ///     Initializes the service with required dependencies.
    /// </summary>
    /// <param name="creds">Bot credentials providing Twitch client ID, secret, and IRC credentials.</param>
    /// <param name="db">Factory for creating database connections.</param>
    /// <param name="logger">Logger for this service.</param>
    /// <param name="commandHandler">Handler that dispatches Twitch chat commands.</param>
    public TwitchService(
        IBotCredentials creds,
        IDataConnectionFactory dbFactory,
        ILogger<TwitchService> logger,
        TwitchCommandHandler commandHandler)
    {
        this.creds = creds;
        this.dbFactory = dbFactory;
        this.logger = logger;
        this.commandHandler = commandHandler;
    }

    /// <summary>
    ///     Gets the Twitch bot account username from credentials, or an empty string if not configured.
    /// </summary>
    public string BotUsername
    {
        get
        {
            return creds.TwitchBotUsername ?? "";
        }
    }

    /// <summary>
    ///     Called when the Discord bot is ready. Starts the Twitch IRC client and live-state polling
    ///     if Twitch IRC credentials are configured.
    /// </summary>
    public async Task OnReadyAsync()
    {
        if (string.IsNullOrWhiteSpace(creds.TwitchBotUsername) ||
            string.IsNullOrWhiteSpace(creds.TwitchBotOAuthToken))
        {
            logger.LogInformation("TwitchBotUsername or TwitchBotOAuthToken not set; Twitch IRC disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(creds.TwitchClientId) ||
            string.IsNullOrWhiteSpace(creds.TwitchClientSecret))
        {
            logger.LogInformation("TwitchClientId or TwitchClientSecret not set; Twitch Helix API disabled");
            return;
        }

        await LoadConfigsAndConnectAsync();
        StartLivePoll();
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
    ///     Loads all enabled guild configs from the database, builds the channel-to-guild map,
    ///     initialises the IRC client, and joins all configured channels.
    /// </summary>
    public async Task LoadConfigsAndConnectAsync()
    {
        await using var conn = await dbFactory.CreateConnectionAsync();
        var configs = await conn.TwitchGuildConfigs
            .Where(c => c.Enabled)
            .ToListAsync();

        channelToGuild.Clear();
        foreach (var cfg in configs)
            channelToGuild[cfg.TwitchChannel] = cfg.GuildId;

        if (channelToGuild.IsEmpty)
        {
            logger.LogInformation("No Twitch channels configured; skipping IRC connect");
            return;
        }

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

        var credentials = new ConnectionCredentials(creds.TwitchBotUsername, creds.TwitchBotOAuthToken);
        var wsClient = new WebSocketClient();
        ircClient = new TwitchClient(wsClient);
        ircClient.Initialize(credentials, configs.Select(c => c.TwitchChannel).ToList());

        ircClient.OnMessageReceived += OnMessageReceived;
        ircClient.OnNewSubscriber += OnNewSubscriber;
        ircClient.OnReSubscriber += OnReSubscriber;
        ircClient.OnGiftedSubscription += OnGiftedSubscription;
        ircClient.OnRaidNotification += OnRaidNotification;
        ircClient.OnConnected += (_, _) => logger.LogInformation("Twitch IRC connected");
        ircClient.OnDisconnected += (_, _) => logger.LogWarning("Twitch IRC disconnected");
        ircClient.OnError += (_, e) => logger.LogError(e.Exception, "Twitch IRC error");

        ircClient.Connect();
        logger.LogInformation("Twitch IRC client connected; joined {Count} channel(s)", channelToGuild.Count);
    }

    /// <summary>
    ///     Joins a Twitch channel for the given guild and persists the config to the database.
    ///     If the IRC client is already running, joins immediately.
    /// </summary>
    /// <param name="guildId">The Discord guild ID that owns this config.</param>
    /// <param name="twitchChannel">Twitch channel name to join (leading # is stripped).</param>
    /// <param name="commandPrefix">Command prefix to use in this channel.</param>
    public async Task JoinChannelAsync(ulong guildId, string twitchChannel, string commandPrefix = "!")
    {
        twitchChannel = twitchChannel.ToLowerInvariant().TrimStart('#');

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

        if (ircClient is { IsConnected: true })
            ircClient.JoinChannel(twitchChannel);
        else if (ircClient is null)
            await LoadConfigsAndConnectAsync();

        logger.LogInformation("Guild {GuildId} joined Twitch channel #{Channel}", guildId, twitchChannel);
    }

    /// <summary>
    ///     Disables the Twitch config for the given guild and leaves the IRC channel.
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

        if (ircClient is { IsConnected: true })
            ircClient.LeaveChannel(channel);

        logger.LogInformation("Guild {GuildId} left Twitch channel #{Channel}", guildId, channel);
    }

    /// <summary>
    ///     Sends a chat message to a Twitch channel. The bot must have already joined the channel.
    /// </summary>
    /// <param name="channel">The Twitch channel name.</param>
    /// <param name="message">The message to send.</param>
    public void SendMessage(string channel, string message)
    {
        if (ircClient is not { IsConnected: true })
        {
            logger.LogWarning("Cannot send message to #{Channel}: IRC client not connected", channel);
            return;
        }

        ircClient.SendMessage(channel, message);
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

    private void StartLivePoll()
    {
        pollCts = new CancellationTokenSource();
        _ = Task.Run(() => LivePollLoopAsync(pollCts.Token));
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
                    await StreamOnline(new TwitchStreamOnlineArgs
                    {
                        BroadcasterUserId = stream.UserId,
                        BroadcasterUserLogin = stream.UserLogin,
                        BroadcasterUserName = stream.UserName,
                        StreamId = stream.Id,
                        StartedAt = stream.StartedAt,
                        GuildId = guildId
                    });
                }
            }
            else if (wasLive && !isLive)
            {
                liveState[login] = false;

                if (StreamOffline is not null && channelToGuild.TryGetValue(login, out var guildId))
                {
                    await StreamOffline(new TwitchStreamOfflineArgs
                    {
                        BroadcasterUserLogin = login, BroadcasterUserName = login, GuildId = guildId
                    });
                }
            }
        }
    }

    private async void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var msg = e.ChatMessage;
        if (!channelToGuild.TryGetValue(msg.Channel, out var guildId)) return;

        await using var conn = await dbFactory.CreateConnectionAsync();
        var config = await conn.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (config is null || !config.Enabled) return;

        var prefix = config.CommandPrefix;
        if (!msg.Message.StartsWith(prefix, StringComparison.Ordinal)) return;

        var ctx = new TwitchCommandContext(msg, guildId, prefix)
        {
            LinkedDiscordUserId = await GetLinkedDiscordUserAsync(guildId, msg.Username),
            ChannelLanguage = config.Language
        };

        await commandHandler.ExecuteAsync(ctx);
    }

    private async void OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
    {
        if (!channelToGuild.TryGetValue(e.Channel, out var guildId) || NewSub is null) return;

        await NewSub(new TwitchNewSubArgs
        {
            Channel = e.Channel,
            Username = e.Subscriber.Login,
            DisplayName = e.Subscriber.DisplayName,
            SubPlan = e.Subscriber.SubscriptionPlan.ToString(),
            IsGift = false,
            GuildId = guildId
        });
    }

    private async void OnReSubscriber(object? sender, OnReSubscriberArgs e)
    {
        if (!channelToGuild.TryGetValue(e.Channel, out var guildId) || NewSub is null) return;

        await NewSub(new TwitchNewSubArgs
        {
            Channel = e.Channel,
            Username = e.ReSubscriber.Login,
            DisplayName = e.ReSubscriber.DisplayName,
            SubPlan = e.ReSubscriber.SubscriptionPlan.ToString(),
            IsGift = false,
            GuildId = guildId
        });
    }

    private async void OnGiftedSubscription(object? sender, OnGiftedSubscriptionArgs e)
    {
        if (!channelToGuild.TryGetValue(e.Channel, out var guildId) || NewSub is null) return;

        await NewSub(new TwitchNewSubArgs
        {
            Channel = e.Channel,
            Username = e.GiftedSubscription.MsgParamRecipientUserName,
            DisplayName = e.GiftedSubscription.MsgParamRecipientDisplayName,
            SubPlan = e.GiftedSubscription.MsgParamSubPlan.ToString(),
            IsGift = true,
            GuildId = guildId
        });
    }

    private async void OnRaidNotification(object? sender, OnRaidNotificationArgs e)
    {
        if (!channelToGuild.TryGetValue(e.Channel, out var guildId) || Raid is null) return;

        if (!int.TryParse(e.RaidNotification.MsgParamViewerCount, out var viewers))
            viewers = 0;

        await Raid(new TwitchRaidArgs
        {
            Channel = e.Channel,
            RaiderDisplayName = e.RaidNotification.DisplayName,
            ViewerCount = viewers,
            GuildId = guildId
        });
    }
}