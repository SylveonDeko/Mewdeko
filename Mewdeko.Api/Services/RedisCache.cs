using Mewdeko.Database.Models;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;

#pragma warning disable CS8604 // Possible null reference argument.

namespace Mewdeko.Api.Services;

/// <summary>
///     Service for caching data in Redis.
/// </summary>
public class RedisCache
{
    private readonly string redisKey;

    private readonly JsonSerializerSettings settings = new()
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    /// <summary>
    ///     Initializes a new instance of the <see cref="RedisCache" /> class.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public RedisCache(string redisUrl, string redisKey)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        var conf = ConfigurationOptions.Parse(redisUrl);
        conf.SocketManager = new SocketManager("Main", true);
        LoadRedis(conf).ConfigureAwait(false);
        this.redisKey = redisKey;
    }

    /// <summary>
    ///     The Redis connection multiplexer.
    /// </summary>
    public ConnectionMultiplexer Redis { get; set; }

    /// <summary>
    ///     Caaches a users afk status.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="afk">The afk status.</param>
    public async Task CacheAfk(ulong guildId, ulong userId, Afk afk)
    {
        try
        {
            var db = Redis.GetDatabase();
            await db.StringSetAsync($"{redisKey}_{guildId}_{userId}_afk", JsonConvert.SerializeObject(afk),
                flags: CommandFlags.FireAndForget);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occured while setting afk");
        }
    }

    /// <summary>
    ///     Caches config for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <param name="config">The config to cache.</param>
    public async Task SetGuildConfigCache(ulong id, GuildConfig config)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"{redisKey}_guildconfig_{id}", JsonConvert.SerializeObject(config, settings));
    }

    /// <summary>
    ///     Retrieves config for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <returns>If successfull, the guild config, if not, null.</returns>
    public async Task<GuildConfig?> GetGuildConfigCache(ulong id)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_guildconfig_{id}");
        return result.HasValue ? JsonConvert.DeserializeObject<GuildConfig>(result!, settings) : null;
    }

    /// <summary>
    ///     Retrieves a users afk status.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The afk status.</returns>
    public async Task<Afk?> RetrieveAfk(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        var afkJson = await db.StringGetAsync($"{redisKey}_{guildId}_{userId}_afk");
        return afkJson.HasValue ? JsonConvert.DeserializeObject<Afk>(afkJson) : null;
    }

    /// <summary>
    ///     Clears a users afk status.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    public async Task ClearAfk(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        await db.KeyDeleteAsync($"{redisKey}_{guildId}_{userId}_afk",
            CommandFlags.FireAndForget);
    }

    private async Task LoadRedis(ConfigurationOptions options)
    {
        options.AsyncTimeout = 20000;
        options.SyncTimeout = 20000;
        Redis = await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);
    }
}