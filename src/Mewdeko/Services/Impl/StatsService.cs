using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Humanizer;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Utility.Services;
using Swan.Formatters;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Service for collecting and posting statistics about the bot.
/// </summary>
public class StatsService : IStatsService, IDisposable, IReadyExecutor
{
    /// <summary>
    ///     The version of the bot. I should make this set from commits somehow idk
    /// </summary>
    public const string BotVersion = "8";

    private readonly IDataCache cache;
    private readonly IDiscordClient client;
    private readonly IBotCredentials creds;
    private readonly HttpClient http;
    private readonly ILogger<StatsService> logger;

    private readonly DateTime started;
    private PeriodicTimer topGgTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StatsService" /> class.
    /// </summary>
    /// <param name="client">The discord client</param>
    /// <param name="creds">The bots credentials</param>
    /// <param name="http">The http client</param>
    /// <param name="cache">The caching service</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <param name="logger">The logger instance for structured logging.</param>
    public StatsService(
        IDiscordClient client, IBotCredentials creds,
        HttpClient http, IDataCache cache, ILogger<StatsService> logger)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.creds = creds ?? throw new ArgumentNullException(nameof(creds));
        this.http = http ?? throw new ArgumentNullException(nameof(http));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.logger = logger;

        started = DateTime.UtcNow;

        _ = PostToTopGg();
    }

    /// <summary>
    ///     Disposes of the timers.
    /// </summary>
    public void Dispose()
    {
        topGgTimer?.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        _ = Task.Run(async () =>
        {
            var periodicTimer = new PeriodicTimer(TimeSpan.FromHours(12));

            do
            {
                try
                {
                    logger.LogInformation("Updating top guilds");
                    var guilds = (await client.GetGuildsAsync().ConfigureAwait(false))
                        .Cast<SocketGuild>();

                    var excludedTerms = new[]
                    {
                        "botlist", "bots", "xhamster", "nsfw", "18+"
                    };
                    const ulong excludedId = 374071874222686211;

                    var servers = guilds
                        .Where(x => x.Id != excludedId &&
                                    !excludedTerms.Any(term =>
                                        x.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                        .OrderByDescending(x => x.MemberCount)
                        .Take(11)
                        .Select(x => new MewdekoPartialGuild
                        {
                            IconUrl = x.IconId.StartsWith("a_") ? x.IconUrl.Replace(".jpg", ".gif") : x.IconUrl,
                            MemberCount = x.MemberCount,
                            Name = x.Name
                        })
                        .ToList();

                    var serialied = Json.Serialize(servers);
                    await cache.Redis.GetDatabase().StringSetAsync($"{client.CurrentUser.Id}_topguilds", serialied)
                        .ConfigureAwait(false);
                    logger.LogInformation("Updated top guilds");
                }
                catch (Exception e)
                {
                    logger.LogError("Failed to update top guilds: {0}", e);
                    return;
                }
            } while (await periodicTimer.WaitForNextTickAsync());
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Gets the version of the Discord.Net library.
    /// </summary>
    public string Library
    {
        get
        {
            return $"Discord.Net {DllVersionChecker.GetDllVersion()}";
        }
    }

    /// <summary>
    ///     Gets the memory usage of the bot.
    /// </summary>
    public string Heap
    {
        get
        {
            return ByteSize.FromBytes(Process.GetCurrentProcess().WorkingSet64).Megabytes
                .ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    ///     Gets the uptime of the bot as a human-readable string.
    /// </summary>
    /// <param name="separator">The separator</param>
    /// <returns>A string used in .stats to display uptime</returns>
    public string GetUptimeString(string separator = ", ")
    {
        return GetUptime().Humanize(2, minUnit: TimeUnit.Minute, collectionSeparator: separator);
    }


    private TimeSpan GetUptime()
    {
        return DateTime.UtcNow - started;
    }


    private async Task PostToTopGg()
    {
        if (string.IsNullOrEmpty(creds.VotesToken)) return;

        topGgTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (await topGgTimer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            var guilds = await client.GetGuildsAsync();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {
                    "shard_count", creds.TotalShards.ToString()
                },
                {
                    "server_count", guilds.Count.ToString()
                }
            });

            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Authorization", creds.VotesToken);
            var response = await http
                .PostAsync(new Uri($"https://top.gg/api/bots/{client.CurrentUser.Id}/stats"), content)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode) continue;
            logger.LogError("Failed to post stats to Top.gg: {0} {1} {2}", response.ReasonPhrase, response.StatusCode,
                response.Content);
            return;
        }
    }

    /// <summary>
    ///     Represents a partial guild information.
    /// </summary>
    public class MewdekoPartialGuild
    {
        /// <summary>
        ///     Gets or sets the name of the guild.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        ///     Gets or sets the URL of the guild's icon.
        /// </summary>
        public string? IconUrl { get; set; }

        /// <summary>
        ///     Gets or sets the number of members in the guild.
        /// </summary>
        public int MemberCount { get; set; }
    }
}