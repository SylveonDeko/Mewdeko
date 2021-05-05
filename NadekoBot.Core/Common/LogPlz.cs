using NLog;
using System;
using System.Diagnostics;

namespace NadekoBot.Core.Common
{
    public class LogPlz
    {
        private readonly Logger _log;
        private readonly Stopwatch _sw;
        private TimeSpan _lastLap;
        private int count = 0;

        private LogPlz()
        {
            _log = LogManager.GetCurrentClassLogger();
            _sw = Stopwatch.StartNew();
            _lastLap = TimeSpan.Zero;
        }

        public static LogPlz Go() => new LogPlz();
        public void Lap()
        {
            var cur = _sw.Elapsed;
            var sinceLast = cur - _lastLap;
            _lastLap = cur;

            Print((++count).ToString(), sinceLast);
        }
        public void End()
        {
            _sw.Stop();
            Print("END", _sw.Elapsed);
        }

        private void Print(string v, TimeSpan sinceLast)
        {
            _log.Info("#{0} => {1}", v, sinceLast.TotalSeconds.ToString("F3"));
        }
    }
}
