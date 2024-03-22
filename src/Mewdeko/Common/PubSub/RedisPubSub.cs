using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Common.PubSub;

public sealed class RedisPubSub : IPubSub
{
    /// <summary>
    /// The bot credentials.
    /// </summary>
    private readonly IBotCredentials creds;

    /// <summary>
    /// The Redis connection multiplexer.
    /// </summary>
    private readonly ConnectionMultiplexer multi;

    /// <summary>
    /// The serializer for data.
    /// </summary>
    private readonly ISeria serializer;

    /// <summary>
    /// Initializes a new instance of the RedisPubSub class.
    /// </summary>
    /// <param name="multi">The Redis connection multiplexer.</param>
    /// <param name="serializer">The serializer for data.</param>
    /// <param name="creds">The bot credentials.</param>
    public RedisPubSub(ConnectionMultiplexer multi, ISeria serializer, IBotCredentials creds)
    {
        this.multi = multi ?? throw new ArgumentNullException(nameof(multi));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.creds = creds ?? throw new ArgumentNullException(nameof(creds));
    }

    /// <summary>
    /// Publishes a key with associated data.
    /// </summary>
    /// <typeparam name="TData">The type of data the key represents.</typeparam>
    /// <param name="key">The key to publish.</param>
    /// <param name="data">The data associated with the key.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Pub<TData>(TypedKey<TData> key, TData? data)
        where TData : notnull
    {
        if (data is null)
        {
            Log.Warning("Trying to publish a null value for event {EventName}. This is not allowed", key.Key);
            return Task.CompletedTask;
        }

        var serialized = serializer.Serialize(data);
        return multi.GetSubscriber()
            .PublishAsync($"{creds.RedisKey()}:{key.Key}", serialized, CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Subscribes an action to a specific key.
    /// </summary>
    /// <typeparam name="TData">The type of data the key represents.</typeparam>
    /// <param name="key">The key to subscribe to.</param>
    /// <param name="action">The action to execute when the key is published.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Sub<TData>(TypedKey<TData> key, Func<TData, ValueTask> action)
        where TData : notnull
    {
        var eventName = key.Key;

        async void OnSubscribeHandler(RedisChannel _, RedisValue data)
        {
            try
            {
                var dataObj = serializer.Deserialize<TData>(data);
                if (dataObj is not null)
                {
                    await action(dataObj).ConfigureAwait(false);
                }
                else
                {
                    Log.Warning("Received a null value for event {EventName}. This is not allowed", eventName);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error handling the event {EventName}: {ErrorMessage}", eventName, ex.Message);
            }
        }

        return multi.GetSubscriber().SubscribeAsync($"{creds.RedisKey()}:{eventName}", OnSubscribeHandler);
    }

    /// <summary>
    /// Unsubscribes from a specific key.
    /// </summary>
    /// <typeparam name="TData">The type of data the key represents.</typeparam>
    /// <param name="key">The key to unsubscribe from.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Unsub<TData>(TypedKey<TData> key)
    {
        return multi.GetSubscriber().UnsubscribeAsync($"{creds.RedisKey()}:{key.Key}");
    }
}