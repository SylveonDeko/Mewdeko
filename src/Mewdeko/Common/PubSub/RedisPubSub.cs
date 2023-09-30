using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Common.PubSub;

public sealed class RedisPubSub : IPubSub
{
    private readonly IBotCredentials creds;
    private readonly ConnectionMultiplexer multi;
    private readonly ISeria serializer;

    public RedisPubSub(ConnectionMultiplexer multi, ISeria serializer, IBotCredentials creds)
    {
        this.multi = multi ?? throw new ArgumentNullException(nameof(multi));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.creds = creds ?? throw new ArgumentNullException(nameof(creds));
    }

    public Task Pub<TData>(TypedKey<TData> key, TData data)
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

    // Potential Unsubscribe method:
    public Task Unsub<TData>(TypedKey<TData> key)
    {
        return multi.GetSubscriber().UnsubscribeAsync($"{creds.RedisKey()}:{key.Key}");
    }
}