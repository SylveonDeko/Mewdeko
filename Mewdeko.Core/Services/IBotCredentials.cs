using System.Collections.Immutable;
using Discord;

namespace Mewdeko.Core.Services
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

        bool IsOwner(IUser u);
    }

    public class RestartConfig
    {
        public RestartConfig(string cmd, string args)
        {
            Cmd = cmd;
            Args = args;
        }

        public string Cmd { get; }
        public string Args { get; }
    }

    public class DBConfig
    {
        public DBConfig(string type, string connectionString)
        {
            Type = type;
            ConnectionString = connectionString;
        }

        public string Type { get; }
        public string ConnectionString { get; }
    }
}