using Serilog;

namespace Mewdeko.Common.PubSub;

/// <summary>
///     Class that implements the IPubSub interface for Redis.
/// </summary>
public class EventPubSub : IPubSub
{
    /// <summary>
    ///     A dictionary to store actions for each key.
    /// </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Delegate, List<Func<object, ValueTask>>>> actions
        = new();

    /// <summary>
    ///     Subscribes an action to a specific key.
    /// </summary>
    /// <typeparam name="TData">The type of data the key represents.</typeparam>
    /// <param name="key">The key to subscribe to.</param>
    /// <param name="action">The action to execute when the key is published.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Sub<TData>(TypedKey<TData> key, Func<TData, ValueTask> action)
        where TData : notnull
    {
        var keyActions = actions.GetOrAdd(key.Key,
            _ => new ConcurrentDictionary<Delegate, List<Func<object, ValueTask>>>());
        var sameActions = keyActions.GetOrAdd(action, _ => []);

        lock (sameActions) // Lock the list since List<T> is not thread-safe
        {
            sameActions.Add(LocalAction);
        }

        return;

        ValueTask LocalAction(object obj)
        {
            return action((TData)obj);
        }
    }

    /// <summary>
    ///     Publishes a key with associated data.
    /// </summary>
    /// <typeparam name="TData">The type of data the key represents.</typeparam>
    /// <param name="key">The key to publish.</param>
    /// <param name="data">The data associated with the key.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Pub<TData>(TypedKey<TData> key, TData data) where TData : notnull
    {
        if (actions.TryGetValue(key.Key, out var dictionary))
        {
            var tasks = new List<Task>();
            foreach (var kvp in dictionary)
            {
                foreach (var action in kvp.Value)
                {
                    try
                    {
                        tasks.Add(action(data).AsTask());
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error while executing event handler");
                    }
                }
            }

            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    ///     Unsubscribes an action from a specific key.
    /// </summary>
    /// <typeparam name="TData">The type of data the key represents.</typeparam>
    /// <param name="key">The key to unsubscribe from.</param>
    /// <param name="action">The action to unsubscribe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Unsub<TData>(in TypedKey<TData> key, Func<TData, ValueTask> action)
    {
        if (!actions.TryGetValue(key.Key, out var dictionary) || !dictionary.TryGetValue(action, out var sameActions))
            return Task.CompletedTask;
        lock (sameActions)
        {
            sameActions.RemoveAll(a => (Func<TData, ValueTask>)a.Target == action); // Remove the specific subscription

            // Clean up if there are no more subscriptions for this action
            if (sameActions.Count != 0) return Task.CompletedTask;
            dictionary.TryRemove(action, out _);

            // Clean up if there are no more actions for this key
            if (dictionary.Count == 0)
            {
                actions.TryRemove(key.Key, out _);
            }
        }

        return Task.CompletedTask;
    }
}