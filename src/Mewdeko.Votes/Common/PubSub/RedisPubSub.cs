#nullable enable
using System;
using System.Threading.Tasks;
using Mewdeko.Votes.Extensions;
using Mewdeko.Votes.Services;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Votes.Common.PubSub;

public sealed class RedisPubSub(ConnectionMultiplexer multi, ISeria serializer, IBotCredentials creds) : IPubSub
{
    public Task Pub<TData>(in TypedKey<TData> key, TData? data)
        where TData : notnull
    {
        if (data is null)
        {
            Log.Warning("Trying to publish a null value for event {EventName}. This is not allowed", key.Key);
            return Task.CompletedTask;
        }

        var serialized = serializer.Serialize(data);
        var redisKey = $"{creds.RedisKey()}:{key.Key}";
        return multi.GetSubscriber()
            .PublishAsync(RedisChannel.Literal(redisKey), serialized, CommandFlags.FireAndForget);
    }

    public Task Sub<TData>(in TypedKey<TData> key, Func<TData, ValueTask> action)
        where TData : notnull
    {
        var eventName = key.Key;

        var redisKey = $"{creds.RedisKey()}:{eventName}";
        return multi.GetSubscriber().SubscribeAsync(RedisChannel.Literal(redisKey), OnSubscribeHandler);

        async void OnSubscribeHandler(RedisChannel _, RedisValue data)
        {
            try
            {
                var dataObj = serializer.Deserialize<TData?>(data);
                if (dataObj is not null)
                {
                    await action(dataObj).ConfigureAwait(false);
                }
                else
                {
                    Log.Warning("Publishing event {EventName} with a null value. This is not allowed", eventName);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error handling the event {EventName}: {ErrorMessage}", eventName, ex.Message);
            }
        }
    }

    public Task Unsub<TData>(in TypedKey<TData> key)
    {
        var redisKey = $"{creds.RedisKey()}:{key.Key}";
        return multi.GetSubscriber().UnsubscribeAsync(RedisChannel.Literal(redisKey));
    }
}