using StackExchange.Redis;
using System.Web;

namespace Mewdeko.Services.strings.impl;

/// <summary>
///     Uses <see cref="IStringsSource" /> to load strings into redis hash (only on Shard 0)
///     and retrieves them from redis via <see cref="GetText" />
/// </summary>
public class RedisBotStringsProvider : IBotStringsProvider
{
    private readonly IBotCredentials _creds;
    private readonly ConnectionMultiplexer _redis;
    private readonly IStringsSource _source;

    public RedisBotStringsProvider(ConnectionMultiplexer redis, DiscordSocketClient discordClient,
        IStringsSource source, IBotCredentials creds)
    {
        _redis = redis;
        _source = source;
        _creds = creds;

        if (discordClient.ShardId == 0)
            Reload();
    }

    public string GetText(string localeName, string? key) => _redis.GetDatabase().HashGet($"{_creds.RedisKey()}:responses:{localeName}", key);

    public CommandStrings GetCommandStrings(string localeName, string commandName)
    {
        string argsStr = _redis.GetDatabase()
            .HashGet($"{_creds.RedisKey()}:commands:{localeName}", $"{commandName}::args");
        if (argsStr == default)
            return null;

        var descStr = _redis.GetDatabase()
            .HashGet($"{_creds.RedisKey()}:commands:{localeName}", $"{commandName}::desc");
        if (descStr == default)
            return null;

        var args = Array.ConvertAll(argsStr.Split('&'), HttpUtility.UrlDecode);
        return new CommandStrings
        {
            Args = args,
            Desc = descStr
        };
    }

    public void Reload()
    {
        var redisDb = _redis.GetDatabase();
        foreach (var (localeName, localeStrings) in _source.GetResponseStrings())
        {
            var hashFields = localeStrings
                .Select(x => new HashEntry(x.Key, x.Value))
                .ToArray();

            redisDb.HashSet($"{_creds.RedisKey()}:responses:{localeName}", hashFields);
        }

        foreach (var (localeName, localeStrings) in _source.GetCommandStrings())
        {
            var hashFields = localeStrings
                .Select(x => new HashEntry($"{x.Key}::args",
                    string.Join('&', Array.ConvertAll(x.Value.Args, HttpUtility.UrlEncode))))
                .Concat(localeStrings
                    .Select(x => new HashEntry($"{x.Key}::desc", x.Value.Desc)))
                .ToArray();

            redisDb.HashSet($"{_creds.RedisKey()}:commands:{localeName}", hashFields);
        }
    }
}