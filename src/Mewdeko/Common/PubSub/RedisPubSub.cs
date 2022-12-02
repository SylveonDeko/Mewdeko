using System.Threading.Tasks;
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
        this.multi = multi;
        this.serializer = serializer;
        this.creds = creds;
    }

    public Task Pub<TData>(in TypedKey<TData> key, TData data)
        where TData : notnull
    {
        var serialized = serializer.Serialize(data);
        return multi.GetSubscriber()
            .PublishAsync($"{creds.RedisKey()}:{key.Key}", serialized, CommandFlags.FireAndForget);
    }

    public Task Sub<TData>(in TypedKey<TData> key, Func<TData, ValueTask> action)
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
                    Log.Warning("Publishing event {EventName} with a null value. This is not allowed",
                        eventName);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error handling the event {EventName}: {ErrorMessage}", eventName, ex.Message);
            }
        }

        return multi.GetSubscriber().SubscribeAsync($"{creds.RedisKey()}:{eventName}", OnSubscribeHandler);
    }
}