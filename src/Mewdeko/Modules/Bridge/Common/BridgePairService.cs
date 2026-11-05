using System.Text;
using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Services.Strings;
using StackExchange.Redis;

namespace Mewdeko.Modules.Bridge.Common;

/// <summary>
///     What the pair logic needs from the bot it runs inside.
/// </summary>
public interface IBridgePairHost
{
    /// <summary>The platform this bot is on.</summary>
    string Platform { get; }

    /// <summary>Whether the bot is in a community.</summary>
    /// <param name="guildId">The community.</param>
    /// <returns>True when the bot can see it.</returns>
    bool HasGuild(ulong guildId);

    /// <summary>Gets a community's name.</summary>
    /// <param name="guildId">The community.</param>
    /// <returns>The name, or empty.</returns>
    string GuildName(ulong guildId);

    /// <summary>Lists the text channels of a community in display order.</summary>
    /// <param name="guildId">The community.</param>
    /// <returns>The channels.</returns>
    IReadOnlyList<BridgeChannelInfo> TextChannels(ulong guildId);

    /// <summary>Whether a channel already has a bridge.</summary>
    /// <param name="channelId">The channel.</param>
    /// <returns>True when it is bridged.</returns>
    bool IsBridged(ulong channelId);

    /// <summary>Whether the bot may manage webhooks in a channel.</summary>
    /// <param name="guildId">The community.</param>
    /// <param name="channelId">The channel.</param>
    /// <returns>True when it may.</returns>
    bool CanManageWebhooks(ulong guildId, ulong channelId);

    /// <summary>Whether a user may manage bridges in a community.</summary>
    /// <param name="guildId">The community.</param>
    /// <param name="userId">The user.</param>
    /// <returns>True when they may.</returns>
    Task<bool> CanManageAsync(ulong guildId, ulong userId);

    /// <summary>Gets the bridges that were made through a pair.</summary>
    /// <param name="pairId">The pair.</param>
    /// <returns>The bridges on this side.</returns>
    IReadOnlyList<ChannelBridge> BridgesOfPair(Guid pairId);

    /// <summary>Creates the webhook and the stored bridge for a channel, without telling the other side.</summary>
    /// <param name="guildId">The community.</param>
    /// <param name="channelId">The channel to bridge.</param>
    /// <param name="userId">The user the bridge is made for.</param>
    /// <param name="pairId">The pair it is made through.</param>
    /// <param name="remoteGuildId">The community on the other platform.</param>
    /// <param name="remoteGuildName">Its name.</param>
    /// <param name="remoteChannelId">The channel on the other platform.</param>
    /// <param name="remoteChannelName">Its name.</param>
    /// <returns>The new bridge, or null when the webhook could not be made.</returns>
    Task<ChannelBridge?> CreateBridgeAsync(ulong guildId, ulong channelId, ulong userId, Guid pairId,
        ulong remoteGuildId, string remoteGuildName, ulong remoteChannelId, string remoteChannelName);

    /// <summary>Removes a bridge and tells the other side.</summary>
    /// <param name="bridge">The bridge.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveBridgeAsync(ChannelBridge bridge);

    /// <summary>Removes a bridge the other side already removed, posting the removal notice.</summary>
    /// <param name="bridge">The bridge.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UnlinkLocalAsync(ChannelBridge bridge);

    /// <summary>Clears the pair from every bridge made through it.</summary>
    /// <param name="pairId">The pair.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ClearPairAsync(Guid pairId);

    /// <summary>Sends an envelope to the other platform.</summary>
    /// <param name="envelope">The envelope.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task PublishAsync(BridgeEnvelope envelope);

    /// <summary>Posts a notice in a channel, ignoring failures.</summary>
    /// <param name="guildId">The community.</param>
    /// <param name="channelId">The channel.</param>
    /// <param name="text">The text.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task NoticeAsync(ulong guildId, ulong channelId, string text);

    /// <summary>Posts the bridge created notice in the channel of a new bridge.</summary>
    /// <param name="bridge">The bridge.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task NoticeLinkedAsync(ChannelBridge bridge);
}

/// <summary>
///     A text channel as the pair logic sees it.
/// </summary>
/// <param name="Id">The channel id.</param>
/// <param name="Name">The channel name.</param>
/// <param name="Category">The category name, or empty.</param>
/// <param name="VisibleToEveryone">Whether the community's @everyone role can view the channel.</param>
public sealed record BridgeChannelInfo(ulong Id, string Name, string Category, bool VisibleToEveryone);

/// <summary>
///     Why a pair operation was refused.
/// </summary>
public enum BridgePairError
{
    /// <summary>It worked.</summary>
    None,

    /// <summary>The user may not manage bridges here.</summary>
    NoPermission,

    /// <summary>The pair, proposal or channel does not exist.</summary>
    NotFound,

    /// <summary>The code is unknown or expired.</summary>
    CodeInvalid,

    /// <summary>The code was made on this platform.</summary>
    CodeWrongPlatform,

    /// <summary>The communities are already paired.</summary>
    AlreadyPaired,

    /// <summary>The other side does not share that channel.</summary>
    ChannelNotShared,

    /// <summary>A channel already has a bridge.</summary>
    AlreadyBridged,

    /// <summary>The bot may not manage webhooks in the channel.</summary>
    MissingPermission,

    /// <summary>The request is incomplete or malformed.</summary>
    InvalidRequest
}

/// <summary>
///     Helpers for <see cref="BridgePairError" />.
/// </summary>
public static class BridgePairErrors
{
    /// <summary>Gets the error code the API reports.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The snake case code.</returns>
    public static string Code(this BridgePairError error) => error switch
    {
        BridgePairError.NotFound => "not_found",
        BridgePairError.CodeInvalid => "code_invalid",
        BridgePairError.CodeWrongPlatform => "code_wrong_platform",
        BridgePairError.AlreadyPaired => "already_paired",
        BridgePairError.ChannelNotShared => "channel_not_shared",
        BridgePairError.AlreadyBridged => "already_bridged",
        BridgePairError.MissingPermission => "missing_permission",
        BridgePairError.NoPermission => "no_permission",
        _ => "invalid_request"
    };
}

/// <summary>
///     What a pair command answers with.
/// </summary>
/// <param name="Success">Whether it worked.</param>
/// <param name="Text">The text to show.</param>
/// <param name="Title">A title when the answer is a list, otherwise null.</param>
public sealed record BridgePairReply(bool Success, string Text, string? Title = null);

/// <summary>
///     Pairs communities across the two platforms, shares channel lists and runs channel proposals. The Discord and
///     Fluxer bots carry an identical copy and supply the platform parts through <see cref="IBridgePairHost" />.
/// </summary>
public sealed class BridgePairService
{
    private const int ListLimit = 3800;
    private const string ApproveName = "Approve";
    private const string AutoName = "Auto";

