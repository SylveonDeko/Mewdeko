using Grpc.Core;
using Grpc.Net.Client;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Coordinator;
using Serilog;

namespace Mewdeko.Services.Impl
{
    /// <summary>
    /// Represents a coordinator that communicates with the remote gRPC service.
    /// </summary>
    public class RemoteGrpcCoordinator : ICoordinator, IReadyExecutor
    {
        private readonly DiscordSocketClient client;
        private readonly Coordinator.Coordinator.CoordinatorClient coordClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteGrpcCoordinator"/> class.
        /// </summary>
        /// <param name="client">The Discord socket client.</param>
        /// <param name="credentials">The bot credentials.</param>
        public RemoteGrpcCoordinator(DiscordSocketClient client, IBotCredentials credentials)
        {
            var channel = GrpcChannel.ForAddress($"http://localhost:{credentials.ShardRunPort}");
            coordClient = new Coordinator.Coordinator.CoordinatorClient(channel);
            this.client = client;
        }

        /// <summary>
        /// Restarts all shards.
        /// </summary>
        /// <returns>True if the operation is successful, otherwise false.</returns>
        public bool RestartBot()
        {
            coordClient.RestartAllShards(new RestartAllRequest());
            return true;
        }

        /// <summary>
        /// Sends a termination signal to all shards.
        /// </summary>
        public void Die() =>
            coordClient.Die(new DieRequest
            {
                Graceful = false
            });

        /// <summary>
        /// Restarts a specific shard.
        /// </summary>
        /// <param name="shardId">The ID of the shard to restart.</param>
        /// <returns>True if the operation is successful, otherwise false.</returns>
        public bool RestartShard(int shardId)
        {
            coordClient.RestartShard(new RestartShardRequest
            {
                ShardId = shardId
            });
            return true;
        }

        /// <summary>
        /// Retrieves the status of all shards.
        /// </summary>
        /// <returns>The list of shard statuses.</returns>
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

        /// <summary>
        /// Gets the total number of guilds across all shards.
        /// </summary>
        /// <returns>The total number of guilds.</returns>
        public int GetGuildCount()
        {
            var res = coordClient.GetAllStatuses(new GetAllStatusesRequest());
            return res.Statuses.Sum(x => x.GuildCount);
        }

        /// <summary>
        /// Gets the total number of users across all shards.
        /// </summary>
        /// <returns>The total number of users.</returns>
        public int GetUserCount()
        {
            var res = coordClient.GetAllStatuses(new GetAllStatusesRequest());
            return res.Statuses.Sum(x => x.UserCount);
        }

        /// <summary>
        /// Executes tasks upon the bot becoming ready.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
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
}