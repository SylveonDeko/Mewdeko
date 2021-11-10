using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Common.Extensions.Interactive.Entities
{
    /// <summary>
    ///     Represents a <see cref="TaskCompletionSource{TResult}" /> with a timeout timer which can be reset.
    /// </summary>
    internal sealed class TimeoutTaskCompletionSource<TResult>
    {
        private readonly Func<TResult> _cancelAction;
        private readonly TResult _cancelResult;
        private readonly TaskCompletionSource<TResult> _taskSource;
        private readonly Func<TResult> _timeoutAction;
        private readonly TResult _timeoutResult;
        private readonly Timer _timer;
        private readonly CancellationTokenRegistration _tokenRegistration;

        private bool _disposed;

        private TimeoutTaskCompletionSource(TimeSpan delay, bool canReset = true,
            CancellationToken cancellationToken = default)
        {
            Delay = delay;
            CanReset = canReset;
            _taskSource = new TaskCompletionSource<TResult>();
            _timer = new Timer(OnTimerFired, null, delay, Timeout.InfiniteTimeSpan);
            _tokenRegistration = cancellationToken.Register(() => TryCancel());
        }

        public TimeoutTaskCompletionSource(TimeSpan delay, bool canReset = true, TResult timeoutResult = default,
            TResult cancelResult = default, CancellationToken cancellationToken = default)
            : this(delay, canReset, cancellationToken)
        {
            _timeoutResult = timeoutResult;
            _cancelResult = cancelResult;
        }

        public TimeoutTaskCompletionSource(TimeSpan delay, bool canReset = true, Func<TResult> timeoutAction = default,
            Func<TResult> cancelAction = default, CancellationToken cancellationToken = default)
            : this(delay, canReset, cancellationToken)
        {
            _timeoutAction = timeoutAction;
            _cancelAction = cancelAction;
        }

        /// <summary>
        ///     Gets the delay before the timeout.
        /// </summary>
        public TimeSpan Delay { get; }

        /// <summary>
        ///     Gets whether this delay can be reset.
        /// </summary>
        public bool CanReset { get; }

        public TResult TimeoutResult => _timeoutAction == null ? _timeoutResult : _timeoutAction();

        public TResult CancelResult => _cancelAction == null ? _cancelResult : _cancelAction();

        public Task<TResult> Task => _taskSource.Task;

        private void OnTimerFired(object state)
        {
            _disposed = true;
            _timer.Dispose();
            _taskSource.TrySetResult(TimeoutResult);
            _tokenRegistration.Dispose();
        }

        public bool TryReset()
        {
            if (_disposed || !CanReset) return false;

            _timer.Change(Delay, Timeout.InfiniteTimeSpan);
            return true;
        }

        public bool TryCancel()
        {
            return !_disposed && _taskSource.TrySetResult(CancelResult);
        }

        public bool TrySetResult(TResult result)
        {
            return _taskSource.TrySetResult(result);
        }

        public bool TryDispose()
        {
            if (_disposed) return false;

            _timer.Dispose();
            TryCancel();
            _tokenRegistration.Dispose();
            _disposed = true;

            return true;
        }
    }
}