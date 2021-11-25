using System.Runtime.InteropServices;

namespace Mewdeko.Modules.Music.Common.SongResolver.Impl
{
    public sealed class MultimediaTimer : IDisposable
    {
        private readonly Action<object> _callback;
        private readonly uint _eventId;
        private readonly object _state;

        private LpTimeProcDelegate _lpTimeProc;

        public MultimediaTimer(Action<object> callback, object state, int period)
        {
            if (period <= 0)
                throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0");

            _callback = callback;
            _state = state;

            _lpTimeProc = CallbackInternal;
            _eventId = timeSetEvent((uint)period, 1, _lpTimeProc, 0, TimerMode.Periodic);
        }

        public void Dispose()
        {
            _lpTimeProc = default;
            timeKillEvent(_eventId);
        }

        /// <summary>
        ///     The timeSetEvent function starts a specified timer event. The multimedia timer runs in its own thread.
        ///     After the event is activated, it calls the specified callback function or sets or pulses the specified
        ///     event object.
        /// </summary>
        /// <param name="uDelay">
        ///     Event delay, in milliseconds. If this value is not in the range of the minimum and
        ///     maximum event delays supported by the timer, the function returns an error.
        /// </param>
        /// <param name="uResolution">
        ///     Resolution of the timer event, in milliseconds. The resolution increases with
        ///     smaller values; a resolution of 0 indicates periodic events should occur with the greatest possible accuracy.
        ///     To reduce system overhead, however, you should use the maximum value appropriate for your application.
        /// </param>
        /// <param name="lpTimeProc">
        ///     Pointer to a callback function that is called once upon expiration of a single event or periodically upon
        ///     expiration of periodic events. If fuEvent specifies the TIME_CALLBACK_EVENT_SET or TIME_CALLBACK_EVENT_PULSE
        ///     flag, then the lpTimeProc parameter is interpreted as a handle to an event object. The event will be set or
        ///     pulsed upon completion of a single event or periodically upon completion of periodic events.
        ///     For any other value of fuEvent, the lpTimeProc parameter is a pointer to a callback function of type
        ///     LPTIMECALLBACK.
        /// </param>
        /// <param name="dwUser">User-supplied callback data.</param>
        /// <param name="fuEvent"></param>
        /// <returns>Timer event type. This parameter may include one of the following values.</returns>
        [DllImport("Winmm.dll")]
        private static extern uint timeSetEvent(
            uint uDelay,
            uint uResolution,
            LpTimeProcDelegate lpTimeProc,
            int dwUser,
            TimerMode fuEvent
        );

        /// <summary>
        ///     The timeKillEvent function cancels a specified timer event.
        /// </summary>
        /// <param name="uTimerID">
        ///     Identifier of the timer event to cancel.
        ///     This identifier was returned by the timeSetEvent function when the timer event was set up.
        /// </param>
        /// <returns>Returns TIMERR_NOERROR if successful or MMSYSERR_INVALPARAM if the specified timer event does not exist.</returns>
        [DllImport("Winmm.dll")]
        private static extern int timeKillEvent(
            uint uTimerID
        );

        private void CallbackInternal(uint uTimerId, uint uMsg, int dwUser, int dw1, int dw2)
        {
            _callback(_state);
        }

        private delegate void LpTimeProcDelegate(uint uTimerID, uint uMsg, int dwUser, int dw1, int dw2);

        private enum TimerMode
        {
            OneShot,
            Periodic
        }
    }
}