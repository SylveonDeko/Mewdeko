using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Impl;
using StackExchange.Redis;

namespace Mewdeko.Services;

/// <summary>
///     Holds which bot instance handles which guild, and answers the ownership rules from memory.
/// </summary>
public sealed class GuildOwnershipMap
{
    private readonly ConcurrentDictionary<ulong, int> owners = new();
    private volatile int instanceId;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GuildOwnershipMap" /> class.
    /// </summary>
    /// <param name="isMaster">Whether this instance is the master instance.</param>
    /// <param name="instanceId">This instance's BotInstance id, or 0 when it is not known yet.</param>
    public GuildOwnershipMap(bool isMaster, int instanceId = 0)
    {
        IsMaster = isMaster;
        this.instanceId = instanceId;
    }

    /// <summary>
    ///     Gets whether this instance is the master instance.
    /// </summary>
    public bool IsMaster { get; }

    /// <summary>
    ///     Gets or sets this instance's BotInstance id, or 0 when it is not known yet.
    /// </summary>
    public int InstanceId
    {
        get => instanceId;
        set => instanceId = value;
    }

    /// <summary>
    ///     Gets the number of guilds that have an assignment.
    /// </summary>
    public int Count => owners.Count;

    /// <summary>
    ///     Checks whether this instance handles a guild: the guild is assigned to it, or the guild is unassigned
    ///     and this is the master instance.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <returns>True if this instance handles the guild.</returns>
    public bool IsMine(ulong guildId)
    {
        if (owners.IsEmpty)
            return IsMaster;

        return owners.TryGetValue(guildId, out var owner) ? owner == instanceId && instanceId != 0 : IsMaster;
    }

    /// <summary>
    ///     Assigns a guild to an instance.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="owner">The BotInstance id that handles the guild.</param>
    public void Set(ulong guildId, int owner)
    {
        owners[guildId] = owner;
    }

    /// <summary>
    ///     Removes a guild's assignment, returning it to the master instance.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    public void Remove(ulong guildId)
    {
        owners.TryRemove(guildId, out _);
    }

    /// <summary>
    ///     Replaces every assignment with the given set.
    /// </summary>
    /// <param name="assignments">The guild id and owning BotInstance id pairs.</param>
    public void Replace(IEnumerable<KeyValuePair<ulong, int>> assignments)
    {
        var fresh = assignments.ToDictionary(x => x.Key, x => x.Value);
        foreach (var stale in owners.Keys.Where(x => !fresh.ContainsKey(x)))
            owners.TryRemove(stale, out _);

        foreach (var (guildId, owner) in fresh)
            owners[guildId] = owner;
    }
}

