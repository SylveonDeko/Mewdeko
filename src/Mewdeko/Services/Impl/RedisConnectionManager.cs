using StackExchange.Redis;

namespace Mewdeko.Services.Impl;

/// <summary>
/// Manages pooled redis connections
/// </summary>
public static class RedisConnectionManager
{
    private static ConnectionMultiplexer? _connection;

    /// <summary>
    /// Initializes the connections used for redis
    /// </summary>
    /// <param name="redisConnections">; Seperated connection IPs</param>
    public static void Initialize(string redisConnections)
    {
        if (_connection != null) return;
        var configurationOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            SocketManager = SocketManager.Shared
        };

        var connections = redisConnections.Split(';');
        foreach (var connection in connections)
        {
            configurationOptions.EndPoints.Add(connection);
        }

        _connection = ConnectionMultiplexer.Connect(configurationOptions);
    }

    /// <summary>
    /// The actual connection used for redis
    /// </summary>
    /// <exception cref="InvalidOperationException">Oops!</exception>
    public static ConnectionMultiplexer Connection => _connection ?? throw new InvalidOperationException("RedisConnectionManager is not initialized.");
}