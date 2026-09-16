using System.Globalization;
using System.IO;
using Mewdeko.Modules.ServerStats.Common;
using SkiaSharp;

namespace Mewdeko.Modules.ServerStats.Services;

/// <summary>
///     Draws the line and bar charts used by stats commands, live boards and reports.
/// </summary>
public static class StatChartRenderer
{
    /// <summary>
    ///     The default series palette, cycled when a chart has more series than colours.
    /// </summary>
    public static readonly SKColor[] Palette =
    [
        new(0x4C, 0xAF, 0xEF),
        new(0xF2, 0x6B, 0x6B),
        new(0x6B, 0xD9, 0x8B),
        new(0xF5, 0xC5, 0x42),
        new(0xB6, 0x7F, 0xF0),
        new(0x8C, 0x9E, 0xAA)
    ];

    private static readonly SKColor Background = new(38, 50, 56);
    private static readonly SKColor Grid = new(55, 71, 79);
    private static readonly SKColor Text = SKColors.White;

    /// <summary>
    ///     Renders one or more series as a smoothed line chart.
    /// </summary>
    /// <param name="title">The chart title.</param>
    /// <param name="series">The series to draw.</param>
    /// <param name="colors">Optional colour per series, falling back to the palette.</param>
    /// <param name="valueFormat">How to format axis labels: "N0", "F1" and so on.</param>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <returns>A PNG stream positioned at the start.</returns>
    public static MemoryStream RenderLineChart(string title, IReadOnlyList<ChartSeries> series,
        IReadOnlyList<SKColor>? colors = null, string valueFormat = "N0", int width = 1000, int height = 500)
    {
        const int padding = 70;
        const int legendHeight = 30;
        var plotLeft = padding;
        var plotTop = padding;
        var plotRight = width - 30;
        var plotBottom = height - padding - legendHeight;
        var plotWidth = plotRight - plotLeft;
        var plotHeight = plotBottom - plotTop;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(Background);

        using var titleFont = new SKFont(SKTypeface.Default, 22);
        using var labelFont = new SKFont(SKTypeface.Default, 14);
        using var textPaint = new SKPaint
        {
            Color = Text, IsAntialias = true
        };
        using var gridPaint = new SKPaint
        {
            Color = Grid, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true
        };

        canvas.DrawText(title, width / 2f, 38, SKTextAlign.Center, titleFont, textPaint);

        var pointCount = series.Count == 0 ? 0 : series.Max(s => s.Points.Count);
        if (pointCount == 0)
        {
            canvas.DrawText("No data yet", width / 2f, height / 2f, SKTextAlign.Center, labelFont, textPaint);
            return Encode(bitmap);
        }

        var maxValue = series.SelectMany(s => s.Points).Select(p => p.Value).DefaultIfEmpty(0).Max();
        var minValue = series.SelectMany(s => s.Points).Select(p => p.Value).DefaultIfEmpty(0).Min();
        if (minValue > 0) minValue = 0;
        var (axisMin, axisMax, step) = NiceAxis(minValue, maxValue);
        var range = axisMax - axisMin;
        if (range <= 0) range = 1;

        float ToX(int index)
        {
            return pointCount == 1
                ? plotLeft + plotWidth / 2f
                : plotLeft + index * (plotWidth / (float)(pointCount - 1));
        }

        float ToY(double value)
        {
            return (float)(plotBottom - (value - axisMin) / range * plotHeight);
        }

        for (var v = axisMin; v <= axisMax + step / 2; v += step)
        {
            var y = ToY(v);
            canvas.DrawLine(plotLeft, y, plotRight, y, gridPaint);
            canvas.DrawText(v.ToString(valueFormat, CultureInfo.InvariantCulture), plotLeft - 10, y + 5,
                SKTextAlign.Right, labelFont, textPaint);
        }

        var reference = series.OrderByDescending(s => s.Points.Count).First().Points;
        var labelEvery = Math.Max(1, (int)Math.Ceiling(pointCount / 12.0));
        var hourly = reference.Count > 1 && reference[1].Bucket - reference[0].Bucket < TimeSpan.FromDays(1);
        for (var i = 0; i < pointCount; i++)
        {
            var x = ToX(i);
            canvas.DrawLine(x, plotTop, x, plotBottom, gridPaint);
            if (i % labelEvery != 0 && i != pointCount - 1)
                continue;

            var bucket = reference[Math.Min(i, reference.Count - 1)].Bucket;
            var label = hourly ? bucket.ToString("dd MMM HH:mm") : bucket.ToString("dd MMM");
            canvas.DrawText(label, x, plotBottom + 20, SKTextAlign.Center, labelFont, textPaint);
        }

        canvas.DrawLine(plotLeft, plotBottom, plotRight, plotBottom, gridPaint);
        canvas.DrawLine(plotLeft, plotTop, plotLeft, plotBottom, gridPaint);

        for (var s = 0; s < series.Count; s++)
        {
            var color = colors != null && s < colors.Count ? colors[s] : Palette[s % Palette.Length];
            var points = series[s].Points;
            if (points.Count == 0)
                continue;

            using var linePaint = new SKPaint
            {
                Color = color, StrokeWidth = 3, IsAntialias = true, Style = SKPaintStyle.Stroke
            };
            using var fillPaint = new SKPaint
            {
                Color = color.WithAlpha(40), IsAntialias = true, Style = SKPaintStyle.Fill
            };
            using var dotPaint = new SKPaint
            {
                Color = color, IsAntialias = true, Style = SKPaintStyle.Fill
            };

            var lineBuilder = new SKPathBuilder();
            var fillBuilder = new SKPathBuilder();
            lineBuilder.MoveTo(ToX(0), ToY(points[0].Value));
            fillBuilder.MoveTo(ToX(0), ToY(points[0].Value));
            for (var i = 1; i < points.Count; i++)
            {
                var prevX = ToX(i - 1);
                var prevY = ToY(points[i - 1].Value);
                var curX = ToX(i);
                var curY = ToY(points[i].Value);
                var midX = (prevX + curX) / 2;
                lineBuilder.CubicTo(midX, prevY, midX, curY, curX, curY);
                fillBuilder.CubicTo(midX, prevY, midX, curY, curX, curY);
            }

            fillBuilder.LineTo(ToX(points.Count - 1), plotBottom);
            fillBuilder.LineTo(ToX(0), plotBottom);
            fillBuilder.Close();

            using var fill = fillBuilder.Detach();
            using var path = lineBuilder.Detach();
            canvas.DrawPath(fill, fillPaint);
            canvas.DrawPath(path, linePaint);

            if (points.Count <= 60)
            {
                for (var i = 0; i < points.Count; i++)
                    canvas.DrawCircle(ToX(i), ToY(points[i].Value), 4, dotPaint);
            }

            var legendX = plotLeft + s * 180;
            var legendY = height - 22;
            canvas.DrawRect(legendX, legendY - 10, 14, 14, dotPaint);
            canvas.DrawText(series[s].Label, legendX + 22, legendY + 2, SKTextAlign.Left, labelFont, textPaint);
        }

        return Encode(bitmap);
    }

