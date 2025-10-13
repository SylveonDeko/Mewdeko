using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Manages pooled redis connections
/// </summary>
public static class RedisConnectionManager
{
    /// <summary>
    ///     Ze redis
    /// </summary>
    public static ConnectionMultiplexer? Connection;

    private static readonly object ConnectionLock = new();

    /// <summary>
    ///     Initializes the connections used for redis
    /// </summary>
    /// <param name="redisConnections">; Seperated connection IPs</param>
    /// <param name="shardCount">Shard Count</param>
    public static void Initialize(string redisConnections, int shardCount)
    {
        if (Connection != null) return;

        var configurationOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            SocketManager = new SocketManager("", true),
            AsyncTimeout = 30000, // Increase timeout
            SyncTimeout = 30000, // Increase timeout
            ConnectTimeout = 30000, // Increase timeout
            ConnectRetry = 5, // Add retry count
            ReconnectRetryPolicy = new ExponentialRetry(5000) // Add retry policy
        };

        var connections = redisConnections.Split(';');
        foreach (var connection in connections)
        {
            configurationOptions.EndPoints.Add(connection);
        }

        try
        {
            Connection = ConnectionMultiplexer.Connect(configurationOptions);

            Connection.ConnectionFailed += (sender, args) =>
            {
                Log.Warning("Redis connection failed: {0}", args.Exception.Message);
            };

            Connection.ConnectionRestored += (sender, args) =>
            {
                Log.Information("Redis connection restored");
            };
        }
        catch (Exception ex)
        {
            Log.Error("Failed to initialize Redis: {0}", ex.Message);
        }
    }
}