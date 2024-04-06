using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using Discord.Commands;
using Humanizer;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Utility.Services;
using Serilog;
using Swan.Formatters;

namespace Mewdeko.Services.Impl
{
    /// <summary>
    /// Service for collecting and posting statistics about the bot.
    /// </summary>
    public class StatsService : IStatsService, IReadyExecutor, IDisposable
    {
        private readonly DiscordSocketClient client;
        private readonly IBotCredentials creds;
        private readonly ICoordinator coord;
        private readonly HttpClient http;
        private readonly IDataCache cache;

        /// <summary>
        /// The version of the bot. I should make this set from commits somehow idk
        /// </summary>
        public const string BotVersion = "8";

        private readonly DateTime started;
        private PeriodicTimer statcordTimer;
        private PeriodicTimer topGgTimer;
        private PeriodicTimer updateGuildsTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatsService"/> class.
        /// </summary>
        /// <param name="client">The discord client</param>
        /// <param name="creds">The bots credentials</param>
        /// <param name="coord">The coordinator service</param>
        /// <param name="cmdServ">The command service</param>
        /// <param name="http">The http client</param>
        /// <param name="cache">The caching service</param>
        /// <exception cref="ArgumentNullException"></exception>
        public StatsService(
            DiscordSocketClient client, IBotCredentials creds, ICoordinator coord, CommandService cmdServ,
            HttpClient http, IDataCache cache)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.creds = creds ?? throw new ArgumentNullException(nameof(creds));
            this.coord = coord ?? throw new ArgumentNullException(nameof(coord));
            this.http = http ?? throw new ArgumentNullException(nameof(http));
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));

            started = DateTime.UtcNow;

            InitializePeriodicTasks(cmdServ);
        }

        private void InitializePeriodicTasks(CommandService cmdServ)
        {
            _ = PostToTopGg();
            _ = PostToStatcord(coord, client, cmdServ);
            _ = OnReadyAsync();
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

        private TimeSpan GetUptime() => DateTime.UtcNow - started;

        /// <summary>
        /// Gets the uptime of the bot as a human-readable string.
        /// </summary>
        /// <param name="separator">The separator</param>
        /// <returns>A string used in .stats to display uptime</returns>
        public string GetUptimeString(string separator = ", ") =>
            GetUptime().Humanize(2, minUnit: TimeUnit.Minute, collectionSeparator: separator);

        private async Task PostToStatcord(ICoordinator coord, DiscordSocketClient socketClient, CommandService cmdServ)
        {
            if (string.IsNullOrWhiteSpace(creds.StatcordKey)) return;

            statcordTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            while (await statcordTimer.WaitForNextTickAsync().ConfigureAwait(false))
            {
                var memoryUsage = ByteSize.FromBytes(Process.GetCurrentProcess().PrivateMemorySize64).Bytes;
                var content = Json.Serialize(new
                {
                    id = socketClient.CurrentUser.Id.ToString(),
                    key = creds.StatcordKey,
                    servers = coord.GetGuildCount().ToString(),
                    users = coord.GetUserCount().ToString(),
                    active = Array.Empty<string>(),
                    commands = "0",
                    popular = Array.Empty<string>(),
                    memactive = memoryUsage.ToString(),
                    memload = "0",
                    cpuload = "0",
                    bandwidth = "0",
                    custom1 = cmdServ.Commands.Count().ToString()
                });

                var stringContent = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
                await http.PostAsync("https://api.statcord.com/beta/stats", stringContent).ConfigureAwait(false);
            }
        }

        private async Task PostToTopGg()
        {
            if (client.ShardId != 0 || string.IsNullOrEmpty(creds.VotesToken)) return;

            topGgTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));

            while (await topGgTimer.WaitForNextTickAsync().ConfigureAwait(false))
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {
                        "shard_count", creds.TotalShards.ToString()
                    },
                    {
                        "shard_id", client.ShardId.ToString()
                    },
                    {
                        "server_count", coord.GetGuildCount().ToString()
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

        /// <inheritdoc/>
        public async Task OnReadyAsync()
        {
            if (client.ShardId != 0)
                return;

            var periodicTimer = new PeriodicTimer(TimeSpan.FromHours(12));

            do
            {
                try
                {
                    Log.Information("Updating top guilds");
                    var guilds = await client.Rest.GetGuildsAsync(true);
                    var servers = guilds.OrderByDescending(x => x.ApproximateMemberCount.Value)
                        .Where(x => !x.Name.ToLower().Contains("botlist")).Take(11).Select(x =>
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
                catch (Exception e)
                {
                    Log.Error("Failed to update top guilds: {0}", e);
                }
            } while (await periodicTimer.WaitForNextTickAsync());
        }

        /// <summary>
        /// Disposes of the timers.
        /// </summary>
        public void Dispose()
        {
            statcordTimer.Dispose();
            topGgTimer.Dispose();
            updateGuildsTimer.Dispose();
        }

        /// <summary>
        /// Represents a partial guild information.
        /// </summary>
        public class MewdekoPartialGuild
        {
            /// <summary>
            /// Gets or sets the name of the guild.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the URL of the guild's icon.
            /// </summary>
            public string IconUrl { get; set; }

            /// <summary>
            /// Gets or sets the number of members in the guild.
            /// </summary>
            public int MemberCount { get; set; }
        }
    }
}