using System;

namespace NadekoBot.Core.Common
{
    public struct ShmartNumber : IEquatable<ShmartNumber>
    {
        public long Value { get; }
        public string Input { get; }

        public ShmartNumber(long val, string input = null)
        {
            Value = val;
            Input = input;
        }

        public static implicit operator ShmartNumber(long num)
        {
            return new ShmartNumber(num);
        }

        public static implicit operator long(ShmartNumber num)
        {
            return num.Value;
        }

        public static implicit operator ShmartNumber(int num)
        {
            return new ShmartNumber(num);
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public override bool Equals(object obj)
        {
            return obj is ShmartNumber sn
                ? Equals(sn)
                : false;
        }

        public bool Equals(ShmartNumber other)
        {
            return other.Value == Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ Input.GetHashCode(StringComparison.InvariantCulture);
        }

        public static bool operator ==(ShmartNumber left, ShmartNumber right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ShmartNumber left, ShmartNumber right)
        {
            return !(left == right);
        }
    }
}
