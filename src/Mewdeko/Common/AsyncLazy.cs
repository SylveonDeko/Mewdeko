using System.Runtime.CompilerServices;

namespace Mewdeko.Common;

/// <summary>
///     Provides a lazy-initialized task, allowing asynchronous initialization of the value.
/// </summary>
/// <typeparam name="T">The type of the value to be initialized.</typeparam>
public class AsyncLazy<T> : Lazy<Task<T>>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncLazy{T}" /> class
    ///     with the specified synchronous value factory function.
    /// </summary>
    /// <param name="valueFactory">The synchronous function used to initialize the value.</param>
    public AsyncLazy(Func<T> valueFactory) :
        base(() => Task.Run(valueFactory))
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncLazy{T}" /> class
    ///     with the specified asynchronous task factory function.
    /// </summary>
    /// <param name="taskFactory">The asynchronous function used to initialize the value.</param>
    public AsyncLazy(Func<Task<T>> taskFactory) :
        base(() => Task.Run(taskFactory))
    {
    }

    /// <summary>
    ///     Gets an awaiter used to await the completion of the lazy-initialized task.
    /// </summary>
    /// <returns>An awaiter for the lazy-initialized task.</returns>
    public TaskAwaiter<T> GetAwaiter()
    {
        return Value.GetAwaiter();
    }
}