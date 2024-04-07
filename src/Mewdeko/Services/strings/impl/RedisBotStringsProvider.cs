using System.Web;
using Mewdeko.Services.strings;
using Mewdeko.Services.strings.impl;
using StackExchange.Redis;

namespace Mewdeko.Services.Strings.Impl
{
    /// <summary>
    /// Uses <see cref="IStringsSource"/> to load strings into Redis hash (only on Shard 0)
    /// and retrieves them from Redis via <see cref="GetText"/>.
    /// </summary>
    public class RedisBotStringsProvider : IBotStringsProvider
    {
        private readonly IBotCredentials creds;
        private readonly ConnectionMultiplexer redis;
        private readonly IStringsSource source;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisBotStringsProvider"/> class.
        /// </summary>
        /// <param name="redis">The Redis connection multiplexer.</param>
        /// <param name="discordClient">The Discord socket client.</param>
        /// <param name="source">The strings source.</param>
        /// <param name="creds">The bot credentials.</param>
        public RedisBotStringsProvider(ConnectionMultiplexer redis, DiscordSocketClient discordClient,
            IStringsSource source, IBotCredentials creds)
        {
            this.redis = redis;
            this.source = source;
            this.creds = creds;

            if (discordClient.ShardId == 0)
                Reload();
        }

        /// <summary>
        /// Retrieves the text associated with the specified key for the given locale.
        /// </summary>
        /// <param name="localeName">The name of the locale.</param>
        /// <param name="key">The key of the text to retrieve.</param>
        /// <returns>The text associated with the specified key for the given locale.</returns>
        public string GetText(string localeName, string? key) =>
            redis.GetDatabase().HashGet($"{creds.RedisKey()}:responses:{localeName}", key);

        /// <summary>
        /// Retrieves the command strings for the specified command and locale.
        /// </summary>
        /// <param name="localeName">The name of the locale.</param>
        /// <param name="commandName">The name of the command.</param>
        /// <returns>The command strings for the specified command and locale.</returns>
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

        /// <summary>
        /// Reloads the strings in the Redis cache.
        /// </summary>
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
}