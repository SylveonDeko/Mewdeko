using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Coordinator;
using Serilog;

namespace Mewdeko.Services.Impl;

public class RemoteGrpcCoordinator : ICoordinator, IReadyExecutor
{
    private readonly DiscordSocketClient client;
    private readonly Coordinator.Coordinator.CoordinatorClient coordClient;

    public RemoteGrpcCoordinator(DiscordSocketClient client, IBotCredentials credentials)
    {
        var channel = GrpcChannel.ForAddress($"http://localhost:{credentials.ShardRunPort}");
        coordClient = new Coordinator.Coordinator.CoordinatorClient(channel);
        this.client = client;
    }

    public bool RestartBot()
    {
        coordClient.RestartAllShards(new RestartAllRequest());

        return true;
    }

    public void Die() =>
        coordClient.Die(new DieRequest
        {
            Graceful = false
        });

    public bool RestartShard(int shardId)
    {
        coordClient.RestartShard(new RestartShardRequest
        {
            ShardId = shardId
        });

        return true;
    }

    public IList<ShardStatus> GetAllShardStatuses()
    {
        var res = coordClient.GetAllStatuses(new GetAllStatusesRequest());

        return res.Statuses
            .ToArray()
            .Map(s => new ShardStatus
            {
                ConnectionState = FromCoordConnState(s.State),
                GuildCount = s.GuildCount,
                ShardId = s.ShardId,
                LastUpdate = s.LastUpdate.ToDateTime(),
                UserCount = s.UserCount
            });
    }

    public int GetGuildCount()
    {
        var res = coordClient.GetAllStatuses(new GetAllStatusesRequest());

        return res.Statuses.Sum(x => x.GuildCount);
    }

    public int GetUserCount()
    {
        var res = coordClient.GetAllStatuses(new GetAllStatusesRequest());

        return res.Statuses.Sum(x => x.UserCount);
    }

    public Task OnReadyAsync()
    {
        Task.Run(async () =>
        {
            var gracefulImminent = false;
            while (true)
            {
                try
                {
                    var reply = await coordClient.HeartbeatAsync(new HeartbeatRequest
                    {
                        State = ToCoordConnState(client.ConnectionState),
                        GuildCount = client.ConnectionState == ConnectionState.Connected ? client.Guilds.Count : 0,
                        ShardId = client.ShardId,
                        UserCount = client.Guilds.SelectMany(x => x.Users).Distinct().Count()
                    }, deadline: DateTime.UtcNow + TimeSpan.FromSeconds(10));
                    gracefulImminent = reply.GracefulImminent;
                }
                catch (RpcException ex)
                {
                    if (!gracefulImminent)
                    {
                        Log.Warning(ex, "Hearbeat failed and graceful shutdown was not expected: {Message}",
                            ex.Message);
                        break;
                    }

                    await Task.Delay(22500).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unexpected heartbeat exception: {Message}", ex.Message);
                    break;
                }

                await Task.Delay(7500).ConfigureAwait(false);
            }

            Environment.Exit(5);
        });

        return Task.CompletedTask;
    }

    private static ConnState ToCoordConnState(ConnectionState state) =>
        state switch
        {
            ConnectionState.Connecting => ConnState.Connecting,
            ConnectionState.Connected => ConnState.Connected,
            _ => ConnState.Disconnected
        };

    private static ConnectionState FromCoordConnState(ConnState state) =>
        state switch
        {
            ConnState.Connecting => ConnectionState.Connecting,
            ConnState.Connected => ConnectionState.Connected,
            _ => ConnectionState.Disconnected
        };
}