using System.Security.Cryptography;
using SkiaSharp;

namespace Mewdeko.Modules.Verification.Services;

/// <summary>
///     Draws image captchas.
/// </summary>
public static class VerificationCaptcha
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 5;
    private const int Width = 300;
    private const int Height = 110;

    private static readonly Lazy<SKTypeface> Typeface = new(() =>
        SKTypeface.FromFile("data/fonts/NotoSans-Bold.ttf") ?? SKTypeface.Default);

    /// <summary>
    ///     Makes a random code.
    /// </summary>
    public static string NewCode() => RandomNumberGenerator.GetString(Alphabet, CodeLength);

    /// <summary>
    ///     Checks an answer against a code, ignoring case and spaces.
    /// </summary>
    /// <param name="code">The code that was drawn.</param>
    /// <param name="answer">What the member typed.</param>
    public static bool Matches(string code, string? answer) =>
        string.Equals(code, answer?.Replace(" ", "").Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Draws a code as a distorted PNG.
    /// </summary>
    /// <param name="code">The code to draw.</param>
    public static byte[] Render(string code)
    {
        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(RandomColor(215, 245));

        using var linePaint = new SKPaint
        {
            IsAntialias = true, StrokeWidth = 2, Style = SKPaintStyle.Stroke
        };
        for (var i = 0; i < 8; i++)
        {
            linePaint.Color = RandomColor(90, 190);
            canvas.DrawLine(Next(0, Width), Next(0, Height), Next(0, Width), Next(0, Height), linePaint);
        }

        using var font = new SKFont(Typeface.Value, 58);
        using var textPaint = new SKPaint
        {
            IsAntialias = true
        };

        var step = (Width - 40f) / code.Length;
        for (var i = 0; i < code.Length; i++)
        {
            textPaint.Color = RandomColor(10, 110);
            canvas.Save();
            canvas.Translate(24 + step * i + step / 2, Height / 2f + Next(-6, 7));
            canvas.RotateDegrees(Next(-22, 23));
            canvas.DrawText(code[i].ToString(), 0, 20, SKTextAlign.Center, font, textPaint);
            canvas.Restore();
        }

        for (var i = 0; i < 4; i++)
        {
            linePaint.Color = RandomColor(40, 140);
            using var builder = new SKPathBuilder();
            var y = Next(15, Height - 15);
            builder.MoveTo(0, y);
            builder.CubicTo(Width / 3f, y + Next(-30, 31), Width * 2 / 3f, y + Next(-30, 31), Width, y + Next(-20, 21));
            using var path = builder.Detach();
            canvas.DrawPath(path, linePaint);
        }

        textPaint.Style = SKPaintStyle.Fill;
        for (var i = 0; i < 160; i++)
        {
            textPaint.Color = RandomColor(60, 200);
            canvas.DrawCircle(Next(0, Width), Next(0, Height), 1.2f, textPaint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static int Next(int min, int max) => RandomNumberGenerator.GetInt32(min, max);

    private static SKColor RandomColor(int min, int max) =>
        new((byte)Next(min, max), (byte)Next(min, max), (byte)Next(min, max));
}
