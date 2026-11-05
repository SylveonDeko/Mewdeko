using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using Discord.Net;
using Humanizer;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Bridge.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Strings;
using StackExchange.Redis;

namespace Mewdeko.Modules.Bridge.Services;

/// <summary>
///     Relays bridged channels to channels on Fluxer through the Redis streams both bots share.
/// </summary>
public partial class BridgeService : INService, IReadyExecutor, IUnloadableService, IBridgePairHost
{
    private const string ThisPlatform = BridgeProtocol.Discord;
    private const string WebhookName = "Mewdeko Bridge";
    private const int MaxFileBytes = 8 * 1024 * 1024;
    private const int MaxTotalFileBytes = 24 * 1024 * 1024;
    private const int MaxContentLength = 2000;
    private const int LaneCapacity = 50;
    private const int BurstMessages = 10;
    private static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MinClaimIdle = TimeSpan.FromSeconds(30);

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly Regex ReservedNames = ReservedNameRegex();

    private readonly ConcurrentDictionary<ulong, ChannelBridge> byChannel = new();
    private readonly ConcurrentDictionary<Guid, ChannelBridge> byId = new();
    private readonly ConcurrentDictionary<Guid, Lane> lanes = new();
    private readonly ConcurrentDictionary<ulong, DateTime> ownDeletes = new();
    private readonly CancellationTokenSource stopping = new();
    private readonly SemaphoreSlim wake = new(0, 1);

    private readonly DiscordShardedClient client;
    private readonly IDataCache cache;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ILogger<BridgeService> logger;
    private readonly GeneratedBotStrings strings;
    private readonly FilterService filterService;
    private readonly string consumer = $"{ThisPlatform}-{Environment.ProcessId}";
    private int started;
    private Task? readLoop;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BridgeService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="cache">The data cache that exposes the shared Redis connection.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="strings">The localized strings.</param>
    /// <param name="filterService">The filter service, for the guild's filtered words.</param>
    /// <param name="logger">The logger.</param>
    public BridgeService(DiscordShardedClient client, IDataCache cache, IDataConnectionFactory dbFactory,
        EventHandler eventHandler, GeneratedBotStrings strings, FilterService filterService,
        ILogger<BridgeService> logger)
    {
        this.filterService = filterService;
        this.client = client;
        this.cache = cache;
        this.dbFactory = dbFactory;
        this.eventHandler = eventHandler;
        this.strings = strings;
        this.logger = logger;
        eventHandler.Subscribe("MessageReceived", "BridgeService", OnMessageReceived);
        eventHandler.Subscribe("MessageUpdated", "BridgeService", OnMessageUpdated);
        eventHandler.Subscribe("MessageDeleted", "BridgeService", OnMessageDeleted);
        eventHandler.Subscribe("ReactionAdded", "BridgeService", OnReactionAdded);
        eventHandler.Subscribe("ReactionRemoved", "BridgeService", OnReactionRemoved);
        eventHandler.Subscribe("ChannelCreated", "BridgeService", OnChannelCreated);
        eventHandler.Subscribe("ChannelDestroyed", "BridgeService", OnChannelDestroyed);
        eventHandler.Subscribe("ChannelUpdated", "BridgeService", OnChannelUpdated);
        eventHandler.Subscribe("RoleUpdated", "BridgeService", OnRoleUpdated);
        Pairs = new BridgePairService(this, dbFactory, cache, strings, logger);
    }

    /// <summary>
    ///     Gets the pairs between communities on both platforms and the channel proposals inside them.
    /// </summary>
    public BridgePairService Pairs { get; }

