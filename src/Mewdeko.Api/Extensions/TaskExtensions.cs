namespace Mewdeko.Api.Extensions;

public static class TaskExtensions
{

    /// <summary>
    ///     Creates a task that will complete when all of the <see cref="Task{TResult}" /> objects in an enumerable
    ///     collection have completed
    /// </summary>
    /// <param name="tasks">The tasks to wait on for completion.</param>
    /// <typeparam name="TResult">The type of the completed task.</typeparam>
    /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
    public static Task<TResult[]> WhenAll<TResult>(this IEnumerable<Task<TResult>> tasks)
        => Task.WhenAll(tasks);

    /// <summary>
    ///     Creates a task that will complete when all of the <see cref="Task" /> objects in an enumerable
    ///     collection have completed
    /// </summary>
    /// <param name="tasks">The tasks to wait on for completion.</param>
    /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
    public static Task WhenAll(this IEnumerable<Task> tasks)
        => Task.WhenAll(tasks);
}