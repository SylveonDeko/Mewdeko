namespace NadekoBot.Core.Common
{
    public readonly struct TypedKey<TData>
    {
        public readonly string Key;

        public TypedKey(in string key)
        {
            Key = key;
        }

        public static implicit operator TypedKey<TData>(in string input)
            => new TypedKey<TData>(input);
        public static implicit operator string(in TypedKey<TData> input)
            => input.Key;

        public static bool operator ==(in TypedKey<TData> left, in TypedKey<TData> right)
            => left.Key == right.Key;
        public static bool operator !=(in TypedKey<TData> left, in TypedKey<TData> right)
            => !(left == right);

        public override bool Equals(object obj)
            => obj is TypedKey<TData> o && o == this;

        public override int GetHashCode() => Key?.GetHashCode() ?? 0;

        public override string ToString() => Key;
    }
}