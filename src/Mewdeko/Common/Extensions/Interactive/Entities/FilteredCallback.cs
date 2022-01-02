using System.Threading.Tasks;
using Discord.WebSocket;

namespace Mewdeko.Common.Extensions.Interactive.Entities;

/// <summary>
///     Represents an event handler with a filter.
/// </summary>
/// <typeparam name="TInput">The type of the incoming inputs.</typeparam>
internal class FilteredCallback<TInput> : IInteractiveCallback<TInput>
{
    private bool _disposed;

    public FilteredCallback(Func<TInput, bool> filter, Func<TInput, bool, Task> action,
        TimeoutTaskCompletionSource<(TInput, InteractiveStatus)> timeoutTaskSource, DateTimeOffset startTime)
    {
        Filter = filter;
        Action = action;
        TimeoutTaskSource = timeoutTaskSource;
        StartTime = startTime;
    }

    /// <summary>
    ///     Gets the filter.
    /// </summary>
    public Func<TInput, bool> Filter { get; }

    /// <summary>
    ///     Gets the action which gets executed to incoming inputs.
    /// </summary>
    public Func<TInput, bool, Task> Action { get; }

    /// <summary>
    ///     Gets the <see cref="TimeoutTaskCompletionSource{TResult}" /> used to set the result of the callback.
    /// </summary>
    public TimeoutTaskCompletionSource<(TInput, InteractiveStatus)> TimeoutTaskSource { get; }

    /// <inheritdoc />
    public DateTimeOffset StartTime { get; }

    /// <inheritdoc />
    public void Cancel() => TimeoutTaskSource.TryCancel();

    /// <inheritdoc />
    public async Task ExecuteAsync(TInput input)
    {
        var success = Filter(input);
        await Action(input, success).ConfigureAwait(false);

        if (success)
        {
            TimeoutTaskSource.TrySetResult((input, InteractiveStatus.Success));
            Dispose();
        }
    }

    /// <inheritdoc />
    public Task ExecuteAsync(SocketMessage message)
    {
        if (message is TInput input) return ExecuteAsync(input);

        throw new InvalidOperationException("Cannot execute this callback using a message.");
    }

    /// <inheritdoc />
    public Task ExecuteAsync(SocketReaction reaction)
    {
        if (reaction is TInput input) return ExecuteAsync(input);

        throw new InvalidOperationException("Cannot execute this callback using a reaction.");
    }

#if DNETLABS
    /// <inheritdoc />
    public Task ExecuteAsync(SocketInteraction interaction)
    {
        if (interaction is TInput input) return ExecuteAsync(input);

        throw new InvalidOperationException("Cannot execute this callback using a reaction.");
    }
#endif

    /// <inheritdoc />
    public void Dispose() => Dispose(true);

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing) TimeoutTaskSource.TryDispose();

        _disposed = true;
    }
}