    /// <summary>
    ///     Loads the bridges and starts reading the inbound stream.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        if (Interlocked.Exchange(ref started, 1) == 1)
            return;

        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            foreach (var bridge in await db.ChannelBridges.ToListAsync())
                Track(bridge);
        }

        try
        {
            await Pairs.InitializeAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load the bridge pairs");
        }

        try
        {
            await EnsureGroupAsync();
            var subscriber = cache.Redis.GetSubscriber();
            await subscriber.SubscribeAsync(RedisChannel.Literal(BridgeProtocol.WakeChannel(ThisPlatform)),
                (_, _) =>
                {
                    if (wake.CurrentCount == 0)
                    {
                        try
                        {
                            wake.Release();
                        }
                        catch (SemaphoreFullException)
                        {
                        }
                    }
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not start the bridge reader");
            return;
        }

        readLoop = Task.Run(() => ReadLoopAsync(stopping.Token));
    }

    /// <summary>
    ///     Stops the reader and the per-bridge workers.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Unload()
    {
        eventHandler.Unsubscribe("MessageReceived", "BridgeService", OnMessageReceived);
        eventHandler.Unsubscribe("MessageUpdated", "BridgeService", OnMessageUpdated);
        eventHandler.Unsubscribe("MessageDeleted", "BridgeService", OnMessageDeleted);
        eventHandler.Unsubscribe("ReactionAdded", "BridgeService", OnReactionAdded);
        eventHandler.Unsubscribe("ReactionRemoved", "BridgeService", OnReactionRemoved);
        eventHandler.Unsubscribe("ChannelCreated", "BridgeService", OnChannelCreated);
        eventHandler.Unsubscribe("ChannelDestroyed", "BridgeService", OnChannelDestroyed);
        eventHandler.Unsubscribe("ChannelUpdated", "BridgeService", OnChannelUpdated);
        eventHandler.Unsubscribe("RoleUpdated", "BridgeService", OnRoleUpdated);
        Pairs.Stop();
        await stopping.CancelAsync();
        foreach (var lane in lanes.Values)
            lane.Queue.Writer.TryComplete();
        if (readLoop is not null)
        {
            try
            {
                await readLoop.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>
    ///     Gets the bridges of a guild.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <returns>The guild's bridges.</returns>
    public IReadOnlyList<ChannelBridge> GetBridges(ulong guildId) =>
        byChannel.Values.Where(x => x.GuildId == guildId).OrderBy(x => x.DateAdded).ToList();

    /// <summary>
    ///     Gets the bridge on a channel.
    /// </summary>
    /// <param name="channelId">The channel.</param>
    /// <returns>The bridge, or null when the channel is not bridged.</returns>
    public ChannelBridge? GetBridge(ulong channelId) => byChannel.GetValueOrDefault(channelId);

    /// <summary>
    ///     Gets a guild's bridge by its id.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="bridgeId">The bridge id.</param>
    /// <returns>The bridge, or null when there is none.</returns>
    public ChannelBridge? GetBridge(ulong guildId, Guid bridgeId) =>
        byId.TryGetValue(bridgeId, out var bridge) && bridge.GuildId == guildId ? bridge : null;

    /// <summary>
    ///     Creates a link code for a channel.
    /// </summary>
    /// <param name="guild">The guild the channel is in.</param>
    /// <param name="channel">The channel to bridge.</param>
    /// <param name="userId">The user asking for the code.</param>
    /// <returns>The code and when it expires, or the reason it could not be made.</returns>
    public async Task<(BridgeError Error, string? Code, DateTimeOffset ExpiresAt)> CreateCodeAsync(
        IGuild guild, ITextChannel channel, ulong userId)
    {
        var error = await CheckAsync(guild, channel, userId);
        if (error != BridgeError.None)
            return (error, null, default);

        var code = BridgeProtocol.NewLinkCode();
        var request = new BridgeLinkCode
        {
            Code = code,
            Platform = ThisPlatform,
            GuildId = guild.Id,
            ChannelId = channel.Id,
            GuildName = guild.Name,
            ChannelName = channel.Name,
            CreatedByUserId = userId
        };

        await cache.Redis.GetDatabase().StringSetAsync(BridgeProtocol.LinkCodeKey(code),
            JsonSerializer.Serialize(request, BridgeProtocol.Json), BridgeProtocol.LinkCodeLifetime);
        return (BridgeError.None, code, DateTimeOffset.UtcNow + BridgeProtocol.LinkCodeLifetime);
    }

    /// <summary>
    ///     Accepts a link code created on the other platform and bridges the channel with it.
    /// </summary>
    /// <param name="guild">The guild the channel is in.</param>
    /// <param name="channel">The channel to bridge.</param>
    /// <param name="userId">The user accepting the code.</param>
    /// <param name="rawCode">The code as typed.</param>
    /// <returns>The new bridge, or the reason it could not be made.</returns>
    public async Task<(BridgeError Error, ChannelBridge? Bridge)> LinkAsync(IGuild guild, ITextChannel channel,
        ulong userId, string rawCode)
    {
        var error = await CheckAsync(guild, channel, userId);
        if (error != BridgeError.None)
            return (error, null);

        var code = BridgeProtocol.NormalizeCode(rawCode);
        if (code.Length == 0)
            return (BridgeError.InvalidCode, null);

        var redis = cache.Redis.GetDatabase();
        var raw = await redis.StringGetDeleteAsync(BridgeProtocol.LinkCodeKey(code));
        if (raw.IsNullOrEmpty)
            return (BridgeError.InvalidCode, null);

        BridgeLinkCode? request;
        try
        {
            request = JsonSerializer.Deserialize<BridgeLinkCode>((string)raw!, BridgeProtocol.Json);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null)
            return (BridgeError.InvalidCode, null);

        if (request.Platform != BridgeProtocol.Other(ThisPlatform))
        {
            await RestoreCodeAsync(code, request, raw);
            return (BridgeError.SamePlatform, null);
        }

        var url = await EnsureWebhookAsync(channel, null);
        if (url is null)
        {
            await RestoreCodeAsync(code, request, raw);
            return (BridgeError.WebhookFailed, null);
        }

        var bridge = NewBridge(Guid.NewGuid(), guild.Id, channel.Id, url, userId, request.GuildId, request.GuildName,
            request.ChannelId, request.ChannelName, null);

        await InsertAsync(bridge);
        await PublishAsync(new BridgeEnvelope
        {
            Type = BridgeEnvelopeType.LinkAccepted,
            BridgeId = bridge.BridgeId,
            Link = new BridgeLinkAccepted
            {
                Request = request,
                GuildId = guild.Id,
                ChannelId = channel.Id,
                GuildName = guild.Name,
                ChannelName = channel.Name,
                AcceptedByUserId = userId
            }
        });
        return (BridgeError.None, bridge);
    }

    /// <summary>
    ///     Pauses or resumes a bridge and tells the other side.
    /// </summary>
    /// <param name="bridge">The bridge.</param>
    /// <param name="paused">Whether it should be paused.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetPausedAsync(ChannelBridge bridge, bool paused)
    {
        bridge.Paused = paused;
        await UpdateAsync(bridge);
        await PublishAsync(new BridgeEnvelope
        {
            Type = BridgeEnvelopeType.PauseChanged, BridgeId = bridge.BridgeId, Paused = paused
        });
    }

    /// <summary>
    ///     Sets whether messages from bots are relayed.
    /// </summary>
    /// <param name="bridge">The bridge.</param>
    /// <param name="allow">Whether bots are relayed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetAllowBotsAsync(ChannelBridge bridge, bool allow)
    {
        bridge.AllowBots = allow;
        await UpdateAsync(bridge);
    }

    /// <summary>
    ///     Saves a bridge's options and, when the direction changed, tells the other side.
    /// </summary>
    /// <param name="bridge">The bridge.</param>
    /// <param name="options">The new options.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetOptionsAsync(ChannelBridge bridge, BridgeOptions options)
    {
        var directionChanged = bridge.Direction != (int)options.Direction;
        bridge.Apply(options);
        await UpdateAsync(bridge);
        if (directionChanged)
        {
            await PublishAsync(new BridgeEnvelope
            {
                Type = BridgeEnvelopeType.DirectionChanged, BridgeId = bridge.BridgeId, Direction = options.Direction
            });
        }
    }

    /// <summary>
    ///     Names a direction for people.
    /// </summary>
    /// <param name="guildId">The guild whose language is used.</param>
    /// <param name="direction">The direction.</param>
    /// <returns>The localized name.</returns>
    public string DirectionName(ulong guildId, BridgeDirection direction) => direction switch
    {
        BridgeDirection.SendOnly => strings.BridgeDirectionSend(guildId),
        BridgeDirection.ReceiveOnly => strings.BridgeDirectionReceive(guildId),
        _ => strings.BridgeDirectionBoth(guildId)
    };

    /// <summary>
    ///     Describes a minimum member age for people.
    /// </summary>
    /// <param name="minutes">The age in minutes.</param>
    /// <returns>The age as words.</returns>
    public static string DescribeMinAge(int minutes) =>
        TimeSpan.FromMinutes(minutes).Humanize(2, maxUnit: TimeUnit.Day);

    /// <summary>
    ///     Lists every option of a bridge for people.
    /// </summary>
    /// <param name="bridge">The bridge.</param>
    /// <returns>The text to show.</returns>
    public string DescribeOptions(ChannelBridge bridge)
    {
        var guildId = bridge.GuildId;
        var options = bridge.OptionsOf();
        string Roles(IReadOnlyList<ulong> ids, string empty) =>
            ids.Count == 0 ? empty : string.Join(' ', ids.Select(x => $"<@&{x}>"));
        string Toggle(bool on) => on ? strings.BridgeValueOn(guildId) : strings.BridgeValueOff(guildId);

        return strings.BridgeSettingsBody(guildId, DirectionName(guildId, options.Direction),
            Roles(options.AllowedRoleIds, strings.BridgeValueEveryone(guildId)),
            Roles(options.BlockedRoleIds, strings.BridgeValueNone(guildId)),
            options.BlockedUserIds.Count == 0
                ? strings.BridgeValueNone(guildId)
                : string.Join(' ', options.BlockedUserIds.Select(x => $"<@{x}>")),
            options.MinMemberAgeMinutes == 0
                ? strings.BridgeValueOff(guildId)
                : DescribeMinAge(options.MinMemberAgeMinutes),
            Toggle(options.BlockInvites), Toggle(options.UseWordFilter), options.NameFormat);
    }

    /// <summary>
    ///     Removes a bridge and tells the other side.
    /// </summary>
    /// <param name="bridge">The bridge.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RemoveAsync(ChannelBridge bridge)
    {
        await PublishAsync(new BridgeEnvelope
        {
            Type = BridgeEnvelopeType.Unlinked, BridgeId = bridge.BridgeId
        });
        await DropAsync(bridge);
    }

    private async Task<BridgeError> CheckAsync(IGuild guild, ITextChannel channel, ulong userId)
    {
        var user = await guild.GetUserAsync(userId);
        if (user is null || !(guild.OwnerId == userId || user.GuildPermissions.ManageGuild
                              || user.GuildPermissions.Administrator))
            return BridgeError.NoPermission;

        if (byChannel.ContainsKey(channel.Id))
            return BridgeError.AlreadyBridged;

        var me = await guild.GetCurrentUserAsync();
        if (me is null || !me.GetPermissions(channel).ManageWebhooks)
            return BridgeError.NeedWebhooks;

        return BridgeError.None;
    }

    private async Task RestoreCodeAsync(string code, BridgeLinkCode request, RedisValue raw)
    {
        var remaining = BridgeProtocol.LinkCodeLifetime - (DateTimeOffset.UtcNow - request.CreatedAt);
        if (remaining > TimeSpan.Zero)
            await cache.Redis.GetDatabase().StringSetAsync(BridgeProtocol.LinkCodeKey(code), raw, remaining);
    }

    private static ChannelBridge NewBridge(Guid bridgeId, ulong guildId, ulong channelId, string webhookUrl,
        ulong userId, ulong remoteGuildId, string remoteGuildName, ulong remoteChannelId, string remoteChannelName,
        Guid? pairId) => new()
    {
        BridgeId = bridgeId,
        GuildId = guildId,
        ChannelId = channelId,
        WebhookUrl = webhookUrl,
        RemoteGuildId = remoteGuildId,
        RemoteChannelId = remoteChannelId,
        RemoteGuildName = remoteGuildName,
        RemoteChannelName = remoteChannelName,
        PairId = pairId,
        CreatedBy = userId,
        DateAdded = DateTime.UtcNow
    };

    private void Track(ChannelBridge bridge)
    {
        byChannel[bridge.ChannelId] = bridge;
        byId[bridge.BridgeId] = bridge;
    }

    private async Task InsertAsync(ChannelBridge bridge)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.InsertAsync(bridge);
        Track(bridge);
    }

    private async Task UpdateAsync(ChannelBridge bridge)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(bridge);
    }

    private async Task DropAsync(ChannelBridge bridge)
    {
        byChannel.TryRemove(bridge.ChannelId, out _);
        byId.TryRemove(bridge.BridgeId, out _);
        if (lanes.TryRemove(bridge.BridgeId, out var lane))
            lane.Queue.Writer.TryComplete();

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.ChannelBridges.Where(x => x.BridgeId == bridge.BridgeId).DeleteAsync();

        try
        {
            var channel = client.GetGuild(bridge.GuildId)?.GetTextChannel(bridge.ChannelId);
            var id = WebhookIdOf(bridge.WebhookUrl);
            if (channel is not null && id != 0 && await channel.GetWebhookAsync(id) is { } hook)
                await hook.DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not delete the bridge webhook in channel {ChannelId}", bridge.ChannelId);
        }
    }

    private static ulong WebhookIdOf(string url)
    {
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.IndexOf(parts, "webhooks");
        return index >= 0 && index + 1 < parts.Length && ulong.TryParse(parts[index + 1], out var id) ? id : 0;
    }

    private async Task<string?> EnsureWebhookAsync(ITextChannel channel, ulong? exclude)
    {
        try
        {
            var hooks = await channel.GetWebhooksAsync();
            var hook = hooks.FirstOrDefault(x => x.Name == WebhookName && x.Token is not null
                                                  && x.Creator?.Id == client.CurrentUser.Id && x.Id != exclude)
                       ?? await channel.CreateWebhookAsync(WebhookName);
            return $"https://discord.com/api/webhooks/{hook.Id}/{hook.Token}";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not create the bridge webhook in channel {ChannelId}", channel.Id);
            return null;
        }
    }

    private async Task<bool> RecreateWebhookAsync(ChannelBridge bridge)
    {
        var channel = client.GetGuild(bridge.GuildId)?.GetTextChannel(bridge.ChannelId);
        if (channel is null)
            return false;

        if (!channel.Guild.CurrentUser.GetPermissions(channel).ManageWebhooks)
        {
            await PauseForPermissionsAsync(bridge);
            return false;
        }

        var url = await EnsureWebhookAsync(channel, WebhookIdOf(bridge.WebhookUrl));
        if (url is null)
            return false;

        bridge.WebhookUrl = url;
        await UpdateAsync(bridge);
        return true;
    }

    private async Task PauseForPermissionsAsync(ChannelBridge bridge)
    {
        if (bridge.Paused)
            return;

        try
        {
            await SetPausedAsync(bridge, true);
            if (client.GetGuild(bridge.GuildId)?.GetTextChannel(bridge.ChannelId) is { } channel)
                await channel.SendConfirmAsync(strings.BridgeNoticePausedPermissions(bridge.GuildId));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not pause bridge {BridgeId}", bridge.BridgeId);
        }
    }

    private async Task<T> WithWebhookAsync<T>(ChannelBridge bridge, Func<DiscordWebhookClient, Task<T>> action)
    {
        for (var attempt = 0;; attempt++)
        {
            try
            {
                using var hook = new DiscordWebhookClient(bridge.WebhookUrl);
                return await action(hook);
            }
            catch (HttpException ex) when (attempt == 0 &&
                                           ex.DiscordCode is DiscordErrorCode.UnknownWebhook
                                               or DiscordErrorCode.InvalidWebhookToken)
            {
                if (!await RecreateWebhookAsync(bridge))
                    throw;
            }
        }
    }

    private async Task PublishAsync(BridgeEnvelope envelope)
    {
        envelope.Origin = ThisPlatform;
        envelope.SentAt = DateTimeOffset.UtcNow;
        try
        {
            await cache.Redis.GetDatabase().StreamAddAsync(
                BridgeProtocol.InboundStream(BridgeProtocol.Other(ThisPlatform)), BridgeProtocol.StreamField,
                BridgeProtocol.Serialize(envelope), maxLength: BridgeProtocol.StreamMaxLength,
                useApproximateMaxLength: true);
            await cache.Redis.GetSubscriber().PublishAsync(
                RedisChannel.Literal(BridgeProtocol.WakeChannel(BridgeProtocol.Other(ThisPlatform))), "1");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not publish a {Type} bridge envelope", envelope.Type);
        }
    }

    [GeneratedRegex("discord|clyde", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedNameRegex();

    private sealed class Lane
    {
        public Channel<Func<Task>> Queue { get; } = Channel.CreateBounded<Func<Task>>(
            new BoundedChannelOptions(LaneCapacity)
            {
                SingleReader = true, FullMode = BoundedChannelFullMode.Wait
            });

        public Queue<DateTime> Stamps { get; } = new();
    }
}

/// <summary>
///     Why a bridge operation was refused.
/// </summary>
public enum BridgeError
{
    /// <summary>It worked.</summary>
    None,

    /// <summary>The user may not manage bridges here.</summary>
    NoPermission,

    /// <summary>The bot may not manage webhooks in the channel.</summary>
    NeedWebhooks,

    /// <summary>The channel already has a bridge.</summary>
    AlreadyBridged,

    /// <summary>The code is unknown or expired.</summary>
    InvalidCode,

    /// <summary>The code was made on this platform.</summary>
    SamePlatform,

    /// <summary>The webhook could not be created.</summary>
    WebhookFailed,

    /// <summary>The channel does not exist.</summary>
    ChannelNotFound,

    /// <summary>The channel is not bridged.</summary>
    NotBridged
}
