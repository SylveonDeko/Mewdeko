using System.Collections.Immutable;

namespace Mewdeko.Services;

/// <summary>
///     Represents credentials required for the bot's functionality.
/// </summary>
public interface IBotCredentials
{
    /// <summary>
    ///     Gets the bot's token.
    /// </summary>
    public string Token { get; }


    /// <summary>
    ///     Gets the bot's Google API key.
    /// </summary>
    public string GoogleApiKey { get; }

    /// <summary>
    ///     The connections to use for connecting to redis. Use a ; separated list to use multiple.
    /// </summary>
    public string RedisConnections { get; }

    /// <summary>
    ///     Gets the IDs of the bot's owners.
    /// </summary>
    public ImmutableArray<ulong> OwnerIds { get; }


    /// <summary>
    ///     Gets the bot's Spotify client ID.
    /// </summary>
    public string SpotifyClientId { get; }

    /// <summary>
    ///     Gets the bot's Spotify client secret.
    /// </summary>
    public string SpotifyClientSecret { get; }

    /// <summary>
    ///     Gets the bot's osu! API key.
    /// </summary>
    public string OsuApiKey { get; }

    /// <summary>
    ///     Gets the path where chat logs are saved.
    /// </summary>
    public string ChatSavePath { get; }

    /// <summary>
    ///     Gets the total number of shards the bot has.
    /// </summary>
    public int TotalShards { get; }

    /// <summary>
    ///     Gets the bot's Twitch client secret.
    /// </summary>
    public string TwitchClientSecret { get; }

    /// <summary>
    ///     Gets the bot's Trovo client ID.
    /// </summary>
    public string TrovoClientId { get; }

    /// <summary>
    ///     Gets the bot's Kick client ID.
    /// </summary>
    public string KickClientId { get; }

    /// <summary>
    ///     Gets the bot's Kick client secret.
    /// </summary>
    public string KickClientSecret { get; }

    /// <summary>
    ///     Gets the token used for voting.
    /// </summary>
    public string VotesToken { get; }

    /// <summary>
    ///     Gets the bot's Twitch client ID.
    /// </summary>
    public string TwitchClientId { get; }

    /// <summary>
    ///     Gets the Open-Meteo API URL. Defaults to the public API, but can be set to a self-hosted instance.
    /// </summary>
    public string OpenMeteoApiUrl { get; }

    /// <summary>
    ///     Gets the channel ID for confession reports.
    /// </summary>
    public ulong ConfessionReportChannelId { get; }

    /// <summary>
    ///     Gets the bot's Cloudflare clearance cookie.
    /// </summary>
    public string CfClearance { get; }

    /// <summary>
    ///     Gets the bot's user agent used for bypassing Cloudflare.
    /// </summary>
    public string UserAgent { get; }

    /// <summary>
    ///     Gets the bot's CSRF token used for bypassing Cloudflare.
    /// </summary>
    public string CsrfToken { get; }

    /// <summary>
    ///     Last.fm API key
    /// </summary>
    public string LastFmApiKey { get; }


    /// <summary>
    ///     Gets the bot's Patreon client ID.
    /// </summary>
    public string PatreonClientId { get; }

    /// <summary>
    ///     Gets the bot's Patreon client secret.
    /// </summary>
    public string PatreonClientSecret { get; }

    /// <summary>
    ///     Gets the base URL for Patreon OAuth callbacks.
    /// </summary>
    public string PatreonBaseUrl { get; }

    /// <summary>
    ///     Gets or sets whether the PostgreSQL setup has been completed.
    /// </summary>
    public bool PostgresSetupCompleted { get; set; }

    /// <summary>
    ///     Gets the Sentry DSN for error tracking.
    /// </summary>
    public string SentryDsn { get; }

    /// <summary>
    ///     Checks if the given user is an owner of the bot.
    /// </summary>
    /// <param name="u">The user to check.</param>
    /// <returns><see langword="true" /> if the user is an owner; otherwise, <see langword="false" />.</returns>
    public bool IsOwner(IUser u);
}

/// <summary>
///     Represents configuration for restarting the bot.
/// </summary>
public class RestartConfig
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RestartConfig" /> class.
    /// </summary>
    /// <param name="cmd">The command used for restarting.</param>
    /// <param name="args">The arguments for restarting.</param>
    public RestartConfig(string cmd, string args)
    {
        Cmd = cmd;
        Args = args;
    }

    /// <summary>
    ///     Gets the command used for restarting.
    /// </summary>
    public string Cmd { get; }

    /// <summary>
    ///     Gets the arguments for restarting.
    /// </summary>
    public string Args { get; }
}

/// <summary>
///     Represents configuration for the database.
/// </summary>
public class DbConfig
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DbConfig" /> class.
    /// </summary>
    /// <param name="type">The type of database.</param>
    /// <param name="connectionString">The connection string for the database.</param>
    public DbConfig(string type, string connectionString)
    {
        Type = type;
        ConnectionString = connectionString;
    }

    /// <summary>
    ///     Gets the type of database.
    /// </summary>
    public string Type { get; }

    /// <summary>
    ///     Gets the connection string for the database.
    /// </summary>
    public string ConnectionString { get; }
}