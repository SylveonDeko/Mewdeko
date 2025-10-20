using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Configuration;
using Serilog;

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
                    JsonSerializer.Serialize(new CredentialsModel()));
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


    /// <summary>
    ///     Gets or sets the PostgreSQL connection string.
    /// </summary>
    public string PsqlConnectionString { get; set; }

    /// <summary>
    ///     Gets or sets whether this is the master mewdeko instance
    /// </summary>
    public bool IsMasterInstance { get; set; }


    /// <summary>
    ///     Gets or sets a value indicating whether to use global currency.
    /// </summary>
    public bool UseGlobalCurrency { get; set; }

    /// <summary>
    ///     Gets or sets the API key used for the bot's API.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    ///     Gets or sets the JWT secret key used for signing JWT tokens.
    /// </summary>
    public string JwtSecret { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Discord OAuth2 Client ID (for mobile app authentication)
    /// </summary>
    public string DiscordClientId { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Discord OAuth2 Client Secret (for mobile app authentication)
    /// </summary>
    public string DiscordClientSecret { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Dashboard URL for mobile OAuth redirects
    /// </summary>
    public string DashboardUrl { get; set; } = "";

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
    ///     Gets or sets the ID of the channel where pronoun abuse reports are sent.
    /// </summary>
    public ulong PronounAbuseReportChannelId { get; set; }


    /// <summary>
    ///     Gets or sets the URL of the Lavalink server.
    /// </summary>
    public string LavalinkUrl { get; set; }


    /// <summary>
    ///     Gets or sets the URL used for giveaway entries.
    /// </summary>
    public string GiveawayEntryUrl { get; set; }


    /// <summary>
    ///     Gets or sets the port used for the API.
    /// </summary>
    public int ApiPort { get; set; } = 5001;


    /// <summary>
    ///     Gets or sets the Redis connection strings, separated by semicolons for multiple connections.
    /// </summary>
    public string RedisConnections { get; set; }

    /// <summary>
    ///     Gets or sets the bot's token.
    /// </summary>
    public string Token { get; set; }


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
    ///     Gets or sets the Patreon client ID.
    /// </summary>
    public string PatreonClientId { get; set; }

    /// <summary>
    ///     Gets or sets the Patreon client secret.
    /// </summary>
    public string PatreonClientSecret { get; set; }

    /// <summary>
    ///     Gets or sets the base URL for Patreon OAuth callbacks.
    /// </summary>
    public string PatreonBaseUrl { get; set; }

    /// <summary>
    ///     Gets or sets the list of owner IDs.
    /// </summary>
    public ImmutableArray<ulong> OwnerIds { get; set; }

    /// <summary>
    ///     Gets or sets the osu! API key.
    /// </summary>
    public string OsuApiKey { get; set; }


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
    ///     Gets or sets the Kick client ID.
    /// </summary>
    public string KickClientId { get; set; }

    /// <summary>
    ///     Gets or sets the Kick client secret.
    /// </summary>
    public string KickClientSecret { get; set; }

    /// <summary>
    ///     Gets or sets the token used for votes.
    /// </summary>
    public string VotesToken { get; set; }

    /// <summary>
    ///     Gets or sets the Open-Meteo API URL. Defaults to the public API, but can be set to a self-hosted instance.
    /// </summary>
    public string OpenMeteoApiUrl { get; set; } = "https://api.open-meteo.com";

    /// <summary>
    ///     Gets or sets the ID of the channel where confession reports are sent.
    /// </summary>
    public ulong ConfessionReportChannelId { get; set; }

    /// <summary>
    ///     Gets or sets whether the PostgreSQL setup has been completed.
    /// </summary>
    public bool PostgresSetupCompleted { get; set; }

    /// <summary>
    ///     Gets or sets the Sentry DSN for error tracking.
    /// </summary>
    public string SentryDsn { get; set; }

    /// <summary>
    ///     Checks if the specified user is an owner.
    /// </summary>
    /// <param name="u">The user to check.</param>
    /// <returns><c>true</c> if the user is an owner; otherwise, <c>false</c>.</returns>
    public bool IsOwner(IUser u)
    {
        return OwnerIds.Contains(u.Id);
    }

    /// <summary>
    ///     Checks if the specified user is an owner.
    /// </summary>
    /// <param name="userId">The user to check.</param>
    /// <returns><c>true</c> if the user is an owner; otherwise, <c>false</c>.</returns>
    public bool IsOwner(ulong userId)
    {
        return OwnerIds.Contains(userId);
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
            File.WriteAllText(credsFileName, JsonSerializer.Serialize(model));
            Log.Information("credentials.json has been created successfully.");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to write credentials.json file.");
            Log.Error(ex.Message);
            Environment.Exit(1);
        }
    }

    private void UpdateMissingCredentialsInteractively(List<string> missingCredentials)
    {
        Log.Information("Updating missing credentials...");

        // Load existing credentials to preserve non-missing values
        CredentialsModel existingModel = null;
        if (File.Exists(credsFileName))
        {
            try
            {
                var existingJson = File.ReadAllText(credsFileName);
                existingModel = JsonSerializer.Deserialize<CredentialsModel>(existingJson);
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not parse existing credentials file: {ex.Message}");
                existingModel = new CredentialsModel();
            }
        }
        else
        {
            existingModel = new CredentialsModel();
        }

        // Update only missing credentials
        if (missingCredentials.Contains("Bot Token"))
        {
            Log.Information(
                "Please enter your bot's token. You can get it from https://discord.com/developers/applications");
            var token = Console.ReadLine();

            while (string.IsNullOrWhiteSpace(token))
            {
                Log.Error("Bot token cannot be empty. Please enter a valid token:");
                token = Console.ReadLine();
            }

            existingModel.Token = token;
        }

        if (missingCredentials.Contains("Owner IDs"))
        {
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

            existingModel.OwnerIds = ownersList;
        }

        if (missingCredentials.Contains("PostgreSQL Connection String"))
        {
            Log.Information("Please input your PostgreSQL Connection String.");
            var psqlConnectionString = Console.ReadLine();

            while (string.IsNullOrWhiteSpace(psqlConnectionString))
            {
                Log.Error("PostgreSQL Connection String cannot be empty. Please enter a valid connection string:");
                psqlConnectionString = Console.ReadLine();
            }

            existingModel.PsqlConnectionString = psqlConnectionString;
        }

        // Save updated credentials
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            File.WriteAllText(credsFileName, JsonSerializer.Serialize(existingModel, options));
            Log.Information("credentials.json has been updated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to update credentials.json file.");
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
            OwnerIds =
            [
                ..data.GetSection(nameof(OwnerIds)).GetChildren()
                    .Select(c => ulong.Parse(c.Value))
            ];
            TurnstileKey = data[nameof(TurnstileKey)];
            GiveawayEntryUrl = data[nameof(GiveawayEntryUrl)];
            GoogleApiKey = data[nameof(GoogleApiKey)];
            PsqlConnectionString = data[nameof(PsqlConnectionString)];
            CsrfToken = data[nameof(CsrfToken)];
            ApiKey = data[nameof(ApiKey)];
            JwtSecret = data[nameof(JwtSecret)];
            DiscordClientId = data[nameof(DiscordClientId)];
            DiscordClientSecret = data[nameof(DiscordClientSecret)];
            DashboardUrl = data[nameof(DashboardUrl)];
            UserAgent = data[nameof(UserAgent)];
            CfClearance = data[nameof(CfClearance)];
            ApiPort = int.TryParse(data[nameof(ApiPort)], out var port) ? port : 5001;
            LastFmApiKey = data[nameof(LastFmApiKey)];
            PatreonClientId = data[nameof(PatreonClientId)];
            PatreonClientSecret = data[nameof(PatreonClientSecret)];
            PatreonBaseUrl = data[nameof(PatreonBaseUrl)];
            OsuApiKey = data[nameof(OsuApiKey)];
            TwitchClientId = data[nameof(TwitchClientId)];
            TwitchClientSecret = data[nameof(TwitchClientSecret)];
            LavalinkUrl = data[nameof(LavalinkUrl)];
            TrovoClientId = data[nameof(TrovoClientId)];
            KickClientId = data[nameof(KickClientId)];
            KickClientSecret = data[nameof(KickClientSecret)];
            IsMasterInstance = Convert.ToBoolean(data[nameof(IsMasterInstance)]);
            SpotifyClientId = data[nameof(SpotifyClientId)];
            SpotifyClientSecret = data[nameof(SpotifyClientSecret)];
            ChatSavePath = data[nameof(ChatSavePath)];
            IsApiEnabled = bool.Parse(data[nameof(IsApiEnabled)] ?? "false");


            VotesToken = data[nameof(VotesToken)];


            TotalShards = int.TryParse(data[nameof(TotalShards)], out var ts) && ts > 0 ? ts : 1;
            TwitchClientId = data[nameof(TwitchClientId)] ?? "http://localhost:5000";
            RedisConnections = data[nameof(RedisConnections)];

            DebugGuildId = ulong.TryParse(data[nameof(DebugGuildId)], out var dgid) ? dgid : 843489716674494475;
            GuildJoinsChannelId = ulong.TryParse(data[nameof(GuildJoinsChannelId)], out var gjid)
                ? gjid
                : 892789588739891250;
            ConfessionReportChannelId = ulong.TryParse(data[nameof(ConfessionReportChannelId)], out var crid)
                ? crid
                : 942825117820530709;
            PronounAbuseReportChannelId = ulong.TryParse(data[nameof(PronounAbuseReportChannelId)], out var pnrepId)
                ? pnrepId
                : 970086914826858547;
            UseGlobalCurrency = bool.TryParse(data[nameof(UseGlobalCurrency)], out var ugc) && ugc;
            OpenMeteoApiUrl = data[nameof(OpenMeteoApiUrl)] ?? "https://api.open-meteo.com";
            PostgresSetupCompleted = bool.TryParse(data[nameof(PostgresSetupCompleted)], out var pgSetup) && pgSetup;
            SentryDsn = data[nameof(SentryDsn)];

            // Check for missing or invalid critical credentials
            var missingCredentials = new List<string>();

            if (string.IsNullOrWhiteSpace(Token))
                missingCredentials.Add("Bot Token");

            if (string.IsNullOrWhiteSpace(PsqlConnectionString))
                missingCredentials.Add("PostgreSQL Connection String");

            if (OwnerIds == null || OwnerIds.Length == 0)
                missingCredentials.Add("Owner IDs");

            // If any critical credentials are missing, offer to fix them
            if (missingCredentials.Count > 0)
            {
                Log.Error($"The following critical credentials are missing: {string.Join(", ", missingCredentials)}");
                Log.Information("Would you like to fix these credentials?");
                Log.Information("1. Update credentials using interactive wizard");
                Log.Information("2. Exit and fix manually");
                Log.Information("Enter your choice (1 or 2): ");

                var choice = Console.ReadLine();
                switch (choice)
                {
                    case "1":
                        UpdateMissingCredentialsInteractively(missingCredentials);
                        // Reload credentials after update
                        UpdateCredentials(null, null);
                        return; // Skip the old validation since we've fixed the issues
                    case "2":
                    default:
                        Log.Error("Please fix the missing credentials and restart the program.");
                        Helpers.ReadErrorAndExit(5);
                        break;
                }
            }
            else
            {
                // Check if PostgreSQL connection string is valid
                try
                {
                    var dataOptions = new DataOptions()
                        .UsePostgreSQL(PsqlConnectionString);

                    using var conn = new DataConnection(dataOptions);
                    conn.OpenDbConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
                    conn.Close();
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to connect to PostgreSQL database with the provided connection string.");
                    Log.Error($"{ex}");
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
                    // Don't create a new connection on every credential update
                    if (!string.IsNullOrWhiteSpace(RedisConnections) &&
                        RedisConnectionManager.Connection == null)
                    {
                        Log.Information("Initializing Redis with connection: {0}",
                            RedisConnections.Split(";")[0]);
                        RedisConnectionManager.Initialize(RedisConnections, TotalShards);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("Redis initialization will be attempted again when needed: {0}", ex.Message);
                }
            }

            if (ApiPort is <= 0 or > 65535)
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
        public List<ulong> OwnerIds { get; set; } = [280835732728184843, 786375627892064257];

        public bool UseGlobalCurrency { get; set; } = false;
        public string TurnstileKey { get; set; } = "";
        public string GiveawayEntryUrl { get; set; } = "";

        public string PsqlConnectionString { get; set; } =
            "Server=ServerIp;Database=DatabaseName;Port=PsqlPort;UID=PsqlUser;Password=UserPassword";

        public string ApiKey { get; set; } = StringExtensions.GenerateSecureString(90);
        public string JwtSecret { get; set; } = StringExtensions.GenerateSecureString(64);
        public string DiscordClientId { get; set; } = "";
        public string DiscordClientSecret { get; set; } = "";
        public string DashboardUrl { get; set; } = "https://mewdeko.tech";
        public ulong DebugGuildId { get; set; } = 843489716674494475;
        public ulong GuildJoinsChannelId { get; set; } = 892789588739891250;
        public ulong PronounAbuseReportChannelId { get; set; } = 970086914826858547;
        public bool IsApiEnabled { get; set; } = false;
        public string LavalinkUrl { get; set; } = "http://localhost:2333";
        public int ApiPort { get; set; } = 5001;
        public bool IsMasterInstance { get; set; } = false;
        public string RedisConnections { get; } = "127.0.0.1:6379";
        public string LastFmApiKey { get; } = "";
        public string PatreonClientId { get; } = "";
        public string PatreonClientSecret { get; } = "";
        public string PatreonBaseUrl { get; } = "https://yourdomain.com";
        public string Token { get; set; } = "";
        public string CfClearance { get; } = "";
        public string UserAgent { get; } = "";
        public string CsrfToken { get; } = "";
        public string SpotifyClientId { get; } = "";
        public string SpotifyClientSecret { get; } = "";
        public string GoogleApiKey { get; } = "";
        public string OsuApiKey { get; } = "";
        public string TrovoClientId { get; } = "";
        public string KickClientId { get; } = "";
        public string KickClientSecret { get; } = "";
        public string TwitchClientId { get; } = "";
        public int TotalShards { get; } = 1;
        public string TwitchClientSecret { get; } = "";
        public string VotesToken { get; } = "";
        public string OpenMeteoApiUrl { get; } = "https://api.open-meteo.com";
        public ulong ConfessionReportChannelId { get; } = 942825117820530709;
        public string ChatSavePath { get; } = "/usr/share/nginx/cdn/chatlogs/";
        public bool PostgresSetupCompleted { get; set; }
        public string SentryDsn { get; } = "";

        [JsonIgnore]
        ImmutableArray<ulong> IBotCredentials.OwnerIds
        {
            get
            {
                return [..OwnerIds];
            }
        }

        public bool IsOwner(IUser u)
        {
            return OwnerIds.Contains(u.Id);
        }

        public bool IsOwner(ulong userId)
        {
            return OwnerIds.Contains(userId);
        }
    }
}