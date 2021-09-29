using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Discord;
using Mewdeko.Common;
using Mewdeko.Core.Common;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Services.Impl
{
    public class BotCredentials : IBotCredentials
    {
        private readonly string _credsFileName = Path.Combine(Directory.GetCurrentDirectory(), "credentials.json");

        public BotCredentials()
        {
            try
            {
                File.WriteAllText("./credentials_example.json",
                    JsonConvert.SerializeObject(new CredentialsModel(), Formatting.Indented));
            }
            catch
            {
            }

            if (!File.Exists(_credsFileName))
                Log.Warning(
                    $"credentials.json is missing. Attempting to load creds from environment variables prefixed with 'Mewdeko_'. Example is in {Path.GetFullPath("./credentials_example.json")}");
            try
            {
                var configBuilder = new ConfigurationBuilder();
                configBuilder.AddJsonFile(_credsFileName, true)
                    .AddEnvironmentVariables("Mewdeko_");

                var data = configBuilder.Build();

                Token = data[nameof(Token)];
                if (string.IsNullOrWhiteSpace(Token))
                {
                    Log.Error(
                        "Token is missing from credentials.json or Environment variables. Add it and restart the program.");
                    Helpers.ReadErrorAndExit(5);
                }

                OwnerIds = data.GetSection("OwnerIds").GetChildren().Select(c => ulong.Parse(c.Value))
                    .ToImmutableArray();
                OfficialMods = data.GetSection("OfficialMods").GetChildren().Select(c => ulong.Parse(c.Value))
                    .ToImmutableArray();
                GoogleApiKey = data[nameof(GoogleApiKey)];
                MashapeKey = data[nameof(MashapeKey)];
                OsuApiKey = data[nameof(OsuApiKey)];
                PatreonAccessToken = data[nameof(PatreonAccessToken)];
                PatreonCampaignId = data[nameof(PatreonCampaignId)] ?? "334038";
                ShardRunCommand = data[nameof(ShardRunCommand)];
                ShardRunArguments = data[nameof(ShardRunArguments)];
                CleverbotApiKey = data[nameof(CleverbotApiKey)];
                LocationIqApiKey = data[nameof(LocationIqApiKey)];
                TimezoneDbApiKey = data[nameof(TimezoneDbApiKey)];
                CoinmarketcapApiKey = data[nameof(CoinmarketcapApiKey)];
                if (string.IsNullOrWhiteSpace(CoinmarketcapApiKey))
                    CoinmarketcapApiKey = "e79ec505-0913-439d-ae07-069e296a6079";

                if (!string.IsNullOrWhiteSpace(data[nameof(RedisOptions)]))
                    RedisOptions = data[nameof(RedisOptions)];
                else
                    RedisOptions = "127.0.0.1,syncTimeout=3000";

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

                var portStr = data[nameof(ShardRunPort)];
                if (string.IsNullOrWhiteSpace(portStr))
                    ShardRunPort = new MewdekoRandom().Next(5000, 6000);
                else
                    ShardRunPort = int.Parse(portStr);

                if (!int.TryParse(data[nameof(TotalShards)], out var ts))
                    ts = 0;
                TotalShards = ts < 1 ? 1 : ts;

                CarbonKey = data[nameof(CarbonKey)];
                var dbSection = data.GetSection("db");
                Db = new DBConfig(string.IsNullOrWhiteSpace(dbSection["Type"])
                        ? "sqlite"
                        : dbSection["Type"],
                    string.IsNullOrWhiteSpace(dbSection["ConnectionString"])
                        ? "Data Source=data/Mewdeko.db"
                        : dbSection["ConnectionString"]);

                TwitchClientId = data[nameof(TwitchClientId)];
                if (string.IsNullOrWhiteSpace(TwitchClientId)) TwitchClientId = "67w6z9i09xv2uoojdm9l0wsyph4hxo6";
            }
            catch (Exception ex)
            {
                Log.Error("JSON serialization has failed. Fix your credentials file and restart the bot.");
                Log.Fatal(ex.ToString());
                Helpers.ReadErrorAndExit(6);
            }
        }

        public int ShardRunPort { get; }
        public string GoogleApiKey { get; }
        public string MashapeKey { get; }
        public string Token { get; }

        public ImmutableArray<ulong> OwnerIds { get; }
        public ImmutableArray<ulong> OfficialMods { get; }

        public string OsuApiKey { get; }
        public string CleverbotApiKey { get; }
        public RestartConfig RestartCommand { get; }
        public DBConfig Db { get; }
        public int TotalShards { get; }
        public string CarbonKey { get; }
        public string PatreonAccessToken { get; }
        public string ShardRunCommand { get; }
        public string ShardRunArguments { get; }

        public string PatreonCampaignId { get; }

        public string TwitchClientId { get; }

        public string VotesUrl { get; }
        public string VotesToken { get; }
        public string BotListToken { get; }
        public string RedisOptions { get; }
        public string LocationIqApiKey { get; }
        public string TimezoneDbApiKey { get; }
        public string CoinmarketcapApiKey { get; }

        public bool IsOwner(IUser u)
        {
            return OwnerIds.Contains(u.Id);
        }

        public bool IsOfficialMod(IUser u)
        {
            return OfficialMods.Contains(u.Id);
        }

        /// <summary>
        ///     No idea why this thing exists
        /// </summary>
        private class CredentialsModel : IBotCredentials
        {
            public ulong[] OwnerIds { get; set; } =
            {
                280835732728184843
            };

            public ulong[] OfficialMods { get; set; } =
            {
                280835732728184843
            };

            public string SoundCloudClientId { get; set; } = "";
            public string RestartCommand { get; set; } = null;
            public int? ShardRunPort { get; set; } = null;
            public string Token { get; } = "";

            public string GoogleApiKey { get; } = "";
            public string MashapeKey { get; } = "";
            public string OsuApiKey { get; } = "";
            public string CleverbotApiKey { get; } = "";
            public string CarbonKey { get; } = "";
            public DBConfig Db { get; } = new("sqlite", "Data Source=data/Mewdeko.db");
            public int TotalShards { get; } = 1;
            public string PatreonAccessToken { get; } = "";
            public string PatreonCampaignId { get; } = "334038";

            public string ShardRunCommand { get; } = "";
            public string ShardRunArguments { get; } = "";

            public string BotListToken { get; set; }
            public string TwitchClientId { get; set; }
            public string VotesToken { get; set; }
            public string VotesUrl { get; set; }
            public string RedisOptions { get; set; }
            public string LocationIqApiKey { get; set; }
            public string TimezoneDbApiKey { get; set; }
            public string CoinmarketcapApiKey { get; set; }

            [JsonIgnore] ImmutableArray<ulong> IBotCredentials.OwnerIds => throw new NotImplementedException();
            [JsonIgnore] ImmutableArray<ulong> IBotCredentials.OfficialMods => throw new NotImplementedException();

            [JsonIgnore] RestartConfig IBotCredentials.RestartCommand => throw new NotImplementedException();

            public bool IsOwner(IUser u)
            {
                throw new NotImplementedException();
            }

            public bool IsOfficialMod(IUser u)
            {
                throw new NotImplementedException();
            }
        }
    }
}