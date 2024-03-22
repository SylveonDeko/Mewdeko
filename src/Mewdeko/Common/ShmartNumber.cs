namespace Mewdeko.Common
{
    /// <summary>
    /// Represents a smart number that can be implicitly converted to and from ulong and uint.
    /// </summary>
    public readonly struct ShmartNumber : IEquatable<ShmartNumber>
    {
        /// <summary>
        /// Gets the value of the ShmartNumber.
        /// </summary>
        public ulong Value { get; }

        /// <summary>
        /// Gets the input string of the ShmartNumber.
        /// </summary>
        public string Input { get; }

        /// <summary>
        /// Initializes a new instance of the ShmartNumber struct with the specified value and input string.
        /// </summary>
        /// <param name="val">The value of the ShmartNumber.</param>
        /// <param name="input">The input string of the ShmartNumber.</param>
        public ShmartNumber(ulong val, string? input = null)
        {
            Value = val;
            Input = input ?? string.Empty;
        }

        /// <summary>
        /// Implicitly converts an unsigned long integer to a ShmartNumber.
        /// </summary>
        public static implicit operator ShmartNumber(ulong num) => new(num);

        /// <summary>
        /// Implicitly converts a ShmartNumber to an unsigned long integer.
        /// </summary>
        public static implicit operator ulong(ShmartNumber num) => num.Value;

        /// <summary>
        /// Implicitly converts an unsigned integer to a ShmartNumber.
        /// </summary>
        public static implicit operator ShmartNumber(uint num) => new(num);

        /// <summary>
        /// Returns a string that represents the current ShmartNumber.
        /// </summary>
        public override string ToString() => Value.ToString();

        /// <summary>
        /// Determines whether the specified object is equal to the current ShmartNumber.
        /// </summary>
        public override bool Equals(object? obj) => obj is ShmartNumber sn && Equals(sn);

        /// <summary>
        /// Indicates whether the current ShmartNumber is equal to another ShmartNumber.
        /// </summary>
        public bool Equals(ShmartNumber other) => other.Value == Value;

        /// <summary>
        /// Returns the hash code for the current ShmartNumber.
        /// </summary>
        public override int GetHashCode() => Value.GetHashCode() ^ Input.GetHashCode(StringComparison.InvariantCulture);

        /// <summary>
        /// Determines whether two ShmartNumber objects are equal.
        /// </summary>
        public static bool operator ==(ShmartNumber left, ShmartNumber right) => left.Equals(right);

        /// <summary>
        /// Determines whether two ShmartNumber objects are not equal.
        /// </summary>
        public static bool operator !=(ShmartNumber left, ShmartNumber right) => !(left == right);
    }
}