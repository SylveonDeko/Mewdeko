namespace Mewdeko.Common.PubSub;

/// <summary>
/// Interface for a publish-subscribe pattern.
/// </summary>
public interface IPubSub
{
    /// <summary>
    /// Publishes a key with associated data.
    /// </summary>
    /// <typeparam name="TData">The type of data the key represents.</typeparam>
    /// <param name="key">The key to publish.</param>
    /// <param name="data">The data associated with the key.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Pub<TData>(TypedKey<TData> key, TData data)
        where TData : notnull;

    /// <summary>
    /// Subscribes an action to a specific key.
    /// </summary>
    /// <typeparam name="TData">The type of data the key represents.</typeparam>
    /// <param name="key">The key to subscribe to.</param>
    /// <param name="action">The action to execute when the key is published.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Sub<TData>(TypedKey<TData> key, Func<TData, ValueTask> action) where TData : notnull;
}