using System.Text.Json;
using System.Web;
using StackExchange.Redis;

namespace Mewdeko.Services.strings.impl;

/// <summary>
///     Uses <see cref="IStringsSource" /> to load strings into Redis hash (only on Shard 0)
///     and retrieves them from Redis via <see cref="GetText" />.
/// </summary>
public class RedisBotStringsProvider : IBotStringsProvider
{
    private readonly IBotCredentials creds;
    private readonly ConnectionMultiplexer redis;
    private readonly IStringsSource source;
    private IReadOnlyCollection<string> availableLocales = Array.Empty<string>();

    /// <summary>
    ///     Initializes a new instance of the <see cref="RedisBotStringsProvider" /> class.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="discordClient">The Discord socket client.</param>
    /// <param name="source">The strings source.</param>
    /// <param name="creds">The bot credentials.</param>
    public RedisBotStringsProvider(ConnectionMultiplexer redis, DiscordShardedClient discordClient,
        IStringsSource source, IBotCredentials creds)
    {
        this.redis = redis;
        this.source = source;
        this.creds = creds;
        Reload();
    }

    /// <summary>
    ///     Retrieves the text associated with the specified key for the given locale.
    /// </summary>
    /// <param name="localeName">The name of the locale.</param>
    /// <param name="key">The key of the text to retrieve.</param>
    /// <returns>The text associated with the specified key for the given locale.</returns>
    public string GetText(string localeName, string? key)
    {
        return redis.GetDatabase().HashGet($"{creds.RedisKey()}:responses:{localeName}", key);
    }

    /// <summary>
    ///     Retrieves the command strings for the specified command and locale.
    /// </summary>
    /// <param name="localeName">The name of the locale.</param>
    /// <param name="commandName">The name of the command.</param>
    /// <returns>The command strings for the specified command and locale.</returns>
    public CommandStrings? GetCommandStrings(string localeName, string commandName)
    {
        var redisDb = redis.GetDatabase();
        string argsStr = redisDb.HashGet($"{creds.RedisKey()}:commands:{localeName}", $"{commandName}::args");
        if (argsStr == null)
            return null;

        var descStr = redisDb.HashGet($"{creds.RedisKey()}:commands:{localeName}", $"{commandName}::desc");
        if (descStr == default)
            return null;

        var signatureStr = redisDb.HashGet($"{creds.RedisKey()}:commands:{localeName}", $"{commandName}::signature");
        var paramsStr = redisDb.HashGet($"{creds.RedisKey()}:commands:{localeName}", $"{commandName}::params");

        var args = Array.ConvertAll(argsStr.Split('&'), HttpUtility.UrlDecode);

        var cmdStrings = new CommandStrings
        {
            Args = args, Desc = descStr, Signature = signatureStr
        };

        // Parse parameters if they exist
        if (string.IsNullOrEmpty(paramsStr)) return cmdStrings;
        try
        {
            var parameters = JsonSerializer.Deserialize<List<ParameterString>>(
                HttpUtility.UrlDecode(paramsStr));

            if (parameters != null)
                cmdStrings.Parameters = parameters;
        }
        catch (Exception)
        {
            // If there's an error deserializing, just continue with empty parameters
        }

        return cmdStrings;
    }

