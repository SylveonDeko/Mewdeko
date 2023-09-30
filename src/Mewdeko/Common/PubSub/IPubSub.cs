namespace Mewdeko.Common.PubSub;

public interface IPubSub
{
    public Task Pub<TData>(TypedKey<TData> key, TData data)
        where TData : notnull;

    public Task Sub<TData>(TypedKey<TData> key, Func<TData, ValueTask> action) where TData : notnull;
}