using System;
using System.Security.Cryptography;

namespace NadekoBot.Common
{
    public class NadekoRandom : Random
    {
        readonly RandomNumberGenerator _rng;

        public NadekoRandom() : base()
        {
            _rng = RandomNumberGenerator.Create();
        }

        public override int Next()
        {
            var bytes = new byte[sizeof(int)];
            _rng.GetBytes(bytes);
            return Math.Abs(BitConverter.ToInt32(bytes, 0));
        }

        public override int Next(int maxValue)
        {
            if (maxValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            var bytes = new byte[sizeof(int)];
            _rng.GetBytes(bytes);
            return Math.Abs(BitConverter.ToInt32(bytes, 0)) % maxValue;
        }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            if (minValue == maxValue)
                return minValue;
            var bytes = new byte[sizeof(int)];
            _rng.GetBytes(bytes);
            var sign = Math.Sign(BitConverter.ToInt32(bytes, 0));
            return (sign * BitConverter.ToInt32(bytes, 0)) % (maxValue - minValue) + minValue;
        }

        public long NextLong(long minValue, long maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            if (minValue == maxValue)
                return minValue;
            var bytes = new byte[sizeof(long)];
            _rng.GetBytes(bytes);
            var sign = Math.Sign(BitConverter.ToInt64(bytes, 0));
            return (sign * BitConverter.ToInt64(bytes, 0)) % (maxValue - minValue) + minValue;
        }

        public override void NextBytes(byte[] buffer)
        {
            _rng.GetBytes(buffer);
        }

        protected override double Sample()
        {
            var bytes = new byte[sizeof(double)];
            _rng.GetBytes(bytes);
            return Math.Abs(BitConverter.ToDouble(bytes, 0) / double.MaxValue + 1);
        }

        public override double NextDouble()
        {
            var bytes = new byte[sizeof(double)];
            _rng.GetBytes(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }
    }
}
