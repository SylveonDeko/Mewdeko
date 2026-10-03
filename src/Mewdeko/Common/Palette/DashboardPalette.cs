using System.Globalization;
using SkiaSharp;

namespace Mewdeko.Common.Palette;

/// <summary>
///     A guild palette exactly as the web dashboard's <c>colorStore</c> emits it. Solid slots are <c>#rrggbb</c>;
///     <see cref="Muted" /> and the gradient slots are the CSS <c>hsl(h, s%, l%)</c> strings the dashboard writes.
/// </summary>
/// <param name="Primary">The primary color.</param>
/// <param name="Secondary">The secondary color.</param>
/// <param name="Accent">The accent color.</param>
/// <param name="Text">The text color.</param>
/// <param name="Muted">The muted text color.</param>
/// <param name="Background">The dark base.</param>
/// <param name="GradientStart">The first gradient stop.</param>
/// <param name="GradientMid">The middle gradient stop.</param>
/// <param name="GradientEnd">The last gradient stop.</param>
public sealed record DashboardPalette(
    string Primary,
    string Secondary,
    string Accent,
    string Text,
    string Muted,
    string Background,
    string GradientStart,
    string GradientMid,
    string GradientEnd)
{
    /// <summary>
    ///     The dashboard's <c>DEFAULT_PALETTE</c>, used whenever extraction fails.
    /// </summary>
    public static readonly DashboardPalette Default = new("#3b82f6", "#8b5cf6", "#ec4899", "#ffffff", "#9ca3af",
        "#121828", "#3a86ff", "#8338ec", "#ff006e");

    /// <summary>
    ///     The dashboard body background, the page and card base under every palette wash.
    /// </summary>
    public static readonly SKColor Page = new(0x1A, 0x20, 0x2C);

    /// <summary>
    ///     <see cref="Primary" /> as a color.
    /// </summary>
    public SKColor PrimaryColor => DashboardColorStore.ToColor(Primary);

    /// <summary>
    ///     <see cref="Secondary" /> as a color.
    /// </summary>
    public SKColor SecondaryColor => DashboardColorStore.ToColor(Secondary);

    /// <summary>
    ///     <see cref="Accent" /> as a color.
    /// </summary>
    public SKColor AccentColor => DashboardColorStore.ToColor(Accent);

    /// <summary>
    ///     <see cref="Text" /> as a color.
    /// </summary>
    public SKColor TextColor => DashboardColorStore.ToColor(Text);

    /// <summary>
    ///     <see cref="Muted" /> as a color.
    /// </summary>
    public SKColor MutedColor => DashboardColorStore.ToColor(Muted);

    /// <summary>
    ///     <see cref="Background" /> as a color.
    /// </summary>
    public SKColor BackgroundColor => DashboardColorStore.ToColor(Background);

    /// <summary>
    ///     <see cref="GradientStart" /> as a color.
    /// </summary>
    public SKColor GradientStartColor => DashboardColorStore.ToColor(GradientStart);

    /// <summary>
    ///     <see cref="GradientMid" /> as a color.
    /// </summary>
    public SKColor GradientMidColor => DashboardColorStore.ToColor(GradientMid);

    /// <summary>
    ///     <see cref="GradientEnd" /> as a color.
    /// </summary>
    public SKColor GradientEndColor => DashboardColorStore.ToColor(GradientEnd);
}

/// <summary>
///     A line by line port of the palette builders in the web dashboard's <c>src/lib/stores/colorStore.ts</c>. Takes the
///     up to 12 colors ColorThief returns and picks, adjusts and formats them with the same arithmetic, rounding and loop
///     limits, so a guild icon yields the same colors in the bot as on the website. HSL values are degrees and percents.
/// </summary>
public static class DashboardColorStore
{
    private const double DarkBgLuminance = 0.03;
    private const int ColorCount = 12;

