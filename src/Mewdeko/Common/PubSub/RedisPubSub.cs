using System;
using System.Threading.Tasks;
using Mewdeko._Extensions;
using Mewdeko.Services;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Common.PubSub;

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
    {
        var serialized = _serializer.Serialize(data);
        return _multi.GetSubscriber()
            .PublishAsync($"{_creds.RedisKey()}:{key.Key}", serialized, CommandFlags.FireAndForget);
    }

    public Task Sub<TData>(in TypedKey<TData> key, Func<TData, ValueTask> action)
    {
        var eventName = key.Key;

        async void Handler(RedisChannel ch, RedisValue data)
        {
            try
            {
                var dataObj = _serializer.Deserialize<TData>(data);
                await action(dataObj);
            }
            catch (Exception ex)
            {
                Log.Error($"Error handling the event {eventName}: {ex.Message}");
            }
        }

        return _multi.GetSubscriber().SubscribeAsync($"{_creds.RedisKey()}:{eventName}", Handler);
    }
}