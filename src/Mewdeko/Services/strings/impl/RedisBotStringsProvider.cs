using System.Web;
using StackExchange.Redis;

namespace Mewdeko.Services.strings.impl;

/// <summary>
///     Uses <see cref="IStringsSource" /> to load strings into redis hash (only on Shard 0)
///     and retrieves them from redis via <see cref="GetText" />
/// </summary>
public class RedisBotStringsProvider : IBotStringsProvider
{
    private readonly IBotCredentials creds;
    private readonly ConnectionMultiplexer redis;
    private readonly IStringsSource source;

    public RedisBotStringsProvider(ConnectionMultiplexer redis, DiscordSocketClient discordClient,
        IStringsSource source, IBotCredentials creds)
    {
        this.redis = redis;
        this.source = source;
        this.creds = creds;

        if (discordClient.ShardId == 0)
            Reload();
    }

    public string GetText(string localeName, string? key) => redis.GetDatabase().HashGet($"{creds.RedisKey()}:responses:{localeName}", key);

    public CommandStrings? GetCommandStrings(string localeName, string commandName)
    {
        string argsStr = redis.GetDatabase()
            .HashGet($"{creds.RedisKey()}:commands:{localeName}", $"{commandName}::args");
        if (argsStr == default)
            return null;

        var descStr = redis.GetDatabase()
            .HashGet($"{creds.RedisKey()}:commands:{localeName}", $"{commandName}::desc");
        if (descStr == default)
            return null;

        var args = Array.ConvertAll(argsStr.Split('&'), HttpUtility.UrlDecode);
        return new CommandStrings
        {
            Args = args, Desc = descStr
        };
    }

    public void Reload()
    {
        var redisDb = redis.GetDatabase();
        foreach (var (localeName, localeStrings) in source.GetResponseStrings())
        {
            var hashFields = localeStrings
                .Select(x => new HashEntry(x.Key, x.Value))
                .ToArray();

            redisDb.HashSet($"{creds.RedisKey()}:responses:{localeName}", hashFields);
        }

        foreach (var (localeName, localeStrings) in source.GetCommandStrings())
        {
            var hashFields = localeStrings
                .Select(x => new HashEntry($"{x.Key}::args",
                    string.Join('&', Array.ConvertAll(x.Value.Args, HttpUtility.UrlEncode))))
                .Concat(localeStrings
                    .Select(x => new HashEntry($"{x.Key}::desc", x.Value.Desc)))
                .ToArray();

            redisDb.HashSet($"{creds.RedisKey()}:commands:{localeName}", hashFields);
        }
    }
}