    /// <summary>
    ///     Renders labelled values as a vertical bar chart.
    /// </summary>
    /// <param name="title">The chart title.</param>
    /// <param name="bars">The bars, in order.</param>
    /// <param name="color">The bar colour, or null for the palette default.</param>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <returns>A PNG stream positioned at the start.</returns>
    public static MemoryStream RenderBarChart(string title, IReadOnlyList<(string Label, double Value)> bars,
        SKColor? color = null, int width = 1000, int height = 500)
    {
        const int padding = 70;
        var plotLeft = padding;
        var plotTop = padding;
        var plotRight = width - 30;
        var plotBottom = height - padding;
        var plotWidth = plotRight - plotLeft;
        var plotHeight = plotBottom - plotTop;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(Background);

        using var titleFont = new SKFont(SKTypeface.Default, 22);
        using var labelFont = new SKFont(SKTypeface.Default, 14);
        using var textPaint = new SKPaint
        {
            Color = Text, IsAntialias = true
        };
        using var gridPaint = new SKPaint
        {
            Color = Grid, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true
        };
        using var barPaint = new SKPaint
        {
            Color = color ?? Palette[0], IsAntialias = true, Style = SKPaintStyle.Fill
        };

        canvas.DrawText(title, width / 2f, 38, SKTextAlign.Center, titleFont, textPaint);
        if (bars.Count == 0)
        {
            canvas.DrawText("No data yet", width / 2f, height / 2f, SKTextAlign.Center, labelFont, textPaint);
            return Encode(bitmap);
        }

        var (_, axisMax, step) = NiceAxis(0, bars.Max(b => b.Value));
        if (axisMax <= 0) axisMax = 1;

        for (var v = 0d; v <= axisMax + step / 2; v += step)
        {
            var y = (float)(plotBottom - v / axisMax * plotHeight);
            canvas.DrawLine(plotLeft, y, plotRight, y, gridPaint);
            canvas.DrawText(v.ToString("N0", CultureInfo.InvariantCulture), plotLeft - 10, y + 5, SKTextAlign.Right,
                labelFont, textPaint);
        }

        var slot = plotWidth / (float)bars.Count;
        var barWidth = slot * 0.7f;
        for (var i = 0; i < bars.Count; i++)
        {
            var x = plotLeft + i * slot + (slot - barWidth) / 2;
            var barHeight = (float)(bars[i].Value / axisMax * plotHeight);
            canvas.DrawRoundRect(x, plotBottom - barHeight, barWidth, barHeight, 4, 4, barPaint);
            canvas.DrawText(bars[i].Value.ToString("N0", CultureInfo.InvariantCulture), x + barWidth / 2,
                plotBottom - barHeight - 6, SKTextAlign.Center, labelFont, textPaint);
            if (bars.Count <= 31 || i % Math.Max(1, bars.Count / 15) == 0)
                canvas.DrawText(bars[i].Label, x + barWidth / 2, plotBottom + 20, SKTextAlign.Center, labelFont,
                    textPaint);
        }

        canvas.DrawLine(plotLeft, plotBottom, plotRight, plotBottom, gridPaint);
        canvas.DrawLine(plotLeft, plotTop, plotLeft, plotBottom, gridPaint);
        return Encode(bitmap);
    }

    /// <summary>
    ///     Converts a packed ARGB colour from guild config into a Skia colour.
    /// </summary>
    /// <param name="argb">The packed colour, or 0 for none.</param>
    /// <param name="fallback">The colour to use when none is stored.</param>
    /// <returns>The colour.</returns>
    public static SKColor FromArgb(long argb, SKColor fallback)
    {
        if (argb == 0)
            return fallback;
        var packed = unchecked((uint)argb);
        return new SKColor((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);
    }

    private static (double Min, double Max, double Step) NiceAxis(double min, double max)
    {
        if (max <= min)
            max = min + 1;

        var raw = (max - min) / 5;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var normalized = raw / magnitude;
        var step = normalized switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        } * magnitude;

        var niceMin = Math.Floor(min / step) * step;
        var niceMax = Math.Ceiling(max / step) * step;
        if (Math.Abs(niceMax - max) < double.Epsilon)
            niceMax += step;
        return (niceMin, niceMax, step);
    }

    private static MemoryStream Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }
}