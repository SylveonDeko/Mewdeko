using System.Globalization;
using HarfBuzzSharp;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using Buffer = HarfBuzzSharp.Buffer;

namespace Mewdeko.Common.Cards;

/// <summary>
///     Draws and measures text with HarfBuzz shaping, taking a fallback font for each grapheme the font has no glyph for.
/// </summary>
public static class UnicodeText
{
    /// <summary>
    ///     Draws text at a baseline and returns its width.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The baseline.</param>
    /// <param name="text">The text.</param>
    /// <param name="font">The preferred font.</param>
    /// <param name="paint">The paint.</param>
    /// <returns>The drawn width.</returns>
    public static float Draw(SKCanvas canvas, float x, float y, string text, SKFont font, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        if (font.ContainsGlyphs(text))
            return DrawShaped(canvas, x, y, text, font, paint);

        var width = 0f;
        foreach (var (run, runFont) in Runs(text, font))
        {
            width += runFont == font
                ? DrawShaped(canvas, x + width, y, run, font, paint)
                : Draw(canvas, x + width, y, run, runFont, paint);
        }

        return width;
    }

    /// <summary>
    ///     Measures text the way <see cref="Draw" /> draws it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The preferred font.</param>
    /// <returns>The width.</returns>
    public static float Measure(string text, SKFont font)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        if (font.ContainsGlyphs(text))
            return MeasureShaped(text, font);

        var width = 0f;
        foreach (var (run, runFont) in Runs(text, font))
            width += runFont == font ? MeasureShaped(run, font) : Measure(run, runFont);
        return width;
    }

    private static List<(string Text, SKFont Font)> Runs(string text, SKFont font)
    {
        var runs = new List<(string, SKFont)>();
        var start = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            if (font.ContainsGlyphs(enumerator.GetTextElement()))
                continue;
            if (start != enumerator.ElementIndex)
            {
                runs.Add((text[start..enumerator.ElementIndex], font));
                start = enumerator.ElementIndex;
            }

            var foundNextGood = false;
            while (enumerator.MoveNext())
            {
                if (!font.ContainsGlyphs(enumerator.GetTextElement()))
                    continue;
                foundNextGood = true;
                break;
            }

            var missing = foundNextGood ? text[start..enumerator.ElementIndex] : text[start..];
            var fallback = SKFontManager.Default.MatchCharacter(font.Typeface.FamilyName, font.Typeface.FontStyle,
                null, missing.EnumerateRunes().First().Value);
            runs.Add((missing, fallback is null ? font : fallback.ToFont(font.Size)));
            start = foundNextGood ? enumerator.ElementIndex : text.Length;
        }

        if (start < text.Length)
            runs.Add((text[start..], font));
        return runs;
    }

    private static float DrawShaped(SKCanvas canvas, float x, float y, string text, SKFont font, SKPaint paint)
    {
        var width = MeasureShaped(text, font);
        canvas.DrawShapedText(text, x, y, font, paint);
        return width;
    }

    private static float MeasureShaped(string text, SKFont font)
    {
        try
        {
            using var blob = font.Typeface.OpenStream().ToHarfBuzzBlob();
            using var face = new Face(blob, 0);
            face.UnitsPerEm = font.Typeface.UnitsPerEm;
            using var hbFont = new HarfBuzzSharp.Font(face);
            using var buffer = new Buffer();
            buffer.AddUtf16(text);
            buffer.GuessSegmentProperties();
            hbFont.Shape(buffer);
            hbFont.GetScale(out var xScale, out _);
            return buffer.GlyphPositions.Sum(p => p.XAdvance) * (font.Size / xScale);
        }
        catch
        {
            return font.MeasureText(text);
        }
    }
}
