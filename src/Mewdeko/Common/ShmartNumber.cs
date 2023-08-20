namespace Mewdeko.Common;

public readonly struct ShmartNumber : IEquatable<ShmartNumber>
{
    public ulong Value { get; }
    public string Input { get; }

    public ShmartNumber(ulong val, string? input = null)
    {
        Value = val;
        Input = input ?? string.Empty;
    }

    public static implicit operator ShmartNumber(ulong num) => new(num);

    public static implicit operator ulong(ShmartNumber num) => num.Value;

    public static implicit operator ShmartNumber(uint num) => new(num);

    public override string ToString() => Value.ToString();

    public override bool Equals(object? obj) => obj is ShmartNumber sn && Equals(sn);

    public bool Equals(ShmartNumber other) => other.Value == Value;

    public override int GetHashCode() => Value.GetHashCode() ^ Input.GetHashCode(StringComparison.InvariantCulture);

    public static bool operator ==(ShmartNumber left, ShmartNumber right) => left.Equals(right);

    public static bool operator !=(ShmartNumber left, ShmartNumber right) => !(left == right);
}