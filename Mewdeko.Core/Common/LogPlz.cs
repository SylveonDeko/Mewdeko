using System;
using System.Diagnostics;
using NLog;

namespace Mewdeko.Core.Common
{
    public class LogPlz
    {
        private readonly Logger _log;
        private readonly Stopwatch _sw;
        private TimeSpan _lastLap;
        private int count;

        private LogPlz()
        {
            _log = LogManager.GetCurrentClassLogger();
            _sw = Stopwatch.StartNew();
            _lastLap = TimeSpan.Zero;
        }

        public static LogPlz Go()
        {
            return new();
        }

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