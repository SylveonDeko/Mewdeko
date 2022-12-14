using System.Security.Cryptography;

namespace Mewdeko.Common;

public class MewdekoRandom : Random
{
    private readonly RandomNumberGenerator rng;

    public MewdekoRandom() => rng = RandomNumberGenerator.Create();

    public override int Next()
    {
        var bytes = new byte[sizeof(int)];
        rng.GetBytes(bytes);
        return Math.Abs(BitConverter.ToInt32(bytes, 0));
    }

    public override int Next(int maxValue)
    {
        if (maxValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxValue));
        var bytes = new byte[sizeof(int)];
        rng.GetBytes(bytes);
        return Math.Abs(BitConverter.ToInt32(bytes, 0)) % maxValue;
    }

    public override int Next(int minValue, int maxValue)
    {
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException(nameof(maxValue));
        if (minValue == maxValue)
            return minValue;
        var bytes = new byte[sizeof(int)];
        rng.GetBytes(bytes);
        var sign = Math.Sign(BitConverter.ToInt32(bytes, 0));
        return (sign * BitConverter.ToInt32(bytes, 0) % (maxValue - minValue)) + minValue;
    }

    public long NextLong(long minValue, long maxValue)
    {
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException(nameof(maxValue));
        if (minValue == maxValue)
            return minValue;
        var bytes = new byte[sizeof(long)];
        rng.GetBytes(bytes);
        var sign = Math.Sign(BitConverter.ToInt64(bytes, 0));
        return (sign * BitConverter.ToInt64(bytes, 0) % (maxValue - minValue)) + minValue;
    }

    public override void NextBytes(byte[] buffer) => rng.GetBytes(buffer);

    protected override double Sample()
    {
        var bytes = new byte[sizeof(double)];
        rng.GetBytes(bytes);
        return Math.Abs((BitConverter.ToDouble(bytes, 0) / double.MaxValue) + 1);
    }

    public override double NextDouble()
    {
        var bytes = new byte[sizeof(double)];
        rng.GetBytes(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }
}