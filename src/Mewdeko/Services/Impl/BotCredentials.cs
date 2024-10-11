using System.Collections.Immutable;
using System.IO;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Npgsql;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Represents the bot's credentials. This class is used to load the bot's credentials from a JSON file.
/// </summary>
public class BotCredentials : IBotCredentials
{
    private readonly string credsFileName = Path.Combine(Directory.GetCurrentDirectory(), "credentials.json");

    /// <summary>
    ///     Initializes a new instance of the <see cref="BotCredentials" /> class.
    /// </summary>
    public BotCredentials()
    {
        try
        {
            var exampleCredentialsPath = "./credentials_example.json";
            if (!File.Exists(exampleCredentialsPath))
            {
                File.WriteAllText(exampleCredentialsPath,
                    JsonConvert.SerializeObject(new CredentialsModel(), Formatting.Indented));
            }
        }
        catch (Exception ex)
        {
            Log.Error("Failed to write the credentials example file.");
            Log.Error(ex.Message);
        }

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
                    CreateCredentialsFileInteractively();
                    break;
                case "2":
                    // No action needed as it will load from environment variables
                    break;
                case "3":
                    Environment.Exit(0);
                    break;
                default:
                    Log.Error("Invalid choice. Please restart the program and select a valid option.");
                    Environment.Exit(0);
                    break;
            }
        }

        UpdateCredentials(null, null);
    }

    // Properties (same as before)


    /// <summary>
    ///     Gets or sets the command used to run a shard.
    /// </summary>
    public string ShardRunCommand { get; set; }

    /// <summary>
    ///     Gets or sets the arguments used with the shard run command.
    /// </summary>
    public string ShardRunArguments { get; set; }

    /// <summary>
    ///     Gets or sets the PostgreSQL connection string.
    /// </summary>
    public string PsqlConnectionString { get; set; }

    // /// <summary>
    // /// Gets or sets whether this is the master mewdeko instance
    // /// </summary>
    // public bool IsMasterInstance { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether to use global currency.
    /// </summary>
    public bool UseGlobalCurrency { get; set; }

    /// <summary>
    ///     Gets or sets the API key used for the bot's API.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Turnstile key used for captcha verification.
    /// </summary>
    public string TurnstileKey { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether the api is enabled or disabled. When set to disabled, no controllers or urls are added on
    ///     boot, so theres no way to interact with the api.
    /// </summary>
    public bool IsApiEnabled { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the debug guild.
    /// </summary>
    public ulong DebugGuildId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the channel where guild joins are reported.
    /// </summary>
    public ulong GuildJoinsChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the channel where global ban reports are sent.
    /// </summary>
    public ulong GlobalBanReportChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the channel where pronoun abuse reports are sent.
    /// </summary>
    public ulong PronounAbuseReportChannelId { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to migrate to PostgreSQL.
    /// </summary>
    public bool MigrateToPsql { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the Lavalink server.
    /// </summary>
    public string LavalinkUrl { get; set; }

    /// <summary>
    ///     Gets or sets the Cleverbot API key.
    /// </summary>
    public string CleverbotApiKey { get; set; }

    /// <summary>
    ///     Gets or sets the URL used for giveaway entries.
    /// </summary>
    public string GiveawayEntryUrl { get; set; }

    /// <summary>
    ///     Gets or sets the Redis options.
    /// </summary>
    public string RedisOptions { get; set; }

    /// <summary>
    ///     Gets or sets the port used for the API.
    /// </summary>
    public int ApiPort { get; set; } = 5001;

    /// <summary>
    ///     Gets or sets a value indicating whether to skip API key verification.
    /// </summary>
    public bool SkipApiKey { get; set; }

    /// <summary>
    ///     Gets or sets the Redis connection strings, separated by semicolons for multiple connections.
    /// </summary>
    public string RedisConnections { get; set; }

    /// <summary>
    ///     Gets or sets the bot's token.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    ///     Gets or sets the bot's client secret.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    ///     Gets or sets the Google API key.
    /// </summary>
    public string GoogleApiKey { get; set; }

    /// <summary>
    ///     Gets or sets the Spotify client ID.
    /// </summary>
    public string SpotifyClientId { get; set; }

    /// <summary>
    ///     Gets or sets the Spotify client secret.
    /// </summary>
    public string SpotifyClientSecret { get; set; }

    /// <summary>
    ///     Gets or sets the Mashape (now RapidAPI) key.
    /// </summary>
    public string MashapeKey { get; set; }

    /// <summary>
    ///     Gets or sets the Statcord key used for bot statistics.
    /// </summary>
    public string StatcordKey { get; set; }

    /// <summary>
    ///     Gets or sets the Cloudflare clearance token.
    /// </summary>
    public string CfClearance { get; set; }

    /// <summary>
    ///     Gets or sets the user agent string used for web requests.
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    ///     Gets or sets the CSRF token.
    /// </summary>
    public string CsrfToken { get; set; }

    /// <summary>
    ///     Gets or sets the Last.fm API key.
    /// </summary>
    public string LastFmApiKey { get; set; }

    /// <summary>
    ///     Gets or sets the Last.fm API secret.
    /// </summary>
    public string LastFmApiSecret { get; set; }

    /// <summary>
    ///     Gets or sets the list of owner IDs.
    /// </summary>
    public ImmutableArray<ulong> OwnerIds { get; set; }

    /// <summary>
    ///     Gets or sets the osu! API key.
    /// </summary>
    public string OsuApiKey { get; set; }

    /// <summary>
    ///     Gets or sets the configuration for the restart command.
    /// </summary>
    public RestartConfig RestartCommand { get; set; }

    /// <summary>
    ///     Gets or sets the total number of shards.
    /// </summary>
    public int TotalShards { get; set; }

    /// <summary>
    ///     Gets or sets the path where chat logs are saved.
    /// </summary>
    public string ChatSavePath { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch client ID.
    /// </summary>
    public string TwitchClientId { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch client secret.
    /// </summary>
    public string TwitchClientSecret { get; set; }

    /// <summary>
    ///     Gets or sets the Trovo client ID.
    /// </summary>
    public string TrovoClientId { get; set; }

    /// <summary>
    ///     Gets or sets the token used for votes.
    /// </summary>
    public string VotesToken { get; set; }

    /// <summary>
    ///     Gets or sets the LocationIQ API key.
    /// </summary>
    public string LocationIqApiKey { get; set; }

    /// <summary>
    ///     Gets or sets the TimezoneDB API key.
    /// </summary>
    public string TimezoneDbApiKey { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the channel where confession reports are sent.
    /// </summary>
    public ulong ConfessionReportChannelId { get; set; }

    /// <summary>
    ///     Checks if the specified user is an owner.
    /// </summary>
    /// <param name="u">The user to check.</param>
    /// <returns><c>true</c> if the user is an owner; otherwise, <c>false</c>.</returns>
    public bool IsOwner(IUser u)
    {
        return OwnerIds.Contains(u.Id);
    }

    private void CreateCredentialsFileInteractively()
    {
        Log.Information(
            "Please enter your bot's token. You can get it from https://discord.com/developers/applications");
        var token = Console.ReadLine();

        while (string.IsNullOrWhiteSpace(token))
        {
            Log.Error("Bot token cannot be empty. Please enter a valid token:");
            token = Console.ReadLine();
        }

        Log.Information(
            "Please enter your ID and any other IDs separated by a space to mark them as owners. You can get your ID by enabling developer mode in Discord and right-clicking your name");
        var ownersInput = Console.ReadLine();
        var ownersList = new List<ulong>();

        if (!string.IsNullOrWhiteSpace(ownersInput))
        {
            var ownerIds = ownersInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var ownerId in ownerIds)
            {
                if (ulong.TryParse(ownerId, out var parsedId))
                {
                    ownersList.Add(parsedId);
                }
                else
                {
                    Log.Warning($"'{ownerId}' is not a valid ID and will be ignored.");
                }
            }
        }

        Log.Information("Please input your PostgreSQL Connection String.");
        var psqlConnectionString = Console.ReadLine();

        while (string.IsNullOrWhiteSpace(psqlConnectionString))
        {
            Log.Error("PostgreSQL Connection String cannot be empty. Please enter a valid connection string:");
            psqlConnectionString = Console.ReadLine();
        }

        var model = new CredentialsModel
        {
            Token = token, OwnerIds = ownersList, PsqlConnectionString = psqlConnectionString
        };

        try
        {
            File.WriteAllText(credsFileName, JsonConvert.SerializeObject(model, Formatting.Indented));
            Log.Information("credentials.json has been created successfully.");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to write credentials.json file.");
            Log.Error(ex.Message);
            Environment.Exit(1);
        }
    }

    private void UpdateCredentials(object sender, FileSystemEventArgs e)
    {
        try
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddJsonFile(credsFileName, true)
                .AddEnvironmentVariables("Mewdeko_");

            var data = configBuilder.Build();

            Token = data[nameof(Token)];
            OwnerIds = [
                ..data.GetSection(nameof(OwnerIds)).GetChildren()
                    .Select(c => ulong.Parse(c.Value))
            ];
            TurnstileKey = data[nameof(TurnstileKey)];
            GiveawayEntryUrl = data[nameof(GiveawayEntryUrl)];
            GoogleApiKey = data[nameof(GoogleApiKey)];
            PsqlConnectionString = data[nameof(PsqlConnectionString)];
            CsrfToken = data[nameof(CsrfToken)];
            MigrateToPsql = bool.Parse(data[nameof(MigrateToPsql)] ?? "false");
            ApiKey = data[nameof(ApiKey)];
            UserAgent = data[nameof(UserAgent)];
            CfClearance = data[nameof(CfClearance)];
            ApiPort = int.TryParse(data[nameof(ApiPort)], out var port) ? port : 5001;
            LastFmApiKey = data[nameof(LastFmApiKey)];
            LastFmApiSecret = data[nameof(LastFmApiSecret)];
            MashapeKey = data[nameof(MashapeKey)];
            OsuApiKey = data[nameof(OsuApiKey)];
            TwitchClientId = data[nameof(TwitchClientId)];
            TwitchClientSecret = data[nameof(TwitchClientSecret)];
            SkipApiKey = bool.Parse(data[nameof(SkipApiKey)] ?? "false");
            LavalinkUrl = data[nameof(LavalinkUrl)];
            TrovoClientId = data[nameof(TrovoClientId)];
            ShardRunCommand = data[nameof(ShardRunCommand)];
            ShardRunArguments = data[nameof(ShardRunArguments)];
            CleverbotApiKey = data[nameof(CleverbotApiKey)];
            // IsMasterInstance = Convert.ToBoolean(data[nameof(IsMasterInstance)]);
            LocationIqApiKey = data[nameof(LocationIqApiKey)];
            SpotifyClientId = data[nameof(SpotifyClientId)];
            SpotifyClientSecret = data[nameof(SpotifyClientSecret)];
            StatcordKey = data[nameof(StatcordKey)];
            ChatSavePath = data[nameof(ChatSavePath)];
            IsApiEnabled = bool.Parse(data[nameof(IsApiEnabled)] ?? "false");
            ClientSecret = data[nameof(ClientSecret)];

            RedisOptions = !string.IsNullOrWhiteSpace(data[nameof(RedisOptions)])
                ? data[nameof(RedisOptions)]
                : "127.0.0.1,syncTimeout=3000";

            VotesToken = data[nameof(VotesToken)];

            var restartSection = data.GetSection(nameof(RestartCommand));
            var cmd = restartSection["cmd"];
            var args = restartSection["args"];
            if (!string.IsNullOrWhiteSpace(cmd))
                RestartCommand = new RestartConfig(cmd, args);

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                ShardRunCommand ??= "dotnet";
                ShardRunArguments ??= "run -c Release --no-build -- {0} {1}";
            }
            else // Windows
            {
                ShardRunCommand ??= "Mewdeko.exe";
                ShardRunArguments ??= "{0} {1}";
            }

            TotalShards = int.TryParse(data[nameof(TotalShards)], out var ts) && ts > 0 ? ts : 1;

            TwitchClientId = data[nameof(TwitchClientId)] ?? "67w6z9i09xv2uoojdm9l0wsyph4hxo6";
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
                Log.Error("PostgreSQL connection string is missing. Please add it and restart.");
                Helpers.ReadErrorAndExit(5);
            }
            else
            {
                // Check if PostgreSQL connection string is valid
                try
                {
                    using var conn = new NpgsqlConnection(PsqlConnectionString);
                    conn.Open();
                    conn.Close();
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to connect to PostgreSQL database with the provided connection string.");
                    Log.Error(ex.Message);
                    Helpers.ReadErrorAndExit(6);
                }
            }

            if (string.IsNullOrWhiteSpace(RedisConnections))
            {
                Log.Error("Redis connection string is missing. Please add it and restart.");
                Helpers.ReadErrorAndExit(5);
            }
            else
            {
                // Check if Redis is running
                try
                {
                    var connect = ConnectionMultiplexer.Connect(RedisConnections.Split(";")[0]);
                    connect.Close();
                }
                catch
                {
                    Log.Error("Redis is not running! Make sure it's installed and running, then restart the bot.");
                    Helpers.ReadErrorAndExit(6);
                }
            }

            if (ApiPort <= 0 || ApiPort > 65535)
            {
                Log.Error("Invalid API Port specified. Please change it to a value between 1 and 65535 and restart.");
                Helpers.ReadErrorAndExit(5);
            }
        }
        catch (Exception ex)
        {
            Log.Error(
                "An error occurred while loading the credentials. Please fix your credentials file and restart the bot.");
            Log.Fatal(ex.ToString());
            Helpers.ReadErrorAndExit(6);
        }
    }

    /// <summary>
    ///     Used for creating a new credentials.json file.
    /// </summary>
    private class CredentialsModel : IBotCredentials
    {
        public List<ulong> OwnerIds { get; set; } = new()
        {
            280835732728184843, 786375627892064257
        };

        public bool UseGlobalCurrency { get; set; } = false;
        public string TurnstileKey { get; set; } = "";
        public string GiveawayEntryUrl { get; set; } = "";

        public string PsqlConnectionString { get; set; } =
            "Server=ServerIp;Database=DatabaseName;Port=PsqlPort;UID=PsqlUser;Password=UserPassword";

        public string ApiKey { get; set; } = StringExtensions.GenerateSecureString(90);
        public ulong DebugGuildId { get; set; } = 843489716674494475;
        public ulong GuildJoinsChannelId { get; set; } = 892789588739891250;
        public ulong GlobalBanReportChannelId { get; set; } = 905109141620682782;
        public ulong PronounAbuseReportChannelId { get; set; } = 970086914826858547;
        public bool MigrateToPsql { get; set; } = false;
        public bool IsApiEnabled { get; set; } = false;
        public string LavalinkUrl { get; set; } = "http://localhost:2333";
        public string CleverbotApiKey { get; set; } = "";
        public string RedisOptions { get; set; } = "127.0.0.1,syncTimeout=3000";
        public int ApiPort { get; set; } = 5001;
        public bool SkipApiKey { get; set; } = false;
       // public bool IsMasterInstance { get; set; } = false;
        public RestartConfig RestartCommand { get; } = null;
        public string RedisConnections { get; } = "127.0.0.1:6379";
        public string LastFmApiKey { get; } = "";
        public string LastFmApiSecret { get; } = "";
        public string Token { get; set; } = "";
        public string ClientSecret { get; } = "";
        public string CfClearance { get; } = "";
        public string UserAgent { get; } = "";
        public string CsrfToken { get; } = "";
        public string SpotifyClientId { get; } = "";
        public string SpotifyClientSecret { get; } = "";
        public string StatcordKey { get; } = "";
        public string GoogleApiKey { get; } = "";
        public string MashapeKey { get; } = "";
        public string OsuApiKey { get; } = "";
        public string TrovoClientId { get; } = "";
        public string TwitchClientId { get; } = "";
        public int TotalShards { get; } = 1;
        public string TwitchClientSecret { get; } = "";
        public string VotesToken { get; } = "";
        public string LocationIqApiKey { get; } = "";
        public string TimezoneDbApiKey { get; } = "";
        public ulong ConfessionReportChannelId { get; } = 942825117820530709;
        public string ChatSavePath { get; } = "/usr/share/nginx/cdn/chatlogs/";

        [JsonIgnore]
        ImmutableArray<ulong> IBotCredentials.OwnerIds
        {
            get
            {
                return OwnerIds.ToImmutableArray();
            }
        }

        public bool IsOwner(IUser u)
        {
            return OwnerIds.Contains(u.Id);
        }
    }
}