using Serilog;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Mewdeko.Services.Impl;

public class SingleProcessCoordinator : ICoordinator
{
    private readonly DiscordSocketClient _client;
    private readonly IBotCredentials _creds;

    public SingleProcessCoordinator(IBotCredentials creds, DiscordSocketClient client)
    {
        _creds = creds;
        _client = client;
    }

    public bool RestartBot()
    {
        if (string.IsNullOrWhiteSpace(_creds.RestartCommand.Cmd)
            || string.IsNullOrWhiteSpace(_creds.RestartCommand.Args))
        {
            Log.Error("You must set RestartCommand.Cmd and RestartCommand.Args in creds.yml");
            return false;
        }

        Process.Start(_creds.RestartCommand.Cmd, _creds.RestartCommand.Args);
        _ = Task.Factory.StartNew(async () =>
        {
            await Task.Delay(2000).ConfigureAwait(false);
            Die();
        }, TaskCreationOptions.LongRunning);
        return true;
    }

    public void Die() => Environment.Exit(0);

    public bool RestartShard(int shardId) => RestartBot();

    public IList<ShardStatus> GetAllShardStatuses() =>
        new[]
        {
            new ShardStatus
            {
                ConnectionState = _client.ConnectionState,
                GuildCount = _client.Guilds.Count,
                LastUpdate = DateTime.UtcNow,
                ShardId = _client.ShardId,
                UserCount = _client.Guilds.SelectMany(x => x.Users).Distinct().Count()
            }
        };

    public int GetGuildCount() => _client.Guilds.Count;
    public int GetUserCount() => _client.Guilds.SelectMany(x => x.Users).Distinct().Count();
}