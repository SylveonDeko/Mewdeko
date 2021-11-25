namespace Mewdeko.Common.Extensions.Interactive.Entities
{
    /// <summary>
    ///     Represents a generic result from an interactive action.
    /// </summary>
    /// <typeparam name="T">The type of the value of this result.</typeparam>
    public class InteractiveResult<T> : InteractiveResult
    {
        internal InteractiveResult(T value, TimeSpan elapsed, InteractiveStatus status = InteractiveStatus.Success)
            : base(elapsed, status)
        {
            Value = value;
        }

        /// <summary>
        ///     Gets the value representing the result returned by the interactive action.
        /// </summary>
        public T Value { get; }
    }

    /// <summary>
    ///     Represents a non-generic result from an interactive action.
    /// </summary>
    public class InteractiveResult : IInteractiveResult
    {
        internal InteractiveResult(TimeSpan elapsed, InteractiveStatus status = InteractiveStatus.Success)
        {
            Elapsed = elapsed;
            Status = status;
        }

        /// <summary>
        ///     Gets whether the interactive action timed out.
        /// </summary>
        public bool IsTimeout => Status == InteractiveStatus.Timeout;

        /// <summary>
        ///     Gets whether the interactive action was canceled.
        /// </summary>
        public bool IsCanceled => Status == InteractiveStatus.Canceled;

        /// <summary>
        ///     Gets whether the interactive action was successful.
        /// </summary>
        public bool IsSuccess => Status == InteractiveStatus.Success;

        /// <summary>
        ///     Gets the time passed between starting the interactive action and getting its result.
        /// </summary>
        public TimeSpan Elapsed { get; }

        /// <summary>
        ///     Gets the status of this result.
        /// </summary>
        public InteractiveStatus Status { get; }
    }
}