    private readonly IBridgePairHost host;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IDataCache cache;
    private readonly GeneratedBotStrings strings;
    private readonly ILogger logger;
    private readonly ConcurrentDictionary<ulong, byte> pairedGuilds = new();
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> debounces = new();
    private readonly ConcurrentDictionary<Guid, string> published = new();
    private readonly ConcurrentDictionary<Guid, RemoteList> remoteLists = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="BridgePairService" /> class.
    /// </summary>
    /// <param name="host">The bot the service runs inside.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="cache">The data cache that exposes the shared Redis connection.</param>
    /// <param name="strings">The localized strings.</param>
    /// <param name="logger">The logger.</param>
    public BridgePairService(IBridgePairHost host, IDataConnectionFactory dbFactory, IDataCache cache,
        GeneratedBotStrings strings, ILogger logger)
    {
        this.host = host;
        this.dbFactory = dbFactory;
        this.cache = cache;
        this.strings = strings;
        this.logger = logger;
    }

    private IDatabase Redis => cache.Redis.GetDatabase();

    private string Other => BridgeProtocol.Other(host.Platform);

    #region Setup

    /// <summary>
    ///     Loads which communities are paired and publishes their channel lists.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InitializeAsync()
    {
        await PurgeExpiredAsync();
        await using var db = await dbFactory.CreateConnectionAsync();
        foreach (var guildId in await db.BridgePairs.Select(x => x.GuildId).Distinct().ToListAsync())
        {
            pairedGuilds[guildId] = 0;
            try
            {
                await PublishGuildAsync(guildId, false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not publish the shared channels of {GuildId}", guildId);
            }
        }
    }

    /// <summary>
    ///     Stops pending channel list publishes.
    /// </summary>
    public void Stop()
    {
        foreach (var source in debounces.Values)
            source.Cancel();
    }

    /// <summary>
    ///     Republishes a community's shared channels a few seconds from now when it has a pair. Call it when channels or
    ///     the permissions of @everyone change.
    /// </summary>
    /// <param name="guildId">The community.</param>
    public void ChannelsChanged(ulong guildId)
    {
        if (!pairedGuilds.ContainsKey(guildId))
            return;

        var source = new CancellationTokenSource();
        if (debounces.TryRemove(guildId, out var previous))
            previous.Cancel();

        debounces[guildId] = source;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(BridgeProtocol.ChannelsDebounce, source.Token);
                await PublishGuildAsync(guildId, true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not publish the shared channels of {GuildId}", guildId);
            }
        });
    }

    #endregion

    #region Pairs

    /// <summary>
    ///     Creates a pair code for a community.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="userId">The user asking.</param>
    /// <returns>The code and when it expires, or the reason it could not be made.</returns>
    public async Task<(BridgePairError Error, string? Code, DateTimeOffset ExpiresAt)> CreateCodeAsync(ulong guildId,
        ulong userId)
    {
        if (!host.HasGuild(guildId))
            return (BridgePairError.NotFound, null, default);

        if (!await host.CanManageAsync(guildId, userId))
            return (BridgePairError.NoPermission, null, default);

        var request = new BridgePairCode
        {
            Platform = host.Platform, GuildId = guildId, GuildName = host.GuildName(guildId), CreatedByUserId = userId
        };

        for (var attempt = 0; attempt < 5; attempt++)
        {
            request.Code = BridgeProtocol.NewLinkCode();
            request.CreatedAt = DateTimeOffset.UtcNow;
            request.ExpiresAt = request.CreatedAt + BridgeProtocol.PairCodeLifetime;
            if (await Redis.StringSetAsync(BridgeProtocol.PairCodeKey(request.Code),
                    JsonSerializer.Serialize(request, BridgeProtocol.Json), BridgeProtocol.PairCodeLifetime,
                    When.NotExists))
                return (BridgePairError.None, request.Code, request.ExpiresAt);
        }

        return (BridgePairError.CodeInvalid, null, default);
    }

    /// <summary>
    ///     Accepts a pair code made on the other platform.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="userId">The user accepting.</param>
    /// <param name="rawCode">The code as typed.</param>
    /// <returns>The new pair, or the reason it could not be made.</returns>
    public async Task<(BridgePairError Error, BridgePair? Pair)> AcceptCodeAsync(ulong guildId, ulong userId,
        string rawCode)
    {
        if (!host.HasGuild(guildId))
            return (BridgePairError.NotFound, null);

        if (!await host.CanManageAsync(guildId, userId))
            return (BridgePairError.NoPermission, null);

        var code = BridgeProtocol.NormalizeCode(rawCode);
        if (code.Length == 0)
            return (BridgePairError.CodeInvalid, null);

        var key = BridgeProtocol.PairCodeKey(code);
        var raw = await Redis.StringGetDeleteAsync(key);
        if (raw.IsNullOrEmpty)
            return (BridgePairError.CodeInvalid, null);

        BridgePairCode? request;
        try
        {
            request = JsonSerializer.Deserialize<BridgePairCode>((string)raw!, BridgeProtocol.Json);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null)
            return (BridgePairError.CodeInvalid, null);

        if (request.Platform != Other)
        {
            await RestoreCodeAsync(key, request, raw);
            return (BridgePairError.CodeWrongPlatform, null);
        }

        var pair = new BridgePair
        {
            PairId = Guid.NewGuid(),
            GuildId = guildId,
            RemoteGuildId = request.GuildId,
            RemoteGuildName = request.GuildName,
            CreatedBy = userId,
            DateAdded = DateTime.UtcNow
        };

        if (!await TryInsertPairAsync(pair))
        {
            await RestoreCodeAsync(key, request, raw);
            return (BridgePairError.AlreadyPaired, null);
        }

        await host.PublishAsync(new BridgeEnvelope
        {
            Type = BridgeEnvelopeType.PairAccepted,
            PairId = pair.PairId,
            Pair = new BridgePairAccepted
            {
                Request = request,
                GuildId = guildId,
                GuildName = host.GuildName(guildId),
                AcceptedByUserId = userId
            }
        });
        await PublishGuildAsync(guildId, false);
        return (BridgePairError.None, pair);
    }

    /// <summary>
    ///     Gets the pairs of a community, oldest first.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <returns>The pairs.</returns>
    public async Task<List<BridgePair>> GetPairsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.BridgePairs.Where(x => x.GuildId == guildId).OrderBy(x => x.DateAdded).ThenBy(x => x.Id)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets one pair of a community.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="pairId">The pair.</param>
    /// <returns>The pair, or null when there is none.</returns>
    public async Task<BridgePair?> GetPairAsync(ulong guildId, Guid pairId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.BridgePairs.FirstOrDefaultAsync(x => x.GuildId == guildId && x.PairId == pairId);
    }

    /// <summary>
    ///     Builds the dashboard view of a pair.
    /// </summary>
    /// <param name="pair">The pair.</param>
    /// <returns>The pair with its channels and proposals.</returns>
    public async Task<BridgePairResponse> DescribeAsync(BridgePair pair)
    {
        await PurgeExpiredAsync();
        var hidden = BridgeSettings.ParseIds(pair.HiddenChannelIds);
        var local = host.TextChannels(pair.GuildId);
        var names = local.ToDictionary(x => x.Id, x => x.Name);
        var remote = await RemoteChannelsAsync(pair);

        List<BridgeProposal> proposals;
        await using (var db = await dbFactory.CreateConnectionAsync())
            proposals = await db.BridgeProposals.Where(x => x.PairId == pair.PairId).OrderBy(x => x.DateAdded)
                .ToListAsync();

        BridgeProposalResponse Map(BridgeProposal x) => new()
        {
            ProposalId = x.ProposalId,
            LocalChannelId = x.LocalChannelId,
            LocalChannelName = names.GetValueOrDefault(x.LocalChannelId, ""),
            RemoteChannelId = x.RemoteChannelId,
            RemoteChannelName = x.RemoteChannelName,
            DateAdded = x.DateAdded
        };

        return new BridgePairResponse
        {
            PairId = pair.PairId,
            RemoteGuildId = pair.RemoteGuildId,
            RemoteGuildName = pair.RemoteGuildName,
            ApprovalMode = ModeName(pair.ApprovalMode),
            HiddenChannelIds = hidden.ToArray(),
            LocalChannels = local.Select(x => new BridgeLocalChannelResponse
            {
                Id = x.Id, Name = x.Name, Category = x.Category,
                Shared = x.VisibleToEveryone && !hidden.Contains(x.Id)
            }).ToList(),
            RemoteChannels = remote.Select(x => new BridgeRemoteChannelResponse
            {
                Id = x.Id, Name = x.Name, Category = x.Category
            }).ToList(),
            IncomingProposals = proposals.Where(x => x.Incoming).Select(Map).ToList(),
            OutgoingProposals = proposals.Where(x => !x.Incoming).Select(Map).ToList(),
            DateAdded = pair.DateAdded
        };
    }

    /// <summary>
    ///     Replaces a pair's approval mode and hidden channels.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="pairId">The pair.</param>
    /// <param name="approvalMode"><c>Approve</c> or <c>Auto</c>.</param>
    /// <param name="hiddenChannelIds">The channels to hide.</param>
    /// <returns>The updated pair, or the reason it could not be changed.</returns>
    public async Task<(BridgePairError Error, BridgePair? Pair)> SetSettingsAsync(ulong guildId, Guid pairId,
        string? approvalMode, IEnumerable<ulong>? hiddenChannelIds)
    {
        if (await GetPairAsync(guildId, pairId) is not { } pair)
            return (BridgePairError.NotFound, null);

        if (!TryParseMode(approvalMode, out var mode) || hiddenChannelIds is null)
            return (BridgePairError.InvalidRequest, null);

        pair.ApprovalMode = mode;
        pair.HiddenChannelIds = BridgeSettings.JoinIds(hiddenChannelIds.Where(x => x != 0));
        await SavePairAsync(pair);
        await PublishGuildAsync(guildId, true);
        return (BridgePairError.None, pair);
    }

    /// <summary>
    ///     Removes a pair, and the bridges made through it unless they are kept, and tells the other side.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="pairId">The pair.</param>
    /// <param name="keepBridges">Whether the bridges made through the pair stay.</param>
    /// <returns>The reason it could not be removed, or none.</returns>
    public async Task<BridgePairError> UnpairAsync(ulong guildId, Guid pairId, bool keepBridges)
    {
        if (await GetPairAsync(guildId, pairId) is not { } pair)
            return BridgePairError.NotFound;

        await RemovePairAsync(pair, keepBridges, false);
        await host.PublishAsync(new BridgeEnvelope
        {
            Type = BridgeEnvelopeType.Unpaired, PairId = pairId, KeepBridges = keepBridges
        });
        return BridgePairError.None;
    }

    #endregion

    #region Channels

    /// <summary>
    ///     Gets the channels the other community shares with this one.
    /// </summary>
    /// <param name="pair">The pair.</param>
    /// <returns>The shared channels, empty when the other side has published none.</returns>
    public async Task<List<BridgeSharedChannel>> RemoteChannelsAsync(BridgePair pair)
    {
        if (remoteLists.TryGetValue(pair.PairId, out var kept)
            && DateTime.UtcNow - kept.At < BridgeProtocol.ChannelsCacheLifetime)
            return kept.Channels;

        var channels = new List<BridgeSharedChannel>();
        try
        {
            var value = await Redis.StringGetAsync(
                BridgeProtocol.ChannelsKey(Other, pair.RemoteGuildId, pair.PairId));
            if (value.HasValue)
                channels = JsonSerializer.Deserialize<List<BridgeSharedChannel>>((string)value!, BridgeProtocol.Json)
                           ?? [];
        }
        catch (Exception ex) when (ex is JsonException or RedisException)
        {
            logger.LogDebug(ex, "Could not read the channels shared by pair {PairId}", pair.PairId);
        }

        remoteLists[pair.PairId] = new RemoteList(DateTime.UtcNow, channels);
        return channels;
    }

    /// <summary>
    ///     Shares or hides a channel for one pair.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="pairId">The pair.</param>
    /// <param name="channelId">The channel.</param>
    /// <param name="share">True to share it, false to hide it.</param>
    /// <returns>The reason it could not be changed, or none.</returns>
    public async Task<BridgePairError> SetSharedAsync(ulong guildId, Guid pairId, ulong channelId, bool share)
    {
        if (await GetPairAsync(guildId, pairId) is not { } pair)
            return BridgePairError.NotFound;

        if (host.TextChannels(guildId).All(x => x.Id != channelId))
            return BridgePairError.InvalidRequest;

        var hidden = BridgeSettings.ParseIds(pair.HiddenChannelIds);
        if (share)
            hidden.Remove(channelId);
        else if (!hidden.Contains(channelId))
            hidden.Add(channelId);

        pair.HiddenChannelIds = BridgeSettings.JoinIds(hidden);
        await SavePairAsync(pair);
        await PublishGuildAsync(guildId, true);
        return BridgePairError.None;
    }

    private async Task PublishGuildAsync(ulong guildId, bool notify)
    {
        if (!host.HasGuild(guildId))
            return;

        var pairs = await GetPairsAsync(guildId);
        var channels = host.TextChannels(guildId);
        foreach (var pair in pairs)
        {
            var hidden = BridgeSettings.ParseIds(pair.HiddenChannelIds);
            var shared = channels.Where(x => x.VisibleToEveryone && !hidden.Contains(x.Id))
                .Select(x => new BridgeSharedChannel
                {
                    Id = x.Id, Name = x.Name, Category = x.Category
                }).ToList();
            var json = JsonSerializer.Serialize(shared, BridgeProtocol.Json);
            var changed = !published.TryGetValue(pair.PairId, out var previous) || previous != json;

            await Redis.StringSetAsync(BridgeProtocol.ChannelsKey(host.Platform, guildId, pair.PairId), json);
            await Redis.StringSetAsync(BridgeProtocol.PairModeKey(host.Platform, guildId, pair.PairId),
                pair.ApprovalMode.ToString());
            published[pair.PairId] = json;

            if (notify && changed)
            {
                await host.PublishAsync(new BridgeEnvelope
                {
                    Type = BridgeEnvelopeType.ChannelsUpdated, PairId = pair.PairId
                });
            }
        }
    }

    private HashSet<ulong> SharedIds(BridgePair pair)
    {
        var hidden = BridgeSettings.ParseIds(pair.HiddenChannelIds);
        return host.TextChannels(pair.GuildId).Where(x => x.VisibleToEveryone && !hidden.Contains(x.Id))
            .Select(x => x.Id).ToHashSet();
    }

    private async Task<bool> RemoteIsAutomaticAsync(BridgePair pair)
    {
        try
        {
            var value = await Redis.StringGetAsync(BridgeProtocol.PairModeKey(Other, pair.RemoteGuildId, pair.PairId));
            return value.HasValue && value.ToString() == "1";
        }
        catch (RedisException)
        {
            return false;
        }
    }

    #endregion

    #region Proposals

    /// <summary>
    ///     Gets the pending proposals of a community: those it has to decide on first, then those it made.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <returns>The proposals with the pair each belongs to.</returns>
    public async Task<List<(BridgeProposal Proposal, BridgePair Pair)>> GetProposalsAsync(ulong guildId)
    {
        await PurgeExpiredAsync();
        var pairs = await GetPairsAsync(guildId);
        var ids = pairs.Select(x => x.PairId).ToList();
        if (ids.Count == 0)
            return [];

        await using var db = await dbFactory.CreateConnectionAsync();
        var rows = await db.BridgeProposals.Where(x => ids.Contains(x.PairId)).OrderBy(x => x.DateAdded)
            .ToListAsync();
        return rows.OrderByDescending(x => x.Incoming).ThenBy(x => x.DateAdded)
            .Select(x => (x, pairs.First(p => p.PairId == x.PairId))).ToList();
    }

    /// <summary>
    ///     Proposes bridging one of this community's channels to a channel the other side shares. When the other side
    ///     accepts automatically the handshake follows on its own.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="pairId">The pair.</param>
    /// <param name="localChannelId">The channel in this community.</param>
    /// <param name="remoteChannelId">The shared channel on the other side.</param>
    /// <param name="userId">The user asking.</param>
    /// <returns>The reason it could not be proposed, or the proposal and whether the other side connects automatically.</returns>
    public async Task<(BridgePairError Error, Guid? ProposalId, bool Connected)> ConnectAsync(ulong guildId,
        Guid pairId, ulong localChannelId, ulong remoteChannelId, ulong userId)
    {
        if (await GetPairAsync(guildId, pairId) is not { } pair)
            return (BridgePairError.NotFound, null, false);

        if (!await host.CanManageAsync(guildId, userId))
            return (BridgePairError.NoPermission, null, false);

        var local = host.TextChannels(guildId).FirstOrDefault(x => x.Id == localChannelId);
        if (local is null)
            return (BridgePairError.InvalidRequest, null, false);

        var remote = (await RemoteChannelsAsync(pair)).FirstOrDefault(x => x.Id == remoteChannelId);
        if (remote is null)
            return (BridgePairError.ChannelNotShared, null, false);

        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            if (host.IsBridged(localChannelId)
                || await db.ChannelBridges.AnyAsync(x => x.GuildId == guildId && x.RemoteGuildId == pair.RemoteGuildId
                                                         && x.RemoteChannelId == remoteChannelId))
                return (BridgePairError.AlreadyBridged, null, false);
        }

        if (!host.CanManageWebhooks(guildId, localChannelId))
            return (BridgePairError.MissingPermission, null, false);

        var automatic = await RemoteIsAutomaticAsync(pair);
        var proposal = new BridgeProposal
        {
            ProposalId = Guid.NewGuid(),
            PairId = pairId,
            Incoming = false,
            LocalChannelId = localChannelId,
            RemoteChannelId = remoteChannelId,
            RemoteChannelName = remote.Name,
            ProposedBy = userId,
            DateAdded = DateTime.UtcNow
        };

        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            await db.BridgeProposals.Where(x => x.PairId == pairId && !x.Incoming
                                                                    && x.LocalChannelId == localChannelId
                                                                    && x.RemoteChannelId == remoteChannelId)
                .DeleteAsync();
            await db.InsertAsync(proposal);
        }

        await host.PublishAsync(new BridgeEnvelope
        {
            Type = BridgeEnvelopeType.BridgeProposed,
            PairId = pairId,
            ProposalId = proposal.ProposalId,
            Proposal = new BridgeProposalInfo
            {
                ChannelId = localChannelId,
                ChannelName = local.Name,
                TargetChannelId = remoteChannelId,
                GuildName = host.GuildName(guildId),
                ProposedByUserId = userId
            }
        });

        return (BridgePairError.None, proposal.ProposalId, automatic);
    }

    /// <summary>
    ///     Accepts a proposal the other side made.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="proposalId">The proposal.</param>
    /// <param name="userId">The user accepting.</param>
    /// <param name="pairId">The pair the proposal must belong to, or null to not check.</param>
    /// <returns>The reason it could not be accepted, or none.</returns>
    public async Task<BridgePairError> AcceptProposalAsync(ulong guildId, Guid proposalId, ulong userId,
        Guid? pairId = null)
    {
        if (!await host.CanManageAsync(guildId, userId))
            return BridgePairError.NoPermission;

        await PurgeExpiredAsync();
        if (await FindProposalAsync(guildId, proposalId, true, pairId) is not var (proposal, pair))
            return BridgePairError.NotFound;

        return await AcceptCoreAsync(pair, proposal, userId);
    }

    /// <summary>
    ///     Declines a proposal the other side made.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="proposalId">The proposal.</param>
    /// <param name="pairId">The pair the proposal must belong to, or null to not check.</param>
    /// <returns>The reason it could not be declined, or none.</returns>
    public async Task<BridgePairError> DeclineProposalAsync(ulong guildId, Guid proposalId, Guid? pairId = null)
    {
        await PurgeExpiredAsync();
        if (await FindProposalAsync(guildId, proposalId, true, pairId) is not var (proposal, pair))
            return BridgePairError.NotFound;

        await DeleteProposalAsync(proposal.ProposalId);
        await DeclineAsync(pair, proposal.ProposalId, "declined");
        return BridgePairError.None;
    }

    /// <summary>
    ///     Withdraws a proposal this community made.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="proposalId">The proposal.</param>
    /// <param name="pairId">The pair the proposal must belong to, or null to not check.</param>
    /// <returns>The reason it could not be cancelled, or none.</returns>
    public async Task<BridgePairError> CancelProposalAsync(ulong guildId, Guid proposalId, Guid? pairId = null)
    {
        await PurgeExpiredAsync();
        if (await FindProposalAsync(guildId, proposalId, false, pairId) is not var (proposal, pair))
            return BridgePairError.NotFound;

        await DeleteProposalAsync(proposal.ProposalId);
        await host.PublishAsync(new BridgeEnvelope
        {
            Type = BridgeEnvelopeType.BridgeProposalCancelled, PairId = pair.PairId, ProposalId = proposal.ProposalId
        });
        return BridgePairError.None;
    }

    /// <summary>
    ///     Removes the record of a proposal this side made once the other side accepted it.
    /// </summary>
    /// <param name="proposalId">The proposal.</param>
    /// <returns>True when the proposal was still waiting, false when it was withdrawn or expired.</returns>
    public async Task<bool> TakeOutgoingProposalAsync(Guid proposalId)
    {
        await PurgeExpiredAsync();
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.BridgeProposals.Where(x => x.ProposalId == proposalId && !x.Incoming).DeleteAsync() > 0;
    }

    private async Task<(BridgeProposal Proposal, BridgePair Pair)?> FindProposalAsync(ulong guildId, Guid proposalId,
        bool incoming, Guid? pairId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var proposal = await db.BridgeProposals.FirstOrDefaultAsync(x => x.ProposalId == proposalId
                                                                         && x.Incoming == incoming);
        if (proposal is null || pairId is { } expected && expected != proposal.PairId)
            return null;

        var pair = await db.BridgePairs.FirstOrDefaultAsync(x => x.PairId == proposal.PairId
                                                                 && x.GuildId == guildId);
        return pair is null ? null : (proposal, pair);
    }

    private async Task<BridgePairError> AcceptCoreAsync(BridgePair pair, BridgeProposal proposal, ulong userId)
    {
        var failure = BridgePairError.None;
        var reason = "";
        if (host.TextChannels(pair.GuildId).All(x => x.Id != proposal.LocalChannelId))
        {
            failure = BridgePairError.NotFound;
            reason = "channel_missing";
        }
        else if (host.IsBridged(proposal.LocalChannelId))
        {
            failure = BridgePairError.AlreadyBridged;
            reason = "already_bridged";
        }
        else if (!host.CanManageWebhooks(pair.GuildId, proposal.LocalChannelId))
        {
            failure = BridgePairError.MissingPermission;
            reason = "missing_permission";
        }

        ChannelBridge? bridge = null;
        if (failure == BridgePairError.None)
        {
            bridge = await host.CreateBridgeAsync(pair.GuildId, proposal.LocalChannelId, userId, pair.PairId,
                pair.RemoteGuildId, pair.RemoteGuildName, proposal.RemoteChannelId, proposal.RemoteChannelName);
            if (bridge is null)
            {
                failure = BridgePairError.MissingPermission;
                reason = "missing_permission";
            }
        }

        await DeleteProposalAsync(proposal.ProposalId);
        if (bridge is null)
        {
            await DeclineAsync(pair, proposal.ProposalId, reason);
            return failure;
        }

        var channelName = host.TextChannels(pair.GuildId).FirstOrDefault(x => x.Id == proposal.LocalChannelId)?.Name
                          ?? "";
        await host.PublishAsync(new BridgeEnvelope
        {
            Type = BridgeEnvelopeType.LinkAccepted,
            BridgeId = bridge.BridgeId,
            PairId = pair.PairId,
            ProposalId = proposal.ProposalId,
            Link = new BridgeLinkAccepted
            {
                Request = new BridgeLinkCode
                {
                    Platform = Other,
                    GuildId = pair.RemoteGuildId,
                    ChannelId = proposal.RemoteChannelId,
                    GuildName = pair.RemoteGuildName,
                    ChannelName = proposal.RemoteChannelName,
                    CreatedByUserId = proposal.ProposedBy
                },
                GuildId = pair.GuildId,
                ChannelId = proposal.LocalChannelId,
                GuildName = host.GuildName(pair.GuildId),
                ChannelName = channelName,
                AcceptedByUserId = userId
            }
        });
        await host.NoticeLinkedAsync(bridge);
        return BridgePairError.None;
    }

    private Task DeclineAsync(BridgePair pair, Guid proposalId, string reason) =>
        host.PublishAsync(new BridgeEnvelope
        {
            Type = BridgeEnvelopeType.BridgeProposalDeclined,
            PairId = pair.PairId,
            ProposalId = proposalId,
            Reason = reason
        });

    private async Task DeleteProposalAsync(Guid proposalId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.BridgeProposals.Where(x => x.ProposalId == proposalId).DeleteAsync();
    }

    private async Task PurgeExpiredAsync()
    {
        var cutoff = DateTime.UtcNow - BridgeProtocol.ProposalLifetime;
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.BridgeProposals.Where(x => x.DateAdded < cutoff).DeleteAsync();
    }

    #endregion

    #region Inbound

    /// <summary>
    ///     Handles a pair-level envelope from the other platform.
    /// </summary>
    /// <param name="envelope">The envelope, one for which <see cref="BridgeEnvelopeType.IsPairLevel" /> is true.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task HandleAsync(BridgeEnvelope envelope)
    {
        switch (envelope.Type)
        {
            case BridgeEnvelopeType.PairAccepted:
                await HandlePairAcceptedAsync(envelope);
                break;
            case BridgeEnvelopeType.Unpaired when envelope.PairId is { } pairId:
                if (await FindPairAsync(pairId) is { } pair)
                    await RemovePairAsync(pair, envelope.KeepBridges ?? false, true);
                break;
            case BridgeEnvelopeType.BridgeProposed:
                await HandleProposedAsync(envelope);
                break;
            case BridgeEnvelopeType.BridgeProposalDeclined when envelope.ProposalId is { } proposalId:
                await HandleDeclinedAsync(proposalId, envelope.Reason ?? "declined");
                break;
            case BridgeEnvelopeType.BridgeProposalCancelled when envelope.ProposalId is { } cancelled:
                await DeleteProposalAsync(cancelled);
                break;
            case BridgeEnvelopeType.ChannelsUpdated when envelope.PairId is { } updated:
                remoteLists.TryRemove(updated, out _);
                break;
        }
    }

    private async Task HandlePairAcceptedAsync(BridgeEnvelope envelope)
    {
        if (envelope.Pair is not { } accepted || envelope.PairId is not { } pairId
                                              || accepted.Request.Platform != host.Platform)
            return;

        var guildId = accepted.Request.GuildId;
        var pair = new BridgePair
        {
            PairId = pairId,
            GuildId = guildId,
            RemoteGuildId = accepted.GuildId,
            RemoteGuildName = accepted.GuildName,
            CreatedBy = accepted.Request.CreatedByUserId,
            DateAdded = DateTime.UtcNow
        };

        if (!host.HasGuild(guildId) || !await TryInsertPairAsync(pair))
        {
            await host.PublishAsync(new BridgeEnvelope
            {
                Type = BridgeEnvelopeType.Unpaired, PairId = pairId, KeepBridges = false
            });
            return;
        }

        await PublishGuildAsync(guildId, false);
    }

    private async Task HandleProposedAsync(BridgeEnvelope envelope)
    {
        if (envelope.Proposal is not { } info || envelope.PairId is not { } pairId
                                              || envelope.ProposalId is not { } proposalId)
            return;

        if (await FindPairAsync(pairId) is not { } pair)
        {
            await host.PublishAsync(new BridgeEnvelope
            {
                Type = BridgeEnvelopeType.BridgeProposalDeclined,
                PairId = pairId,
                ProposalId = proposalId,
                Reason = "not_paired"
            });
            return;
        }

        var target = info.TargetChannelId;
        var reason = host.TextChannels(pair.GuildId).All(x => x.Id != target)
            ? "channel_missing"
            : !SharedIds(pair).Contains(target)
                ? "channel_not_shared"
                : host.IsBridged(target)
                    ? "already_bridged"
                    : null;
        if (reason is not null)
        {
            await DeclineAsync(pair, proposalId, reason);
            return;
        }

        var proposal = new BridgeProposal
        {
            ProposalId = proposalId,
            PairId = pairId,
            Incoming = true,
            LocalChannelId = target,
            RemoteChannelId = info.ChannelId,
            RemoteChannelName = info.ChannelName,
            ProposedBy = info.ProposedByUserId,
            DateAdded = DateTime.UtcNow
        };

        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            await db.BridgeProposals.Where(x => x.PairId == pairId && x.Incoming && x.LocalChannelId == target
                                                && x.RemoteChannelId == info.ChannelId).DeleteAsync();
            await db.InsertAsync(proposal);
        }

        if (pair.ApprovalMode == 1)
        {
            await AcceptCoreAsync(pair, proposal, info.ProposedByUserId);
            return;
        }

        await host.NoticeAsync(pair.GuildId, target, strings.BridgePairNoticeProposed(pair.GuildId,
            info.GuildName.Length == 0 ? pair.RemoteGuildName : info.GuildName, info.ChannelName,
            ShortId(proposalId)));
    }

    private async Task HandleDeclinedAsync(Guid proposalId, string reason)
    {
        BridgeProposal? proposal;
        BridgePair? pair;
        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            proposal = await db.BridgeProposals.FirstOrDefaultAsync(x => x.ProposalId == proposalId && !x.Incoming);
            if (proposal is null)
                return;

            pair = await db.BridgePairs.FirstOrDefaultAsync(x => x.PairId == proposal.PairId);
            await db.BridgeProposals.Where(x => x.ProposalId == proposalId).DeleteAsync();
        }

        if (pair is null)
            return;

        await host.NoticeAsync(pair.GuildId, proposal.LocalChannelId, strings.BridgePairNoticeDeclined(pair.GuildId,
            proposal.RemoteChannelName, pair.RemoteGuildName, ReasonText(pair.GuildId, reason)));
    }

    #endregion

    #region Commands

    /// <summary>
    ///     Runs a pair command typed in chat.
    /// </summary>
    /// <param name="action">The bridge action: pair, pairs, channels, connect, proposals, accept, decline, share, hide, approval or unpair.</param>
    /// <param name="argument">The text after the action.</param>
    /// <param name="guildId">The community.</param>
    /// <param name="userId">The user running it.</param>
    /// <param name="channelId">The channel it was run in.</param>
    /// <param name="prefix">The command prefix, for the usage hints.</param>
    /// <returns>The answer to show.</returns>
    public async Task<BridgePairReply> RunAsync(string action, string? argument, ulong guildId, ulong userId,
        ulong channelId, string prefix)
    {
        var text = (argument ?? "").Trim();
        return action.ToLowerInvariant() switch
        {
            "pair" => text.Length == 0
                ? await RunCreateCodeAsync(guildId, userId, prefix)
                : await RunAcceptCodeAsync(guildId, userId, text, prefix),
            "pairs" => await RunPairsAsync(guildId, prefix),
            "channels" => await RunChannelsAsync(guildId, text, prefix),
            "connect" => await RunConnectAsync(guildId, userId, text, prefix),
            "proposals" => await RunProposalsAsync(guildId, prefix),
            "accept" => await RunAcceptProposalAsync(guildId, userId, text, prefix),
            "decline" => await RunDeclineAsync(guildId, text, prefix),
            "share" or "hide" => await RunShareAsync(guildId, channelId, text, prefix, action.ToLowerInvariant()),
            "approval" => await RunApprovalAsync(guildId, text, prefix),
            "unpair" => await RunUnpairAsync(guildId, text, prefix),
            _ => new BridgePairReply(false, strings.BridgeUnknownAction(guildId, prefix))
        };
    }

    private async Task<BridgePairReply> RunCreateCodeAsync(ulong guildId, ulong userId, string prefix)
    {
        var (error, code, _) = await CreateCodeAsync(guildId, userId);
        return error == BridgePairError.None
            ? new BridgePairReply(true, strings.BridgePairCodeCreated(guildId, code!, prefix))
            : Failure(guildId, error, prefix);
    }

    private async Task<BridgePairReply> RunAcceptCodeAsync(ulong guildId, ulong userId, string code, string prefix)
    {
        var (error, pair) = await AcceptCodeAsync(guildId, userId, code);
        return error == BridgePairError.None && pair is not null
            ? new BridgePairReply(true, strings.BridgePairCreated(guildId, pair.RemoteGuildName,
                BridgeSettings.PlatformLabel(Other), prefix))
            : Failure(guildId, error, prefix);
    }

    private async Task<BridgePairReply> RunPairsAsync(ulong guildId, string prefix)
    {
        var pairs = await GetPairsAsync(guildId);
        if (pairs.Count == 0)
            return new BridgePairReply(false, strings.BridgePairNone(guildId, prefix));

        var lines = new List<string>();
        for (var i = 0; i < pairs.Count; i++)
        {
            lines.Add(strings.BridgePairListItem(guildId, i + 1, pairs[i].RemoteGuildName,
                BridgeSettings.PlatformLabel(Other), host.BridgesOfPair(pairs[i].PairId).Count,
                ModeText(guildId, pairs[i].ApprovalMode)));
        }

        return new BridgePairReply(true, string.Join('\n', lines), strings.BridgePairListTitle(guildId));
    }

    private async Task<BridgePairReply> RunChannelsAsync(ulong guildId, string text, string prefix)
    {
        var (pair, error) = await PickPairAsync(guildId, text, prefix, "channels");
        if (pair is null)
            return new BridgePairReply(false, error!);

        var channels = await RemoteChannelsAsync(pair);
        if (channels.Count == 0)
            return new BridgePairReply(false, strings.BridgePairChannelsEmpty(guildId, pair.RemoteGuildName));

        var builder = new StringBuilder();
        var shown = 0;
        foreach (var group in channels.GroupBy(x => x.Category))
        {
            var block = new StringBuilder();
            if (group.Key.Length > 0)
                block.Append("**").Append(group.Key).Append("**\n");

            foreach (var channel in group)
                block.Append("#").Append(channel.Name).Append(" (`").Append(channel.Id).Append("`)\n");

            if (builder.Length + block.Length > ListLimit)
                break;

            builder.Append(block).Append('\n');
            shown += group.Count();
        }

        if (shown < channels.Count)
            builder.Append(strings.BridgePairChannelsMore(guildId, channels.Count - shown));

        return new BridgePairReply(true, builder.ToString().TrimEnd(),
            strings.BridgePairChannelsTitle(guildId, pair.RemoteGuildName));
    }

    private async Task<BridgePairReply> RunConnectAsync(ulong guildId, ulong userId, string text, string prefix)
    {
        var (tokens, rest) = Split(text, 2);
        if (tokens.Length < 2)
            return new BridgePairReply(false, strings.BridgePairConnectUsage(guildId, prefix));

        if (ResolveLocal(guildId, tokens[0]) is not { } local)
            return new BridgePairReply(false, strings.BridgePairChannelUnknown(guildId));

        var (pair, error) = await PickPairAsync(guildId, rest, prefix, "connect");
        if (pair is null)
            return new BridgePairReply(false, error!);

        var remote = ResolveRemote(await RemoteChannelsAsync(pair), tokens[1]);
        if (remote is null)
            return Failure(guildId, BridgePairError.ChannelNotShared, prefix);

        var (failure, _, connected) = await ConnectAsync(guildId, pair.PairId, local.Id, remote.Id, userId);
        if (failure != BridgePairError.None)
            return Failure(guildId, failure, prefix);

        return new BridgePairReply(true, connected
            ? strings.BridgePairConnecting(guildId, local.Id, remote.Name, pair.RemoteGuildName)
            : strings.BridgePairProposed(guildId, local.Id, remote.Name, pair.RemoteGuildName));
    }

    private async Task<BridgePairReply> RunProposalsAsync(ulong guildId, string prefix)
    {
        var proposals = await GetProposalsAsync(guildId);
        if (proposals.Count == 0)
            return new BridgePairReply(false, strings.BridgePairProposalsEmpty(guildId));

        var lines = new List<string>();
        for (var i = 0; i < proposals.Count; i++)
        {
            var (proposal, pair) = proposals[i];
            lines.Add(proposal.Incoming
                ? strings.BridgePairProposalIncoming(guildId, i + 1, ShortId(proposal.ProposalId),
                    proposal.RemoteChannelName, pair.RemoteGuildName, proposal.LocalChannelId, prefix)
                : strings.BridgePairProposalOutgoing(guildId, i + 1, ShortId(proposal.ProposalId),
                    proposal.LocalChannelId, proposal.RemoteChannelName, pair.RemoteGuildName));
        }

        return new BridgePairReply(true, string.Join('\n', lines), strings.BridgePairProposalsTitle(guildId));
    }

    private async Task<BridgePairReply> RunAcceptProposalAsync(ulong guildId, ulong userId, string text,
        string prefix)
    {
        var found = await ResolveProposalAsync(guildId, text);
        if (found is not var (proposal, pair))
            return new BridgePairReply(false, strings.BridgePairProposalUnknown(guildId, prefix));

        if (!proposal.Incoming)
            return new BridgePairReply(false, strings.BridgePairProposalNotIncoming(guildId, prefix));

        var error = await AcceptProposalAsync(guildId, proposal.ProposalId, userId, pair.PairId);
        return error == BridgePairError.None
            ? new BridgePairReply(true, strings.BridgePairProposalAccepted(guildId, proposal.LocalChannelId,
                proposal.RemoteChannelName, pair.RemoteGuildName))
            : Failure(guildId, error, prefix);
    }

    private async Task<BridgePairReply> RunDeclineAsync(ulong guildId, string text, string prefix)
    {
        var found = await ResolveProposalAsync(guildId, text);
        if (found is not var (proposal, pair))
            return new BridgePairReply(false, strings.BridgePairProposalUnknown(guildId, prefix));

        var error = proposal.Incoming
            ? await DeclineProposalAsync(guildId, proposal.ProposalId, pair.PairId)
            : await CancelProposalAsync(guildId, proposal.ProposalId, pair.PairId);
        return error == BridgePairError.None
            ? new BridgePairReply(true, proposal.Incoming
                ? strings.BridgePairProposalDeclined(guildId)
                : strings.BridgePairProposalCancelled(guildId))
            : Failure(guildId, error, prefix);
    }

    private async Task<BridgePairReply> RunShareAsync(ulong guildId, ulong channelId, string text, string prefix,
        string action)
    {
        var (tokens, rest) = Split(text, 1);
        ulong? local = tokens.Length == 0 ? channelId : ResolveLocal(guildId, tokens[0])?.Id;
        if (local is not { } id)
            return new BridgePairReply(false, strings.BridgePairChannelUnknown(guildId));

        var (pair, error) = await PickPairAsync(guildId, rest, prefix, action);
        if (pair is null)
            return new BridgePairReply(false, error!);

        var share = action == "share";
        var failure = await SetSharedAsync(guildId, pair.PairId, id, share);
        if (failure != BridgePairError.None)
            return Failure(guildId, failure, prefix);

        if (!share)
            return new BridgePairReply(true, strings.BridgePairHidden(guildId, id, pair.RemoteGuildName));

        var visible = host.TextChannels(guildId).FirstOrDefault(x => x.Id == id)?.VisibleToEveryone ?? false;
        return new BridgePairReply(true, visible
            ? strings.BridgePairShared(guildId, id, pair.RemoteGuildName)
            : strings.BridgePairNotPublic(guildId, id, pair.RemoteGuildName));
    }

    private async Task<BridgePairReply> RunApprovalAsync(ulong guildId, string text, string prefix)
    {
        var (tokens, rest) = Split(text, 1);
        if (tokens.Length == 0 || !TryParseMode(tokens[0], out var mode))
            return new BridgePairReply(false, strings.BridgePairApprovalUsage(guildId, prefix));

        var (pair, error) = await PickPairAsync(guildId, rest, prefix, "approval");
        if (pair is null)
            return new BridgePairReply(false, error!);

        var (failure, _) = await SetSettingsAsync(guildId, pair.PairId, ModeName(mode),
            BridgeSettings.ParseIds(pair.HiddenChannelIds));
        if (failure != BridgePairError.None)
            return Failure(guildId, failure, prefix);

        return new BridgePairReply(true, mode == 1
            ? strings.BridgePairApprovalAuto(guildId, pair.RemoteGuildName)
            : strings.BridgePairApprovalApprove(guildId, pair.RemoteGuildName));
    }

    private async Task<BridgePairReply> RunUnpairAsync(ulong guildId, string text, string prefix)
    {
        var keep = false;
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0 && tokens[^1].Equals("keep", StringComparison.OrdinalIgnoreCase))
        {
            keep = true;
            text = string.Join(' ', tokens[..^1]);
        }

        var (pair, error) = await PickPairAsync(guildId, text, prefix, "unpair");
        if (pair is null)
            return new BridgePairReply(false, error!);

        var failure = await UnpairAsync(guildId, pair.PairId, keep);
        if (failure != BridgePairError.None)
            return Failure(guildId, failure, prefix);

        return new BridgePairReply(true, keep
            ? strings.BridgePairUnpairedKept(guildId, pair.RemoteGuildName)
            : strings.BridgePairUnpaired(guildId, pair.RemoteGuildName));
    }

    private BridgePairReply Failure(ulong guildId, BridgePairError error, string prefix) => new(false, error switch
    {
        BridgePairError.NoPermission => strings.BridgePairNoPermission(guildId),
        BridgePairError.NotFound => strings.BridgePairNotFound(guildId, prefix),
        BridgePairError.CodeInvalid => strings.BridgePairCodeInvalid(guildId),
        BridgePairError.CodeWrongPlatform => strings.BridgePairCodeWrongPlatform(guildId),
        BridgePairError.AlreadyPaired => strings.BridgePairAlready(guildId),
        BridgePairError.ChannelNotShared => strings.BridgePairChannelNotShared(guildId),
        BridgePairError.AlreadyBridged => strings.BridgePairAlreadyBridged(guildId),
        BridgePairError.MissingPermission => strings.BridgePairMissingPermission(guildId),
        _ => strings.BridgePairInvalid(guildId)
    });

    private async Task<(BridgePair? Pair, string? Error)> PickPairAsync(ulong guildId, string? text, string prefix,
        string action)
    {
        var pairs = await GetPairsAsync(guildId);
        if (pairs.Count == 0)
            return (null, strings.BridgePairNone(guildId, prefix));

        var value = (text ?? "").Trim();
        if (value.Length == 0)
            return pairs.Count == 1 ? (pairs[0], null) : (null, strings.BridgePairPick(guildId, prefix, action));

        if (int.TryParse(value, out var index))
        {
            return index >= 1 && index <= pairs.Count
                ? (pairs[index - 1], null)
                : (null, strings.BridgePairNotFound(guildId, prefix));
        }

        var found = value.Length >= 6 && value.All(Uri.IsHexDigit)
            ? pairs.FirstOrDefault(x => x.PairId.ToString("N").StartsWith(value, StringComparison.OrdinalIgnoreCase))
            : null;
        found ??= pairs.FirstOrDefault(x => x.RemoteGuildName.Equals(value, StringComparison.OrdinalIgnoreCase))
                  ?? pairs.FirstOrDefault(x => x.RemoteGuildName.Contains(value, StringComparison.OrdinalIgnoreCase));
        return found is null ? (null, strings.BridgePairNotFound(guildId, prefix)) : (found, null);
    }

    private async Task<(BridgeProposal Proposal, BridgePair Pair)?> ResolveProposalAsync(ulong guildId, string text)
    {
        var proposals = await GetProposalsAsync(guildId);
        var value = text.Trim();
        if (value.Length == 0)
            return null;

        if (int.TryParse(value, out var index))
            return index >= 1 && index <= proposals.Count ? proposals[index - 1] : null;

        if (value.Length < 4 || !value.All(Uri.IsHexDigit))
            return null;

        var matches = proposals.Where(x => x.Proposal.ProposalId.ToString("N")
            .StartsWith(value, StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private BridgeChannelInfo? ResolveLocal(ulong guildId, string token)
    {
        var channels = host.TextChannels(guildId);
        if (BridgeSettings.ParseUserId(token) is { } id && channels.FirstOrDefault(x => x.Id == id) is { } byId)
            return byId;

        var name = token.TrimStart('#');
        return channels.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static BridgeSharedChannel? ResolveRemote(List<BridgeSharedChannel> channels, string token)
    {
        if (BridgeSettings.ParseUserId(token) is { } id && channels.FirstOrDefault(x => x.Id == id) is { } byId)
            return byId;

        var name = token.TrimStart('#');
        return channels.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static (string[] Tokens, string Remainder) Split(string text, int count)
    {
        var tokens = new List<string>();
        var rest = text.Trim();
        while (tokens.Count < count && rest.Length > 0)
        {
            var space = rest.IndexOf(' ');
            if (space < 0)
            {
                tokens.Add(rest);
                rest = "";
            }
            else
            {
                tokens.Add(rest[..space]);
                rest = rest[(space + 1)..].Trim();
            }
        }

        return (tokens.ToArray(), rest);
    }

    private static string ShortId(Guid id) => id.ToString("N")[..8];

    private string ModeText(ulong guildId, int mode) =>
        mode == 1 ? strings.BridgePairModeAuto(guildId) : strings.BridgePairModeApprove(guildId);

    private string ReasonText(ulong guildId, string reason) => reason switch
    {
        "channel_not_shared" => strings.BridgePairReasonChannelNotShared(guildId),
        "already_bridged" => strings.BridgePairReasonAlreadyBridged(guildId),
        "missing_permission" => strings.BridgePairReasonMissingPermission(guildId),
        "not_paired" => strings.BridgePairReasonNotPaired(guildId),
        "channel_missing" => strings.BridgePairReasonChannelMissing(guildId),
        _ => strings.BridgePairReasonDeclined(guildId)
    };

    private static string ModeName(int mode) => mode == 1 ? AutoName : ApproveName;

    private static bool TryParseMode(string? text, out int mode)
    {
        mode = 0;
        switch ((text ?? "").Trim().ToLowerInvariant())
        {
            case "approve" or "manual":
                return true;
            case "auto" or "automatic":
                mode = 1;
                return true;
            default:
                return false;
        }
    }

    #endregion

    #region Storage

    private async Task<BridgePair?> FindPairAsync(Guid pairId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.BridgePairs.FirstOrDefaultAsync(x => x.PairId == pairId);
    }

    private async Task<bool> TryInsertPairAsync(BridgePair pair)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            if (await db.BridgePairs.AnyAsync(x => x.GuildId == pair.GuildId && x.RemoteGuildId == pair.RemoteGuildId))
                return false;

            await db.InsertAsync(pair);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not store pair {PairId}", pair.PairId);
            return false;
        }

        pairedGuilds[pair.GuildId] = 0;
        return true;
    }

    private async Task SavePairAsync(BridgePair pair)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.BridgePairs.Where(x => x.PairId == pair.PairId).Set(x => x.ApprovalMode, pair.ApprovalMode)
            .Set(x => x.HiddenChannelIds, pair.HiddenChannelIds).UpdateAsync();
    }

    private async Task RemovePairAsync(BridgePair pair, bool keepBridges, bool remote)
    {
        bool anyLeft;
        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            await db.BridgeProposals.Where(x => x.PairId == pair.PairId).DeleteAsync();
            await db.BridgePairs.Where(x => x.PairId == pair.PairId).DeleteAsync();
            anyLeft = await db.BridgePairs.AnyAsync(x => x.GuildId == pair.GuildId);
        }

        if (!anyLeft)
            pairedGuilds.TryRemove(pair.GuildId, out _);

        published.TryRemove(pair.PairId, out _);
        remoteLists.TryRemove(pair.PairId, out _);
        try
        {
            await Redis.KeyDeleteAsync(
            [
                BridgeProtocol.ChannelsKey(host.Platform, pair.GuildId, pair.PairId),
                BridgeProtocol.PairModeKey(host.Platform, pair.GuildId, pair.PairId)
            ]);
        }
        catch (RedisException ex)
        {
            logger.LogDebug(ex, "Could not delete the shared channels of pair {PairId}", pair.PairId);
        }

        var bridges = host.BridgesOfPair(pair.PairId).ToList();
        if (keepBridges)
        {
            await host.ClearPairAsync(pair.PairId);
            return;
        }

        foreach (var bridge in bridges)
        {
            if (remote)
                await host.UnlinkLocalAsync(bridge);
            else
                await host.RemoveBridgeAsync(bridge);
        }
    }

    private async Task RestoreCodeAsync(string key, BridgePairCode request, RedisValue raw)
    {
        var remaining = request.ExpiresAt - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero)
            await Redis.StringSetAsync(key, raw, remaining);
    }

    private sealed record RemoteList(DateTime At, List<BridgeSharedChannel> Channels);

    #endregion
}
