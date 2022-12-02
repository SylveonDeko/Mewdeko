using System;
using System.Threading.Tasks;
using Mewdeko.Votes.Extensions;
using Mewdeko.Votes.Services;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Votes.Common.PubSub;

public sealed class RedisPubSub : IPubSub
{
    private readonly IBotCredentials _creds;
    private readonly ConnectionMultiplexer _multi;
    private readonly ISeria _serializer;

    public RedisPubSub(ConnectionMultiplexer multi, ISeria serializer, IBotCredentials creds)
    {
        _multi = multi;
        _serializer = serializer;
        _creds = creds;
    }

    public Task Pub<TData>(in TypedKey<TData> key, TData data)
        where TData : notnull
    {
        var serialized = _serializer.Serialize(data);
        return _multi.GetSubscriber()
            .PublishAsync($"{_creds.RedisKey()}:{key.Key}", serialized, CommandFlags.FireAndForget);
    }

    public Task Sub<TData>(in TypedKey<TData> key, System.Func<TData, ValueTask> action)
        where TData : notnull
    {
        var eventName = key.Key;

        async void OnSubscribeHandler(RedisChannel _, RedisValue data)
        {
            try
            {
                var dataObj = _serializer.Deserialize<TData>(data);
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

        return _multi.GetSubscriber().SubscribeAsync($"{_creds.RedisKey()}:{eventName}", OnSubscribeHandler);
    }
}