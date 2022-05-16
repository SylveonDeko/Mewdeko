using Discord;
using Discord.WebSocket;
using Grpc.Core;
using Grpc.Net.Client;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Coordinator;
using Mewdeko.Extensions;
using Serilog;

namespace Mewdeko.Services.Impl;

public class RemoteGrpcCoordinator : ICoordinator, IReadyExecutor
{
    private readonly DiscordSocketClient _client;
    private readonly Coordinator.Coordinator.CoordinatorClient _coordClient;

    public RemoteGrpcCoordinator(DiscordSocketClient client)
    {
        var channel = GrpcChannel.ForAddress("http://localhost:3444");
        _coordClient = new Coordinator.Coordinator.CoordinatorClient(channel);
        _client = client;
    }

    public bool RestartBot()
    {
        _coordClient.RestartAllShards(new RestartAllRequest());

        return true;
    }

    public void Die() =>
        _coordClient.Die(new DieRequest
        {
            Graceful = false
        });

    public bool RestartShard(int shardId)
    {
        _coordClient.RestartShard(new RestartShardRequest
        {
            ShardId = shardId
        });

        return true;
    }

    public IList<ShardStatus> GetAllShardStatuses()
    {
        var res = _coordClient.GetAllStatuses(new GetAllStatusesRequest());

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
        var res = _coordClient.GetAllStatuses(new GetAllStatusesRequest());

        return res.Statuses.Sum(x => x.GuildCount);
    }

    public int GetUserCount()
    {
        var res = _coordClient.GetAllStatuses(new GetAllStatusesRequest());

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
                    var reply = await _coordClient.HeartbeatAsync(new HeartbeatRequest
                    {
                        State = ToCoordConnState(_client.ConnectionState),
                        GuildCount = _client.ConnectionState == ConnectionState.Connected ? _client.Guilds.Count : 0,
                        ShardId = _client.ShardId,
                        UserCount = _client.Guilds.SelectMany(x => x.Users).Distinct().Count()
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