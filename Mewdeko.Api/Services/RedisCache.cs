using Mewdeko.Database.Models;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Api.Services;

/// <summary>
///     Service for caching data in Redis.
/// </summary>
public class RedisCache
{
    private readonly string redisKey;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RedisCache" /> class.
    /// </summary>
    public RedisCache(string redisUrl, string redisKey)
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