    /// <summary>
    ///     Reloads the strings in the Redis cache.
    /// </summary>
    public void Reload()
    {
        var redisDb = redis.GetDatabase();
        var responseStrings = source.GetResponseStrings();
        availableLocales = responseStrings.Keys.ToArray();

        foreach (var (localeName, localeStrings) in responseStrings)
        {
            var hashFields = localeStrings
                .Select(x => new HashEntry(x.Key, x.Value))
                .ToArray();

            redisDb.HashSet($"{creds.RedisKey()}:responses:{localeName}", hashFields);
        }

        foreach (var (localeName, localeStrings) in source.GetCommandStrings())
        {
            List<HashEntry> hashFields = new();

            foreach (var commandEntry in localeStrings)
            {
                // Add basic command information
                hashFields.Add(new HashEntry(
                    $"{commandEntry.Key}::args",
                    string.Join('&', Array.ConvertAll(commandEntry.Value.Args, HttpUtility.UrlEncode))
                ));

                hashFields.Add(new HashEntry(
                    $"{commandEntry.Key}::desc",
                    commandEntry.Value.Desc
                ));

                // Add signature if available
                if (!string.IsNullOrEmpty(commandEntry.Value.Signature))
                {
                    hashFields.Add(new HashEntry(
                        $"{commandEntry.Key}::signature",
                        commandEntry.Value.Signature
                    ));
                }

                // Add parameters if available
                if (commandEntry.Value.Parameters?.Count > 0)
                {
                    var paramsJson = JsonSerializer.Serialize(commandEntry.Value.Parameters);
                    hashFields.Add(new HashEntry(
                        $"{commandEntry.Key}::params",
                        HttpUtility.UrlEncode(paramsJson)
                    ));
                }

                // Add overloads if available
                if (commandEntry.Value.Overloads?.Count > 0)
                {
                    for (var i = 0; i < commandEntry.Value.Overloads.Count; i++)
                    {
                        var overload = commandEntry.Value.Overloads[i];
                        var overloadKey = $"{commandEntry.Key}_overload_{i}";

                        hashFields.Add(new HashEntry(
                            $"{overloadKey}::args",
                            string.Join('&', Array.ConvertAll(overload.Args, HttpUtility.UrlEncode))
                        ));

                        hashFields.Add(new HashEntry(
                            $"{overloadKey}::desc",
                            overload.Desc
                        ));

                        if (!string.IsNullOrEmpty(overload.Signature))
                        {
                            hashFields.Add(new HashEntry(
                                $"{overloadKey}::signature",
                                overload.Signature
                            ));
                        }

                        if (overload.Parameters?.Count > 0)
                        {
                            var paramsJson = JsonSerializer.Serialize(overload.Parameters);
                            hashFields.Add(new HashEntry(
                                $"{overloadKey}::params",
                                HttpUtility.UrlEncode(paramsJson)
                            ));
                        }
                    }
                }
            }

            redisDb.HashSet($"{creds.RedisKey()}:commands:{localeName}", hashFields.ToArray());
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetAvailableLocales()
    {
        return availableLocales;
    }

    /// <summary>
    ///     Retrieves overloaded versions of a command for the specified locale and command name.
    /// </summary>
    /// <param name="localeName">The name of the locale.</param>
    /// <param name="commandName">The base name of the command.</param>
    /// <returns>A list of overloaded versions of the command.</returns>
    public List<CommandOverload> GetCommandOverloads(string localeName, string commandName)
    {
        var overloads = new List<CommandOverload>();
        var redisDb = redis.GetDatabase();
        var baseCommandKey = commandName.ToLowerInvariant();
        var index = 0;

        while (true)
        {
            var overloadKey = $"{baseCommandKey}_overload_{index}";

            string argsStr = redisDb.HashGet($"{creds.RedisKey()}:commands:{localeName}", $"{overloadKey}::args");
            if (argsStr == default)
                break;

            string descStr = redisDb.HashGet($"{creds.RedisKey()}:commands:{localeName}", $"{overloadKey}::desc");
            string paramsStr = redisDb.HashGet($"{creds.RedisKey()}:commands:{localeName}", $"{overloadKey}::params");
            string signatureStr =
                redisDb.HashGet($"{creds.RedisKey()}:commands:{localeName}", $"{overloadKey}::signature");

            var args = Array.ConvertAll(argsStr.Split('&'), HttpUtility.UrlDecode);

            // Create the overload
            var overload = new CommandOverload
            {
                Args = args, Desc = descStr, Signature = signatureStr
            };

            // Parse parameters if they exist
            if (!string.IsNullOrEmpty(paramsStr))
            {
                try
                {
                    // Deserialize parameters from JSON
                    var parameters = JsonSerializer.Deserialize<List<ParameterString>>(
                        HttpUtility.UrlDecode(paramsStr));

                    if (parameters != null)
                        overload.Parameters = parameters;
                }
                catch (Exception)
                {
                    // If there's an error deserializing, just continue with empty parameters
                }
            }

            overloads.Add(overload);
            index++;
        }

        return overloads;
    }
}