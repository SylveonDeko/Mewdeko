namespace Mewdeko.Extensions;

public static class NumberExtensions
{
    public static int KiB(this int value) => value * 1024;

    public static int Kb(this int value) => value * 1000;

    public static int MiB(this int value) => value.KiB() * 1024;

    public static int Mb(this int value) => value.Kb() * 1000;

    public static int GiB(this int value) => value.MiB() * 1024;

    public static int Gb(this int value) => value.Mb() * 1000;

    public static ulong KiB(this ulong value) => value * 1024;

    public static ulong Kb(this ulong value) => value * 1000;

    public static ulong MiB(this ulong value) => value.KiB() * 1024;

    public static ulong Mb(this ulong value) => value.Kb() * 1000;

    public static ulong GiB(this ulong value) => value.MiB() * 1024;

    public static ulong Gb(this ulong value) => value.Mb() * 1000;

    public static bool IsInteger(this decimal number) => number == Math.Truncate(number);

    public static DateTimeOffset ToUnixTimestamp(this double number) => new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(number);
}