    /// <summary>
    ///     Runs the dashboard's whole pipeline on a decoded image: ColorThief sampling and quantization, then the
    ///     cartoon or generic builder.
    /// </summary>
    /// <param name="bitmap">The image at its natural size.</param>
    /// <returns>The palette, or the default when extraction fails.</returns>
    public static DashboardPalette Extract(SKBitmap bitmap)
    {
        try
        {
            using var unpremultiplied = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height,
                SKColorType.Bgra8888, SKAlphaType.Unpremul));
            if (!bitmap.CopyTo(unpremultiplied, SKColorType.Bgra8888))
                return DashboardPalette.Default;
            return Build(Mmcq.Quantize(Mmcq.SamplePixels(unpremultiplied.Pixels), ColorCount));
        }
        catch
        {
            return DashboardPalette.Default;
        }
    }

    /// <summary>
    ///     The tail of <c>extractColorsUncached</c>: fewer than three colors falls back to the default, otherwise the
    ///     cartoon or generic builder runs.
    /// </summary>
    /// <param name="palette">The quantized colors.</param>
    /// <returns>The palette.</returns>
    public static DashboardPalette Build(List<int[]>? palette)
    {
        if (palette is null || palette.Count < 3)
            return DashboardPalette.Default;
        try
        {
            return IsLikelyCartoon(palette) ? BuildCartoonPalette(palette) : BuildGenericPalette(palette);
        }
        catch
        {
            return DashboardPalette.Default;
        }
    }

    /// <summary>
    ///     Resolves a <c>#rrggbb</c> or <c>hsl(h, s%, l%)</c> slot to a color through the dashboard's conversion.
    /// </summary>
    /// <param name="value">The CSS color.</param>
    /// <returns>The color.</returns>
    public static SKColor ToColor(string value)
    {
        if (value.StartsWith('#') && value.Length == 7)
            return new SKColor(Convert.ToByte(value[1..3], 16), Convert.ToByte(value[3..5], 16),
                Convert.ToByte(value[5..7], 16));

        var parts = value.Replace("hsl(", "").Replace(")", "").Split(',')
            .Select(p => double.Parse(p.Trim().TrimEnd('%'), CultureInfo.InvariantCulture)).ToArray();
        var rgb = HslToRgb(parts[0], parts[1], parts[2]);
        return new SKColor((byte)rgb[0], (byte)rgb[1], (byte)rgb[2]);
    }

    private static double JsRound(double x)
    {
        return Math.Floor(x + 0.5);
    }

    private static double GetLuminance(int r, int g, int b)
    {
        static double Channel(int c)
        {
            var v = c / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }

    private static double GetLuminance(int[] c)
    {
        return GetLuminance(c[0], c[1], c[2]);
    }

    private static double GetContrastRatio(double l1, double l2)
    {
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    private static double[] RgbToHsl(int rIn, int gIn, int bIn)
    {
        var r = rIn / 255.0;
        var g = gIn / 255.0;
        var b = bIn / 255.0;
        var max = Math.Max(Math.Max(r, g), b);
        var min = Math.Min(Math.Min(r, g), b);
        var h = 0.0;
        double s;
        var l = (max + min) / 2;
        if (max != min)
        {
            var d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r)
                h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g)
                h = (b - r) / d + 2;
            else
                h = (r - g) / d + 4;
            h /= 6;
        }
        else
        {
            s = 0;
        }

        return [h * 360, s * 100, l * 100];
    }

    private static double[] RgbToHsl(int[] c)
    {
        return RgbToHsl(c[0], c[1], c[2]);
    }

    private static int[] HslToRgb(double hIn, double sIn, double lIn)
    {
        var h = hIn / 360;
        var s = sIn / 100;
        var l = lIn / 100;
        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            static double Hue2Rgb(double p, double q, double t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1.0 / 6) return p + (q - p) * 6 * t;
                if (t < 1.0 / 2) return q;
                if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
                return p;
            }

            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = Hue2Rgb(p, q, h + 1.0 / 3);
            g = Hue2Rgb(p, q, h);
            b = Hue2Rgb(p, q, h - 1.0 / 3);
        }

        return [(int)JsRound(r * 255), (int)JsRound(g * 255), (int)JsRound(b * 255)];
    }

    private static string RgbToHex(int[] c)
    {
        return $"#{c[0]:x2}{c[1]:x2}{c[2]:x2}";
    }

    private static string HslToString(double h, double s, double l)
    {
        return FormattableString.Invariant($"hsl({JsRound(h)}, {JsRound(s)}%, {JsRound(l)}%)");
    }

    private static int[] AdjustForContrast(int[] color, double minContrast = 4.5)
    {
        var hsl = RgbToHsl(color);
        var (h, s, l) = (hsl[0], hsl[1], hsl[2]);
        var contrast = GetContrastRatio(GetLuminance(color), DarkBgLuminance);
        if (contrast >= minContrast)
            return [color[0], color[1], color[2]];

        var newS = Math.Min(s + 15, 100.0);
        var attempts = 0;
        while (contrast < minContrast && attempts < 5)
        {
            attempts++;
            var newColor = HslToRgb(h, newS, l);
            contrast = GetContrastRatio(GetLuminance(newColor), DarkBgLuminance);
            if (contrast >= minContrast)
                return newColor;
        }

        attempts = 0;
        var newL = l;
        while (contrast < minContrast && attempts < 20)
        {
            newL = Math.Min(95.0, newL + 5);
            attempts++;
            var newColor = HslToRgb(h, newS, newL);
            contrast = GetContrastRatio(GetLuminance(newColor), DarkBgLuminance);
            if (newL >= 95 || contrast >= minContrast)
                break;
        }

        return HslToRgb(h, newS, newL);
    }

    private static string CreateMutedColor(int[] color)
    {
        var hsl = RgbToHsl(color);
        var backgroundLuminance = GetLuminance(color);
        var newSaturation = Math.Min(hsl[1] * 0.6, 30.0);
        var newLightness = Math.Max(hsl[2] + 20, 60.0);
        for (var attempts = 0; attempts < 10; attempts++)
        {
            var test = HslToRgb(hsl[0], newSaturation, newLightness);
            if (GetContrastRatio(GetLuminance(test), backgroundLuminance) >= 3.0)
                break;
            newLightness = Math.Min(newLightness + 5, 85.0);
        }

        return HslToString(hsl[0], newSaturation, newLightness);
    }

    private static double ScoreColor(int[] color)
    {
        var hsl = RgbToHsl(color);
        var (h, s, l) = (hsl[0], hsl[1], hsl[2]);
        var saturationScore = s / 100;
        var lightnessScore = 1 - Math.Abs(l - 55) / 55;
        var colorfulness = s > 20 ? 1.0 : s / 20;
        var accentBonus = 0.0;
        if (h is >= 0 and <= 60 or >= 340 and <= 360)
            accentBonus = Math.Min(0.5, s / 100 * 0.5);
        if (h is >= 180 and <= 300)
            accentBonus = Math.Min(0.3, s / 100 * 0.3);
        return saturationScore * 0.5 + lightnessScore * 0.2 + colorfulness * 0.1 + accentBonus * 0.2;
    }

    private static bool IsLikelyCartoon(List<int[]> palette)
    {
        var highSaturationCount = 0;
        var distinct = new HashSet<double>();
        bool skin = false, eyes = false, band = false;
        foreach (var color in palette)
        {
            var hsl = RgbToHsl(color);
            var (h, s, l) = (hsl[0], hsl[1], hsl[2]);
            if (s > 50)
                highSaturationCount++;
            distinct.Add(Math.Floor(h / 30));
            if (h is >= 10 and <= 40 && s < 40 && l > 70)
                skin = true;
            if ((h is >= 35 and <= 55 or >= 0 and <= 10 or >= 200 and <= 240 or >= 90 and <= 150 or >= 250 and <= 290)
                && s > 50 && l is > 30 and < 75)
                eyes = true;
            if ((l > 80 && s < 20) || (s > 70 && l is > 50 and < 65) || (s > 50 && l is > 40 and < 70))
                band = true;
        }

        return (highSaturationCount >= 1 && distinct.Count >= 3) || (skin && eyes) || (band && distinct.Count >= 2);
    }

    private sealed record Analysis(int[] Color, double H, double S, double L, double Score);

    private static List<T> SortedByScore<T>(IEnumerable<T> items, Func<T, double> score)
    {
        return items.OrderByDescending(score).ToList();
    }

    private static DashboardPalette BuildCartoonPalette(List<int[]> palette)
    {
        var analysis = SortedByScore(palette.Select(c =>
        {
            var hsl = RgbToHsl(c);
            return new Analysis(c, hsl[0], hsl[1], hsl[2], ScoreColor(c));
        }), a => a.Score);

        var top = analysis.Take(5).ToList();
        var warm = SortedByScore(analysis.Where(a => (a.H is >= 0 and <= 60 or >= 340 and <= 360) && a.S > 50),
            a => a.Score);
        var cool = SortedByScore(analysis.Where(a => a.H is >= 180 and <= 300 && a.S > 40), a => a.Score);

        int[] primary, secondary, accent;
        if (warm.Count > 0 && cool.Count > 0)
        {
            primary = cool[0].Color;
            secondary = warm[0].Color;
            accent = (warm.ElementAtOrDefault(1) ?? cool.ElementAtOrDefault(1) ?? analysis[2]).Color;
        }
        else
        {
            primary = top[0].Color;
            secondary = top[1].Color;
            accent = top[2].Color;
            var primaryHue = RgbToHsl(primary)[0];
            if (primaryHue is >= 0 and <= 60 or >= 300 and <= 360)
                secondary = HslToRgb((primaryHue + 180) % 360, 85, 60);
            else
                accent = HslToRgb((primaryHue + 180) % 360, 85, 60);
        }

        var eye = analysis.FirstOrDefault(a =>
            (a.H is >= 35 and <= 55 || (a.H is >= 0 and <= 30 && a.S > 70)) && a.L is > 40 and < 75 && a.S > 50);
        if (eye is not null)
            accent = eye.Color;

        return Assemble(AdjustForContrast(primary), AdjustForContrast(secondary), AdjustForContrast(accent), 90, 65);
    }

    private static DashboardPalette BuildGenericPalette(List<int[]> palette)
    {
        var sorted = SortedByScore(palette, ScoreColor);
        var primary = sorted[0];
        var secondary = sorted.ElementAtOrDefault(1) ?? sorted[0];
        var accent = sorted.ElementAtOrDefault(2) ?? sorted[0];
        return Assemble(AdjustForContrast(primary), AdjustForContrast(secondary), AdjustForContrast(accent), 80, 60);
    }

    private static DashboardPalette Assemble(int[] primary, int[] secondary, int[] accent, double gradientSaturation,
        double gradientLightness)
    {
        return new DashboardPalette(
            RgbToHex(primary),
            RgbToHex(secondary),
            RgbToHex(accent),
            "#ffffff",
            CreateMutedColor(primary),
            "#121828",
            HslToString(RgbToHsl(primary)[0], gradientSaturation, gradientLightness),
            HslToString(RgbToHsl(secondary)[0], gradientSaturation, gradientLightness),
            HslToString(RgbToHsl(accent)[0], gradientSaturation, gradientLightness));
    }
}
