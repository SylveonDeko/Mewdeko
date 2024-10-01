namespace Mewdeko.Common.PubSub;

/// <summary>
///     Represents a typed key for publish-subscribe pattern.
/// </summary>
/// <typeparam name="TData">The type of data the key represents.</typeparam>
public readonly struct TypedKey<TData>
{
    /// <summary>
    ///     Gets the key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    ///     Initializes a new instance of the TypedKey struct.
    /// </summary>
    /// <param name="key">The key.</param>
    public TypedKey(in string key)
    {
        Key = key;
    }

    /// <summary>
    ///     Implicit conversion from string to TypedKey.
    /// </summary>
    /// <param name="input">The input string.</param>
    public static implicit operator TypedKey<TData>(in string input)
    {
        return new TypedKey<TData>(input);
    }

    /// <summary>
    ///     Implicit conversion from TypedKey to string.
    /// </summary>
    /// <param name="input">The input TypedKey.</param>
    public static implicit operator string(in TypedKey<TData> input)
    {
        return input.Key;
    }

    /// <summary>
    ///     Equality operator for TypedKey.
    /// </summary>
    /// <param name="left">The left TypedKey.</param>
    /// <param name="right">The right TypedKey.</param>
    /// <returns>True if the keys are equal, false otherwise.</returns>
    public static bool operator ==(in TypedKey<TData> left, in TypedKey<TData> right)
    {
        return left.Key == right.Key;
    }

    /// <summary>
    ///     Inequality operator for TypedKey.
    /// </summary>
    /// <param name="left">The left TypedKey.</param>
    /// <param name="right">The right TypedKey.</param>
    /// <returns>True if the keys are not equal, false otherwise.</returns>
    public static bool operator !=(in TypedKey<TData> left, in TypedKey<TData> right)
    {
        return !(left == right);
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>True if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return obj is TypedKey<TData> o && o == this;
    }

    /// <summary>
    ///     Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return Key?.GetHashCode() ?? 0;
    }

    /// <summary>
    ///     Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return Key;
    }
}