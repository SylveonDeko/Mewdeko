using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using Discord.Commands;
using Humanizer;
using Mewdeko.Modules.Utility.Services;
using Serilog;
using Swan.Formatters;

namespace Mewdeko.Services.Impl
{
    /// <summary>
    /// Service for collecting and posting statistics about the bot.
    /// </summary>
    public class StatsService : IStatsService, IDisposable
    {
        /// <summary>
        /// The version of the bot. I should make this set from commits somehow idk
        /// </summary>
        public const string BotVersion = "8";

        private readonly IDataCache cache;
        private readonly DiscordShardedClient client;
        private readonly IBotCredentials creds;
        private readonly HttpClient http;

        private readonly DateTime started;
        private PeriodicTimer topGgTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatsService"/> class.
        /// </summary>
        /// <param name="client">The discord client</param>
        /// <param name="creds">The bots credentials</param>
        /// <param name="cmdServ">The command service</param>
        /// <param name="http">The http client</param>
        /// <param name="cache">The caching service</param>
        /// <exception cref="ArgumentNullException"></exception>
        public StatsService(
            DiscordShardedClient client, IBotCredentials creds, CommandService cmdServ,
            HttpClient http, IDataCache cache)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.creds = creds ?? throw new ArgumentNullException(nameof(creds));
            this.http = http ?? throw new ArgumentNullException(nameof(http));
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));

            started = DateTime.UtcNow;

            _ = Task.Run(async () => await PostToTopGg());
            _ = OnReadyAsync();
        }

        /// <summary>
        /// Disposes of the timers.
        /// </summary>
        public void Dispose()
        {
            topGgTimer?.Dispose();


        }

        /// <inheritdoc/>
        public Task OnReadyAsync()
        {
            _ = Task.Run(async () =>
            {
                var periodicTimer = new PeriodicTimer(TimeSpan.FromHours(12));

                do
                {
                    try
                    {
                        Log.Information("Updating top guilds");
                        var guilds = await client.Rest.GetGuildsAsync(true);
                        var servers = guilds.OrderByDescending(x => x.ApproximateMemberCount.Value)
                            .Where(x => !x.Name.Contains("botlist", StringComparison.CurrentCultureIgnoreCase)).Take(11).Select(x =>
                                new MewdekoPartialGuild
                                {
                                    IconUrl = x.IconId.StartsWith("a_") ? x.IconUrl.Replace(".jpg", ".gif") : x.IconUrl,
                                    MemberCount = x.ApproximateMemberCount.Value,
                                    Name = x.Name
                                });

                        var serialied = Json.Serialize(servers);
                        await cache.Redis.GetDatabase().StringSetAsync($"{client.CurrentUser.Id}_topguilds", serialied)
                            .ConfigureAwait(false);
                        Log.Information("Updated top guilds");
                    }
                    catch
                    {
                        Log.Error("Failed to update top guilds: {0}");
                    }
                } while (await periodicTimer.WaitForNextTickAsync());
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the version of the Discord.Net library.
        /// </summary>
        public string Library => $"Discord.Net {DllVersionChecker.GetDllVersion()}";

        /// <summary>
        /// Gets the memory usage of the bot.
        /// </summary>
        public string Heap => ByteSize.FromBytes(Process.GetCurrentProcess().PrivateMemorySize64).Megabytes
            .ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Gets the uptime of the bot as a human-readable string.
        /// </summary>
        /// <param name="separator">The separator</param>
        /// <returns>A string used in .stats to display uptime</returns>
        public string GetUptimeString(string separator = ", ") =>
            GetUptime().Humanize(2, minUnit: TimeUnit.Minute, collectionSeparator: separator);


        private TimeSpan GetUptime() => DateTime.UtcNow - started;


        private async Task PostToTopGg()
        {
            if (string.IsNullOrEmpty(creds.VotesToken)) return;

            topGgTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));

            while (await topGgTimer.WaitForNextTickAsync().ConfigureAwait(false))
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {
                        "shard_count", creds.TotalShards.ToString()
                    },
                    {
                        "server_count", client.Guilds.Count.ToString()
                    }
                });

                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Authorization", creds.VotesToken);
                var response = await http
                    .PostAsync(new Uri($"https://top.gg/api/bots/{client.CurrentUser.Id}/stats"), content)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Error("Failed to post stats to Top.gg");
                }
            }
        }

        /// <summary>
        /// Represents a partial guild information.
        /// </summary>
        private class MewdekoPartialGuild
        {
            /// <summary>
            /// Gets or sets the name of the guild.
            /// </summary>
            public string? Name { get; set; }

            /// <summary>
            /// Gets or sets the URL of the guild's icon.
            /// </summary>
            public string? IconUrl { get; set; }

            /// <summary>
            /// Gets or sets the number of members in the guild.
            /// </summary>
            public int MemberCount { get; set; }
        }
    }
}