using System.Collections.Immutable;
using System.IO;
using Mewdeko.Modules.Help;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Npgsql;
using Serilog;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable UnassignedGetOnlyAutoProperty

namespace Mewdeko.Services.Impl;

/// <summary>
/// Represents the bot's credentials. This class is used to load the bot's credentials from a JSON file.
/// </summary>
public class BotCredentials : IBotCredentials
{
    private readonly string credsFileName = Path.Combine(Directory.GetCurrentDirectory(), "credentials.json");

    /// <summary>
    /// Initializes a new instance of the <see cref="BotCredentials"/> class.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public BotCredentials()
    {
        try
        {
            File.WriteAllText("./credentials_example.json",
                JsonConvert.SerializeObject(new CredentialsModel(), Formatting.Indented));
        }
        catch
        {
            // ignored
        }
        Log.Information(credsFileName);
        if (!File.Exists(credsFileName))
        {
            Log.Information("credentials.json is missing. Which of the following do you want to do?");
            Log.Information("1. Create a new credentials.json file using an interactive prompt");
            Log.Information("2. Load credentials from environment variables (Start the variables with Mewdeko_)");
            Log.Information("3. Exit the program");
            Log.Information("Enter the number of your choice: ");
            var choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    Log.Information(
                        "Please enter your bot's token. You can get it from https://discord.com/developers/applications");
                    var token = Console.ReadLine();
                    Log.Information(
                        "Please enter your ID and and any other IDs seperated by a space to mark them as owners. You can get your ID by enabling developer mode in discord and right clicking your name");
                    var owners = Console.ReadLine();
                    var ownersList = string.IsNullOrWhiteSpace(owners)
                        ? []
                        : owners.Split(' ').Select(ulong.Parse).ToList();
                    Log.Information("Please input your PostgreSQL Connection String.");
                    var model = new CredentialsModel
                    {
                        Token = token, OwnerIds = ownersList
                    };
                    File.WriteAllText(credsFileName, JsonConvert.SerializeObject(model, Formatting.Indented));
                    break;
                case "2":
                    break;
                case "3":
                    Environment.Exit(0);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        UpdateCredentials(null, null);
        if (MigrateToPsql) return;
        var watcher = new FileSystemWatcher(Directory.GetCurrentDirectory());
        watcher.NotifyFilter = NotifyFilters.LastWrite;
        watcher.Filter = "*.json";
        watcher.EnableRaisingEvents = true;
        watcher.Changed += UpdateCredentials;
    }

    /// <summary>
    /// Gets or sets the bot's Carbon key.
    /// </summary>
    public string CarbonKey { get; set; }

    /// <summary>
    /// Gets or sets the command used to run a shard.
    /// </summary>
    public string ShardRunCommand { get; set; }

    /// <summary>
    /// Gets or sets the arguments used to run a shard.
    /// </summary>
    public string ShardRunArguments { get; set; }

    /// <summary>
    /// Gets or sets the PostgreSQL connection string.
    /// </summary>
    public string PsqlConnectionString { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether the bot should use global currency.
    /// </summary>
    public bool UseGlobalCurrency { get; set; }

    /// <summary>
    /// Used for Mewdekos Api for dashboard requests.
    /// </summary>
    public string? ApiKey { get; set; } = "";

    /// <summary>
    /// Used for turnstile captcha on the dashboard for giveaways, may be used for other stuff, who knows
    /// </summary>
    public string TurnstileKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the URL for votes.
    /// </summary>
    public string VotesUrl { get; set; }

    /// <summary>
    /// Gets or sets the token for bot lists.
    /// </summary>
    public string BotListToken { get; set; }

    /// <summary>
    /// The IPs to use with redis, use a ; seperated list for multiple
    /// </summary>
    public string RedisConnections { get; set; }

    /// <summary>
    /// Gets or sets the API key for Coinmarketcap.
    /// </summary>
    public string CoinmarketcapApiKey { get; set; }

    /// <summary>
    /// Gets or sets the ID of the debug guild.
    /// </summary>
    public ulong DebugGuildId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where guild joins are reported.
    /// </summary>
    public ulong GuildJoinsChannelId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where global ban reports are sent.
    /// </summary>
    public ulong GlobalBanReportChannelId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where pronoun abuse reports are sent.
    /// </summary>
    public ulong PronounAbuseReportChannelId { get; set; }

    /// <summary>
    /// Gets or sets whether the bot should migrate to PostgreSQL.
    /// </summary>
    public bool MigrateToPsql { get; set; }

    /// <summary>
    /// Gets or sets the bot's token.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the bot's client secret.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the port the client coordinator is running on.
    /// </summary>
    public string ShardRunPort { get; set; }

    /// <summary>
    /// Gets or sets the bot's Google API key.
    /// </summary>
    public string GoogleApiKey { get; set; }

    /// <summary>
    /// Gets or sets the bot's Spotify client ID.
    /// </summary>
    public string SpotifyClientId { get; set; }

    /// <summary>
    /// Gets or sets the bot's Spotify client secret.
    /// </summary>
    public string SpotifyClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the bot's Mashape key.
    /// </summary>
    public string MashapeKey { get; set; }

    /// <summary>
    /// Gets or sets the bot's Statcord key.
    /// </summary>
    public string StatcordKey { get; set; }

    /// <summary>
    /// Gets or sets the bot's clearance cookie for Cloudflare.
    /// </summary>
    public string CfClearance { get; set; }

    /// <summary>
    /// Gets or sets the bot's user agent used for bypassing Cloudflare.
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the bot's CSRF token used for bypassing Cloudflare.
    /// </summary>
    public string CsrfToken { get; set; }

    /// <summary>
    /// Gets or sets the url for the Lavalink server.
    /// </summary>
    public string LavalinkUrl { get; set; }

    /// <summary>
    /// Gets or sets last.fm API key.
    /// </summary>
    public string LastFmApiKey { get; set; }

    /// <summary>
    /// Gets or sets last.fm API secret.
    /// </summary>
    public string LastFmApiSecret { get; set; }

    /// <summary>
    /// Gets or sets the bot's owner IDs.
    /// </summary>
    public ImmutableArray<ulong> OwnerIds { get; set; }

    /// <summary>
    /// Gets or sets the bot's osu! API key.
    /// </summary>
    public string OsuApiKey { get; set; }

    /// <summary>
    /// Gets or sets the key used for the bot's Cleverbot integration.
    /// </summary>
    public string CleverbotApiKey { get; set; }

    /// <summary>
    /// Gets or sets the command used to restart the bot.
    /// </summary>
    public RestartConfig RestartCommand { get; set; }

    /// <summary>
    /// Gets or sets the bot's total number of shards.
    /// </summary>
    public int TotalShards { get; set; }

    /// <summary>
    /// Gets or sets where the bot should save chat logs.
    /// </summary>
    public string ChatSavePath { get; set; }

    /// <summary>
    /// The url used for giveaway captchas
    /// </summary>
    public string GiveawayEntryUrl { get; set; }

    /// <summary>
    /// Gets or sets the Twitch client ID.
    /// </summary>
    public string TwitchClientId { get; set; }

    /// <summary>
    /// Gets or sets the Twitch client secret.
    /// </summary>
    public string TwitchClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the Trovo client ID.
    /// </summary>
    public string TrovoClientId { get; set; }

    /// <summary>
    /// Gets or sets the token for votes.
    /// </summary>
    public string VotesToken { get; set; }

    /// <summary>
    /// Gets or sets the Redis options.
    /// </summary>
    public string RedisOptions { get; set; }

    /// <summary>
    /// Gets or sets the API key for LocationIQ.
    /// </summary>
    public string LocationIqApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API key for TimezoneDB.
    /// </summary>
    public string TimezoneDbApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Genius API key.
    /// </summary>
    public string GeniusKey { get; set; }

    /// <summary>
    /// The http port for the api
    /// </summary>
    public int ApiPort { get; set; } = 5001;

    /// <summary>
    /// Used for debugging the mewdeko api and not needing a key every time
    /// </summary>
    public bool SkipApiKey { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where confession reports are sent.
    /// </summary>
    public ulong ConfessionReportChannelId { get; set; }

    /// <summary>
    /// Checks if the specified user is an owner of the bot.
    /// </summary>
    /// <param name="u">The user to check.</param>
    /// <returns>True if the user is an owner; otherwise, false.</returns>
    public bool IsOwner(IUser u) => OwnerIds.Contains(u.Id);

    private void UpdateCredentials(object ae, FileSystemEventArgs _)
    {
        try
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddJsonFile(credsFileName, true)
                .AddEnvironmentVariables("Mewdeko_");

            var data = configBuilder.Build();

            Token = data[nameof(Token)];
            OwnerIds = [..data.GetSection("OwnerIds").GetChildren().Select(c => ulong.Parse(c.Value))];
            TurnstileKey = data[nameof(TurnstileKey)];
            GiveawayEntryUrl = data[nameof(GiveawayEntryUrl)];
            GoogleApiKey = data[nameof(GoogleApiKey)];
            PsqlConnectionString = data[nameof(PsqlConnectionString)];
            CsrfToken = data[nameof(CsrfToken)];
            MigrateToPsql = bool.Parse(data[nameof(MigrateToPsql)] ?? "false");
            ApiKey = data[nameof(ApiKey)];
            UserAgent = data[nameof(UserAgent)];
            CfClearance = data[nameof(CfClearance)];
            ApiPort = int.TryParse(data[nameof(ApiPort)], out var port) ? port : 0;
            LastFmApiKey = data[nameof(LastFmApiKey)];
            LastFmApiSecret = data[nameof(LastFmApiSecret)];
            MashapeKey = data[nameof(MashapeKey)];
            OsuApiKey = data[nameof(OsuApiKey)];
            TwitchClientId = data[nameof(TwitchClientId)];
            TwitchClientSecret = data[nameof(TwitchClientSecret)];
            SkipApiKey = bool.Parse(data[nameof(SkipApiKey)]);
            LavalinkUrl = data[nameof(LavalinkUrl)];
            TrovoClientId = data[nameof(TrovoClientId)];
            ShardRunCommand = data[nameof(ShardRunCommand)];
            ShardRunArguments = data[nameof(ShardRunArguments)];
            ShardRunPort = data[nameof(ShardRunPort)] ?? "3444";
            CleverbotApiKey = data[nameof(CleverbotApiKey)];
            LocationIqApiKey = data[nameof(LocationIqApiKey)];
            TimezoneDbApiKey = data[nameof(TimezoneDbApiKey)];
            CoinmarketcapApiKey = data[nameof(CoinmarketcapApiKey)];
            SpotifyClientId = data[nameof(SpotifyClientId)];
            SpotifyClientSecret = data[nameof(SpotifyClientSecret)];
            StatcordKey = data[nameof(StatcordKey)];
            ChatSavePath = data[nameof(ChatSavePath)];
            ClientSecret = data[nameof(ClientSecret)];
            if (string.IsNullOrWhiteSpace(CoinmarketcapApiKey))
                CoinmarketcapApiKey = "e79ec505-0913-439d-ae07-069e296a6079";
            GeniusKey = data[nameof(GeniusKey)];

            RedisOptions = !string.IsNullOrWhiteSpace(data[nameof(RedisOptions)])
                ? data[nameof(RedisOptions)]
                : "127.0.0.1,syncTimeout=3000";

            VotesToken = data[nameof(VotesToken)];
            VotesUrl = data[nameof(VotesUrl)];
            BotListToken = data[nameof(BotListToken)];

            var restartSection = data.GetSection(nameof(RestartCommand));
            var cmd = restartSection["cmd"];
            var args = restartSection["args"];
            if (!string.IsNullOrWhiteSpace(cmd))
                RestartCommand = new RestartConfig(cmd, args);

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                if (string.IsNullOrWhiteSpace(ShardRunCommand))
                    ShardRunCommand = "dotnet";
                if (string.IsNullOrWhiteSpace(ShardRunArguments))
                    ShardRunArguments = "run -c Release --no-build -- {0} {1}";
            }
            else //windows
            {
                if (string.IsNullOrWhiteSpace(ShardRunCommand))
                    ShardRunCommand = "Mewdeko.exe";
                if (string.IsNullOrWhiteSpace(ShardRunArguments))
                    ShardRunArguments = "{0} {1}";
            }

            if (!int.TryParse(data[nameof(TotalShards)], out var ts))
                ts = 0;
            TotalShards = ts < 1 ? 1 : ts;

            CarbonKey = data[nameof(CarbonKey)];

            TwitchClientId = data[nameof(TwitchClientId)];
            if (string.IsNullOrWhiteSpace(TwitchClientId)) TwitchClientId = "67w6z9i09xv2uoojdm9l0wsyph4hxo6";
            RedisConnections = data[nameof(RedisConnections)];

            DebugGuildId = ulong.TryParse(data[nameof(DebugGuildId)], out var dgid) ? dgid : 843489716674494475;
            GuildJoinsChannelId = ulong.TryParse(data[nameof(GuildJoinsChannelId)], out var gjid)
                ? gjid
                : 892789588739891250;
            ConfessionReportChannelId = ulong.TryParse(data[nameof(ConfessionReportChannelId)], out var crid)
                ? crid
                : 942825117820530709;
            GlobalBanReportChannelId = ulong.TryParse(data[nameof(GlobalBanReportChannelId)], out var gbrid)
                ? gbrid
                : 905109141620682782;
            PronounAbuseReportChannelId = ulong.TryParse(data[nameof(PronounAbuseReportChannelId)], out var pnrepId)
                ? pnrepId
                : 970086914826858547;
            UseGlobalCurrency = bool.TryParse(data[nameof(UseGlobalCurrency)], out var ugc) && ugc;

            if (string.IsNullOrWhiteSpace(Token))
            {
                Log.Error(
                    "Token is missing from credentials.json or Environment variables. Add it and restart the program");
                Helpers.ReadErrorAndExit(5);
            }

            if (string.IsNullOrWhiteSpace(PsqlConnectionString))
            {
                Log.Error("Postgres connection string is missing. Please add and restart.");
                Helpers.ReadErrorAndExit(5);
            }

            if (string.IsNullOrWhiteSpace(RedisConnections))
            {
                Log.Error("Redis connection string is missing. Please add and restart.");
                Helpers.ReadErrorAndExit(5);
            }

            if (ApiPort is not (0 or < 0))
            {
                Log.Error("Invalid Api Port specified, Please change and restart.");

                Helpers.ReadErrorAndExit(5);
            }

            if (ApiPort > 65535)
                Log.Error("Maximum port number is 65535. Lower your port value and restart.");
        }
        catch (Exception ex)
        {
            Log.Error("JSON serialization has failed. Fix your credentials file and restart the bot");
            Log.Fatal(ex.ToString());
            Helpers.ReadErrorAndExit(6);
        }
    }

    /// <summary>
    ///  Used for creating a new credentials.json file.
    /// </summary>
    private class CredentialsModel : IBotCredentials
    {
        public List<ulong> OwnerIds { get; set; } = [280835732728184843, 786375627892064257];

        public ulong[] OfficialMods { get; set; } =
        [
            280835732728184843, 786375627892064257
        ];

        public bool UseGlobalCurrency { get; set; } = false;

        public string SoundCloudClientId { get; set; } = "";
        public string RestartCommand { get; set; } = null;

        public string CarbonKey { get; } = "";
        public string PatreonAccessToken { get; } = "";
        public string PatreonCampaignId { get; } = "334038";
        public string RedisConnections { get; } = "127.0.0.1:6379";

        public string ShardRunCommand { get; } = "";
        public string ShardRunArguments { get; } = "";
        public string TurnstileKey { get; } = "";
        public string GiveawayEntryUrl { get; } = "";

        public string BotListToken { get; set; }
        public string VotesUrl { get; set; }
        public string PsqlConnectionString { get; set; }  =
            "Server=ServerIp;Database=DatabaseName;Port=PsqlPort;UID=PsqlUser;Password=UserPassword";

        public string ApiKey { get; set; }

        public string CoinmarketcapApiKey { get; set; }

        public ulong DebugGuildId { get; set; } = 843489716674494475;
        public ulong GuildJoinsChannelId { get; set; } = 892789588739891250;
        public ulong GlobalBanReportChannelId { get; set; } = 905109141620682782;
        public ulong PronounAbuseReportChannelId { get; set; } = 970086914826858547;
        public bool MigrateToPsql { get; set; } = false;

        public string LastFmApiKey { get; set; }
        public string LastFmApiSecret { get; set; }

        public string Token { get; set; } = "";
        public string ClientSecret { get; } = "";
        public string GeniusKey { get; set; }
        public string CfClearance { get; set; }
        public string UserAgent { get; set; }
        public string CsrfToken { get; set; }
        public string LavalinkUrl { get; set; } = "http://localhost:2333";
        public string SpotifyClientId { get; set; } = "";
        public string SpotifyClientSecret { get; set; } = "";
        public string StatcordKey { get; set; } = "";
        public string ShardRunPort { get; set; } = "3444";

        public string GoogleApiKey { get; } = "";
        public string MashapeKey { get; } = "";
        public string OsuApiKey { get; } = "";
        public string TrovoClientId { get; } = "";
        public string TwitchClientId { get; } = "";
        public string CleverbotApiKey { get; } = "";
        public int TotalShards { get; } = 1;
        public string TwitchClientSecret { get; set; }
        public string VotesToken { get; set; }
        public string RedisOptions { get; set; }
        public string LocationIqApiKey { get; set; }
        public string TimezoneDbApiKey { get; set; }
        public ulong ConfessionReportChannelId { get; set; } = 942825117820530709;
        public string ChatSavePath { get; set; } = "/usr/share/nginx/cdn/chatlogs/";

        [JsonIgnore]
        ImmutableArray<ulong> IBotCredentials.OwnerIds { get; }

        [JsonIgnore]
        RestartConfig IBotCredentials.RestartCommand { get; }

        public bool IsOwner(IUser u) => throw new NotImplementedException();
    }
}