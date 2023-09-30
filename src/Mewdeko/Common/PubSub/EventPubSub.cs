using Serilog;

namespace Mewdeko.Common.PubSub;

public class EventPubSub : IPubSub
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Delegate, List<Func<object, ValueTask>>>> actions
        = new();

    public Task Sub<TData>(TypedKey<TData> key, Func<TData, ValueTask> action)
        where TData : notnull
    {
        ValueTask LocalAction(object obj) => action((TData)obj);

        var keyActions = actions.GetOrAdd(key.Key,
            _ => new ConcurrentDictionary<Delegate, List<Func<object, ValueTask>>>());
        var sameActions = keyActions.GetOrAdd(action, _ => new List<Func<object, ValueTask>>());

        lock (sameActions) // Lock the list since List<T> is not thread-safe
        {
            sameActions.Add(LocalAction);
        }

        return Task.CompletedTask;
    }

    public async Task Pub<TData>(TypedKey<TData> key, TData data) where TData : notnull
    {
        if (actions.TryGetValue(key.Key, out var dictionary))
        {
            var tasks = new List<ValueTask>();
            foreach (var kvp in dictionary)
            {
                foreach (var action in kvp.Value)
                {
                    try
                    {
                        tasks.Add(action(data));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error while executing event handler");
                    }
                }
            }

            await Task.WhenAll(tasks.Select(vt => vt.AsTask()));
        }
    }

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