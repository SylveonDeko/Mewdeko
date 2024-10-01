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
    string Token { get; }

    /// <summary>
    ///     Gets the bot's client secret.
    /// </summary>
    string ClientSecret { get; }

    /// <summary>
    ///     Gets the bot's Google API key.
    /// </summary>
    string GoogleApiKey { get; }

    /// <summary>
    ///     The connections to use for connecting to redis. Use a ; separated list to use multiple.
    /// </summary>
    string RedisConnections { get; }

    /// <summary>
    ///     Gets the IDs of the bot's owners.
    /// </summary>
    ImmutableArray<ulong> OwnerIds { get; }

    /// <summary>
    ///     Gets the bot's Statcord key.
    /// </summary>
    string StatcordKey { get; }

    /// <summary>
    ///     Gets the bot's Mashape key.
    /// </summary>
    string MashapeKey { get; }

    /// <summary>
    ///     Gets the bot's Spotify client ID.
    /// </summary>
    string SpotifyClientId { get; }

    /// <summary>
    ///     Gets the bot's Spotify client secret.
    /// </summary>
    string SpotifyClientSecret { get; }

    /// <summary>
    ///     Gets the bot's osu! API key.
    /// </summary>
    string OsuApiKey { get; }

    /// <summary>
    ///     Gets the path where chat logs are saved.
    /// </summary>
    string ChatSavePath { get; }

    /// <summary>
    ///     Gets the total number of shards the bot has.
    /// </summary>
    int TotalShards { get; }

    /// <summary>
    ///     Gets the bot's Twitch client secret.
    /// </summary>
    string TwitchClientSecret { get; }

    /// <summary>
    ///     Gets the bot's Trovo client ID.
    /// </summary>
    string TrovoClientId { get; }

    /// <summary>
    ///     Gets the command used to restart the bot.
    /// </summary>
    RestartConfig RestartCommand { get; }

    /// <summary>
    ///     Gets the token used for voting.
    /// </summary>
    string VotesToken { get; }

    /// <summary>
    ///     Gets the bot's Twitch client ID.
    /// </summary>
    string TwitchClientId { get; }

    /// <summary>
    ///     Gets the LocationIQ API key.
    /// </summary>
    string LocationIqApiKey { get; }

    /// <summary>
    ///     Gets the TimezoneDB API key.
    /// </summary>
    string TimezoneDbApiKey { get; }

    /// <summary>
    ///     Gets the channel ID for confession reports.
    /// </summary>
    ulong ConfessionReportChannelId { get; }

    /// <summary>
    ///     Gets the bot's Cloudflare clearance cookie.
    /// </summary>
    string CfClearance { get; }

    /// <summary>
    ///     Gets the bot's user agent used for bypassing Cloudflare.
    /// </summary>
    string UserAgent { get; }

    /// <summary>
    ///     Gets the bot's CSRF token used for bypassing Cloudflare.
    /// </summary>
    string CsrfToken { get; }

    /// <summary>
    ///     Last.fm API key
    /// </summary>
    string LastFmApiKey { get; }

    /// <summary>
    ///     Last.fm API secret
    /// </summary>
    string LastFmApiSecret { get; }

    /// <summary>
    ///     Checks if the given user is an owner of the bot.
    /// </summary>
    /// <param name="u">The user to check.</param>
    /// <returns><see langword="true" /> if the user is an owner; otherwise, <see langword="false" />.</returns>
    bool IsOwner(IUser u);
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