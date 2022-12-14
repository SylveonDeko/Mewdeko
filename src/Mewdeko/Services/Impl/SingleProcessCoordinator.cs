using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;

namespace Mewdeko.Services.Impl;

public class SingleProcessCoordinator : ICoordinator
{
    private readonly DiscordSocketClient client;
    private readonly IBotCredentials creds;

    public SingleProcessCoordinator(IBotCredentials creds, DiscordSocketClient client)
    {
        this.creds = creds;
        this.client = client;
    }

    public bool RestartBot()
    {
        if (string.IsNullOrWhiteSpace(creds.RestartCommand.Cmd)
            || string.IsNullOrWhiteSpace(creds.RestartCommand.Args))
        {
            Log.Error("You must set RestartCommand.Cmd and RestartCommand.Args in creds.yml");
            return false;
        }

        Process.Start(creds.RestartCommand.Cmd, creds.RestartCommand.Args);
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000).ConfigureAwait(false);
            Die();
        });
        return true;
    }

    public void Die() => Environment.Exit(0);

    public bool RestartShard(int shardId) => RestartBot();

    public IList<ShardStatus> GetAllShardStatuses() =>
        new[]
        {
            new ShardStatus
            {
                ConnectionState = client.ConnectionState,
                GuildCount = client.Guilds.Count,
                LastUpdate = DateTime.UtcNow,
                ShardId = client.ShardId,
                UserCount = client.Guilds.SelectMany(x => x.Users).Distinct().Count()
            }
        };

    public int GetGuildCount() => client.Guilds.Count;
    public int GetUserCount() => client.Guilds.SelectMany(x => x.Users).Distinct().Count();
}