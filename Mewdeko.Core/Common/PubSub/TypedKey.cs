namespace Mewdeko.Core.Common
{
    public readonly struct TypedKey<TData>
    {
        public readonly string Key;

        public TypedKey(in string key)
        {
            Key = key;
        }

        public static implicit operator TypedKey<TData>(in string input)
        {
            return new(input);
        }

        public static implicit operator string(in TypedKey<TData> input)
        {
            return input.Key;
        }

        public static bool operator ==(in TypedKey<TData> left, in TypedKey<TData> right)
        {
            return left.Key == right.Key;
        }

        public static bool operator !=(in TypedKey<TData> left, in TypedKey<TData> right)
        {
            return !(left == right);
        }

        public override bool Equals(object obj)
        {
            return obj is TypedKey<TData> o && o == this;
        }

        public override int GetHashCode()
        {
            return Key?.GetHashCode() ?? 0;
        }

        public override string ToString()
        {
            return Key;
        }
    }
}