using System.Collections.Immutable;
using System.IO;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable UnassignedGetOnlyAutoProperty

namespace Mewdeko.Services.Impl;

public class BotCredentials : IBotCredentials
{
    private readonly string credsFileName = Path.Combine(Directory.GetCurrentDirectory(), "credentials.json");

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
                        ? new List<ulong>()
                        : owners.Split(' ').Select(ulong.Parse).ToList();
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
        var watcher = new FileSystemWatcher(Directory.GetCurrentDirectory());
        watcher.NotifyFilter = NotifyFilters.LastWrite;
        watcher.Filter = "*.json";
        watcher.EnableRaisingEvents = true;
        watcher.Changed += UpdateCredentials;
    }

    public void UpdateCredentials(object ae, FileSystemEventArgs _)
    {
        try
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddJsonFile(credsFileName, true)
                .AddEnvironmentVariables("Mewdeko_");

            var data = configBuilder.Build();

            Token = data[nameof(Token)];
            if (string.IsNullOrWhiteSpace(Token))
            {
                Log.Error(
                    "Token is missing from credentials.json or Environment variables. Add it and restart the program");
                Helpers.ReadErrorAndExit(5);
            }

            OwnerIds = data.GetSection("OwnerIds").GetChildren().Select(c => ulong.Parse(c.Value))
                .ToImmutableArray();
            OfficialMods = data.GetSection("OfficialMods").GetChildren().Select(c => ulong.Parse(c.Value))
                .ToImmutableArray();
            GoogleApiKey = data[nameof(GoogleApiKey)];
            UsePsql = false.ParseBoth(data[nameof(UsePsql)] ?? "false");
            PsqlConnectionString = data[nameof(PsqlConnectionString)];
            CsrfToken = data[nameof(CsrfToken)];
            UserAgent = data[nameof(UserAgent)];
            CfClearance = data[nameof(CfClearance)];
            MashapeKey = data[nameof(MashapeKey)];
            OsuApiKey = data[nameof(OsuApiKey)];
            PatreonAccessToken = data[nameof(PatreonAccessToken)];
            TwitchClientId = data[nameof(TwitchClientId)];
            TwitchClientSecret = data[nameof(TwitchClientSecret)];
            TrovoClientId = data[nameof(TrovoClientId)];
            PatreonCampaignId = data[nameof(PatreonCampaignId)] ?? "334038";
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
        }
        catch (Exception ex)
        {
            Log.Error("JSON serialization has failed. Fix your credentials file and restart the bot");
            Log.Fatal(ex.ToString());
            Helpers.ReadErrorAndExit(6);
        }
    }

    public string Token { get; set; }
    public string ClientSecret { get; set; }

    public string ShardRunPort { get; set; }
    public string GoogleApiKey { get; set; }
    public string SpotifyClientId { get; set; }
    public string SpotifyClientSecret { get; set; }
    public string MashapeKey { get; set; }
    public string StatcordKey { get; set; }
    public string CfClearance { get; set; }
    public string UserAgent { get; set; }
    public string CsrfToken { get; set; }

    public ImmutableArray<ulong> OwnerIds { get; set; }
    public ImmutableArray<ulong> OfficialMods { get; set; }

    public string OsuApiKey { get; set; }
    public string CleverbotApiKey { get; set; }
    public RestartConfig RestartCommand { get; set; }
    public int TotalShards { get; set; }
    public string CarbonKey { get; set; }
    public string ChatSavePath { get; set; }
    public string PatreonAccessToken { get; set; }
    public string ShardRunCommand { get; set; }
    public string ShardRunArguments { get; set; }

    public bool UsePsql { get; set; }
    public string PsqlConnectionString { get; set; }

    public string PatreonCampaignId { get; set; }

    public string TwitchClientId { get; set; }
    public bool UseGlobalCurrency { get; set; }
    public string TwitchClientSecret { get; set; }
    public string TrovoClientId { get; set; }

    public string VotesUrl { get; set; }
    public string VotesToken { get; set; }
    public string BotListToken { get; set; }
    public string RedisOptions { get; set; }
    public string LocationIqApiKey { get; set; }
    public string TimezoneDbApiKey { get; set; }
    public string CoinmarketcapApiKey { get; set; }

    public ulong DebugGuildId { get; set; }
    public string GeniusKey { get; set; }
    public ulong GuildJoinsChannelId { get; set; }
    public ulong ConfessionReportChannelId { get; set; }
    public ulong GlobalBanReportChannelId { get; set; }
    public ulong PronounAbuseReportChannelId { get; set; }

    public bool IsOwner(IUser u) => OwnerIds.Contains(u.Id);

    public bool IsOfficialMod(IUser u) => OfficialMods.Contains(u.Id);

    /// <summary>
    ///     No idea why this thing exists
    /// </summary>
    private class CredentialsModel : IBotCredentials
    {
        public List<ulong> OwnerIds { get; set; } = new()
        {
            280835732728184843, 786375627892064257
        };

        public ulong[] OfficialMods { get; set; } =
        {
            280835732728184843, 786375627892064257
        };

        public string Token { get; set; } = "";

        public bool UseGlobalCurrency { get; set; } = false;
        public string ClientSecret { get; } = "";
        public string GeniusKey { get; set; }
        public string CfClearance { get; set; }
        public string UserAgent { get; set; }
        public string CsrfToken { get; set; }

        public string SoundCloudClientId { get; set; } = "";
        public string SpotifyClientId { get; set; } = "";
        public string SpotifyClientSecret { get; set; } = "";
        public string StatcordKey { get; set; } = "";
        public string RestartCommand { get; set; } = null;
        public string ShardRunPort { get; set; } = "3444";

        public string GoogleApiKey { get; } = "";
        public string MashapeKey { get; } = "";
        public string OsuApiKey { get; } = "";
        public string TrovoClientId { get; } = "";
        public string TwitchClientId { get; } = "";
        public string CleverbotApiKey { get; } = "";

        public string CarbonKey { get; } = "";
        public int TotalShards { get; } = 1;
        public string PatreonAccessToken { get; } = "";
        public string PatreonCampaignId { get; } = "334038";

        public string ShardRunCommand { get; } = "";
        public string ShardRunArguments { get; } = "";

        public string BotListToken { get; set; }
        public string TwitchClientSecret { get; set; }
        public string VotesToken { get; set; }
        public string VotesUrl { get; set; }
        public string RedisOptions { get; set; }
        public bool UsePsql { get; set; }
        public string PsqlConnectionString { get; set; }
        public string LocationIqApiKey { get; set; }
        public string TimezoneDbApiKey { get; set; }
        public string CoinmarketcapApiKey { get; set; }

        public ulong DebugGuildId { get; set; } = 843489716674494475;
        public ulong GuildJoinsChannelId { get; set; } = 892789588739891250;
        public ulong ConfessionReportChannelId { get; set; } = 942825117820530709;
        public ulong GlobalBanReportChannelId { get; set; } = 905109141620682782;
        public ulong PronounAbuseReportChannelId { get; set; } = 970086914826858547;
        public string ChatSavePath { get; set; } = "/usr/share/nginx/cdn/chatlogs/";

        [JsonIgnore]
        ImmutableArray<ulong> IBotCredentials.OwnerIds { get; }

        [JsonIgnore]
        ImmutableArray<ulong> IBotCredentials.OfficialMods { get; }

        [JsonIgnore]
        RestartConfig IBotCredentials.RestartCommand { get; }

        public bool IsOwner(IUser u) => throw new NotImplementedException();

        public bool IsOfficialMod(IUser u) => throw new NotImplementedException();
    }
}