/// <summary>
///     Decides which bot instance handles each guild.
/// </summary>
/// <remarks>
///     Assignments live in the GuildOwnership table and are cached in memory.
/// </remarks>
public class GuildOwnershipService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     The Redis channel ownership changes are published on.
    /// </summary>
    public const string Channel = "mewdeko:guild-ownership";

    private const string ReloadMessage = "reload";
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromMinutes(5);

    private readonly DiscordShardedClient client;
    private readonly BotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<GuildOwnershipService> logger;
    private readonly GuildOwnershipMap map;
    private readonly ConnectionMultiplexer redis;
    private Timer? reloadTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GuildOwnershipService" /> class and loads the assignments.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="redis">The shared Redis connection.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="logger">The logger.</param>
    public GuildOwnershipService(IDataConnectionFactory dbFactory, ConnectionMultiplexer redis, BotCredentials creds,
        DiscordShardedClient client, ILogger<GuildOwnershipService> logger)
    {
        this.dbFactory = dbFactory;
        this.redis = redis;
        this.creds = creds;
        this.client = client;
        this.logger = logger;
        map = new GuildOwnershipMap(creds.IsMasterInstance);

        try
        {
            using var db = dbFactory.CreateConnection();
            map.InstanceId = db.BotInstances
                .Where(x => x.Port == creds.ApiPort && x.Host == creds.InstanceApiHost)
                .Select(x => x.Id)
                .FirstOrDefault();
            map.Replace(db.GuildOwnerships.Select(x => new KeyValuePair<ulong, int>(x.GuildId, x.InstanceId))
                .ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load guild ownership at startup");
        }

        try
        {
            var subscriber = redis.GetSubscriber();
            subscriber.Subscribe(RedisChannel.Literal(Channel), OnMessage);
            redis.ConnectionRestored += OnConnectionRestored;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to subscribe to guild ownership changes");
        }

        reloadTimer = new Timer(_ => _ = ReloadAsync(), null, ReloadInterval, ReloadInterval);
    }

    /// <summary>
    ///     Gets whether this instance is the master instance.
    /// </summary>
    public bool HandlesGlobalWork => map.IsMaster;

    /// <summary>
    ///     Gets the BotInstance id of this instance, or 0 if it has not been resolved yet.
    /// </summary>
    public int InstanceId => map.InstanceId;

    /// <summary>
    ///     Gets whether this instance runs for a customer.
    /// </summary>
    public bool IsCustomInstance => !map.IsMaster;

    /// <inheritdoc />
    public void Dispose()
    {
        reloadTimer?.Dispose();
        reloadTimer = null;
        redis.ConnectionRestored -= OnConnectionRestored;
        try
        {
            redis.GetSubscriber().Unsubscribe(RedisChannel.Literal(Channel), OnMessage);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to unsubscribe from guild ownership changes");
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Resolves this instance's BotInstance row, registering one for custom instances that have none.
    /// </summary>
    public async Task OnReadyAsync()
    {
        try
        {
            await ResolveInstanceAsync().ConfigureAwait(false);
            await ReloadAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve this instance's id");
        }
    }

    /// <summary>
    ///     Checks whether this instance should handle a guild. Use this before doing any guild scoped work.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <returns>
    ///     True if the guild is assigned to this instance, or it is unassigned and this is the master instance.
    ///     Always true on a master instance when no guild has an assignment.
    /// </returns>
    public bool IsMine(ulong guildId) => map.IsMine(guildId);

    /// <summary>
    ///     Assigns a guild to an instance, saves it and tells every instance. Replaces any earlier assignment.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="instanceId">The BotInstance id that will handle the guild.</param>
    public async Task AssignAsync(ulong guildId, int instanceId)
    {
        await using (var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false))
        {
            var updated = await db.GuildOwnerships.Where(x => x.GuildId == guildId)
                .Set(x => x.InstanceId, instanceId)
                .UpdateAsync().ConfigureAwait(false);

            if (updated == 0)
            {
                await db.InsertAsync(new GuildOwnership
                {
                    GuildId = guildId, InstanceId = instanceId
                }).ConfigureAwait(false);
            }
        }

        map.Set(guildId, instanceId);
        await PublishAsync($"{guildId}:{instanceId}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a guild's assignment, saves it and tells every instance.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    public async Task ReleaseAsync(ulong guildId)
    {
        await using (var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false))
        {
            await db.GuildOwnerships.Where(x => x.GuildId == guildId).DeleteAsync().ConfigureAwait(false);
        }

        map.Remove(guildId);
        await PublishAsync($"{guildId}:0").ConfigureAwait(false);
    }

    private async Task ResolveInstanceAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var botId = client.CurrentUser.Id;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var existing = await FindInstanceAsync(db, botId).ConfigureAwait(false);
            if (existing is not null)
            {
                map.InstanceId = existing.Id;
                return;
            }

            if (!creds.IsMasterInstance)
                break;

            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }

        logger.LogInformation("Registering self as instance on {Host}:{Port}", creds.InstanceApiHost, creds.ApiPort);
        map.InstanceId = await db.InsertWithInt32IdentityAsync(new BotInstance
        {
            Port = creds.ApiPort,
            Host = creds.InstanceApiHost,
            BotId = botId,
            BotName = client.CurrentUser.Username,
            BotAvatar = client.CurrentUser.GetAvatarUrl() ?? "",
            IsActive = true,
            IsCustom = !creds.IsMasterInstance,
            LastStatusUpdate = DateTime.UtcNow
        }).ConfigureAwait(false);
    }

    private async Task<BotInstance?> FindInstanceAsync(MewdekoDb db, ulong botId)
    {
        var byBot = await db.BotInstances.FirstOrDefaultAsync(x => x.BotId == botId).ConfigureAwait(false);
        if (byBot is not null)
            return byBot;

        return await db.BotInstances
            .FirstOrDefaultAsync(x => x.Port == creds.ApiPort && x.Host == creds.InstanceApiHost)
            .ConfigureAwait(false);
    }

    private async Task ReloadAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
            var rows = await db.GuildOwnerships.ToListAsync().ConfigureAwait(false);
            map.Replace(rows.Select(x => new KeyValuePair<ulong, int>(x.GuildId, x.InstanceId)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reload guild ownership");
        }
    }

    private async Task PublishAsync(string message)
    {
        try
        {
            await redis.GetSubscriber().PublishAsync(RedisChannel.Literal(Channel), message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish guild ownership change");
        }
    }

    private void OnMessage(RedisChannel channel, RedisValue value)
    {
        var text = value.ToString();
        if (text == ReloadMessage)
        {
            _ = ReloadAsync();
            return;
        }

        var parts = text.Split(':');
        if (parts.Length != 2 || !ulong.TryParse(parts[0], out var guildId) || !int.TryParse(parts[1], out var owner))
            return;

        if (owner == 0)
            map.Remove(guildId);
        else
            map.Set(guildId, owner);
    }

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs args)
    {
        _ = ReloadAsync();
    }
}
