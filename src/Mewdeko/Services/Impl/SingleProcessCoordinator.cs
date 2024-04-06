using System.Diagnostics;
using Serilog;

namespace Mewdeko.Services.Impl;

/// <summary>
/// SingleProcessCoordinator is a class that implements the ICoordinator interface.
/// It provides methods to manage the bot process and shard statuses.
/// </summary>
public class SingleProcessCoordinator : ICoordinator
{
    private readonly DiscordSocketClient client;
    private readonly IBotCredentials creds;

    /// <summary>
    /// Initializes a new instance of the SingleProcessCoordinator class.
    /// </summary>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="client">The Discord client.</param>
    public SingleProcessCoordinator(IBotCredentials creds, DiscordSocketClient client)
    {
        this.creds = creds;
        this.client = client;
    }

    /// <summary>
    /// Restarts the bot process.
    /// </summary>
    /// <returns>True if the bot was restarted successfully; otherwise, false.</returns>
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

    /// <summary>
    /// Terminates the current process.
    /// </summary>
    public void Die() => Environment.Exit(0);

    /// <summary>
    /// Restarts a specific shard.
    /// </summary>
    /// <param name="shardId">The ID of the shard to restart.</param>
    /// <returns>True if the shard was restarted successfully; otherwise, false.</returns>
    public bool RestartShard(int shardId) => RestartBot();

    /// <summary>
    /// Gets the status of all shards.
    /// </summary>
    /// <returns>A list of ShardStatus objects representing the status of each shard.</returns>
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

    /// <summary>
    /// Gets the total number of guilds.
    /// </summary>
    /// <returns>The total number of guilds.</returns>
    public int GetGuildCount() => client.Guilds.Count;

    /// <summary>
    /// Gets the total number of users.
    /// </summary>
    /// <returns>The total number of users.</returns>
    public int GetUserCount() => client.Guilds.SelectMany(x => x.Users).Distinct().Count();
}