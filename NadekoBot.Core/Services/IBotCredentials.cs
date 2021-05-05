using Discord;
using System.Collections.Immutable;

namespace NadekoBot.Core.Services
{
    public interface IBotCredentials
    {
        string Token { get; }
        string GoogleApiKey { get; }
        ImmutableArray<ulong> OwnerIds { get; }
        string MashapeKey { get; }
        string PatreonAccessToken { get; }
        string CarbonKey { get; }

        DBConfig Db { get; }
        string OsuApiKey { get; }

        bool IsOwner(IUser u);
        int TotalShards { get; }
        string ShardRunCommand { get; }
        string ShardRunArguments { get; }
        string PatreonCampaignId { get; }
        string CleverbotApiKey { get; }
        RestartConfig RestartCommand { get; }
        string VotesUrl { get; }
        string VotesToken { get; }
        string BotListToken { get; }
        string TwitchClientId { get; }
        string RedisOptions { get; }
        string LocationIqApiKey { get; }
        string TimezoneDbApiKey { get; }
        string CoinmarketcapApiKey { get; }
    }

    public class RestartConfig
    {
        public RestartConfig(string cmd, string args)
        {
            this.Cmd = cmd;
            this.Args = args;
        }

        public string Cmd { get; }
        public string Args { get; }
    }

    public class DBConfig
    {
        public DBConfig(string type, string connectionString)
        {
            this.Type = type;
            this.ConnectionString = connectionString;
        }
        public string Type { get; }
        public string ConnectionString { get; }
    }
}
