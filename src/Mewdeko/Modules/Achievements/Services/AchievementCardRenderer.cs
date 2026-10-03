using System.IO;
using System.Net.Http;
using Mewdeko.Modules.Achievements.Common;
using SkiaSharp;

namespace Mewdeko.Modules.Achievements.Services;

/// <summary>
///     Draws achievement profile cards: avatar, rank, points, progress to the next rank, totals, and equipped badges.
/// </summary>
public sealed class AchievementCardRenderer : INService
{
    private const int Width = 1000;
    private const int Height = 400;
    private const float Radius = 28;
    private const string FontPath = "data/fonts/NotoSans-Bold.ttf";

    private static readonly SKColor Canvas = new(0x12, 0x18, 0x28);
    private static readonly SKColor Surface = new(0x1A, 0x20, 0x2C);
    private static readonly SKColor Text = SKColors.White;
    private static readonly SKColor Muted = new(0xA0, 0xAE, 0xC0);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<AchievementCardRenderer> logger;
    private readonly Lazy<SKTypeface> typeface;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AchievementCardRenderer" /> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory, for avatars.</param>
    /// <param name="logger">Logger instance.</param>
    public AchievementCardRenderer(IHttpClientFactory httpClientFactory, ILogger<AchievementCardRenderer> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
        typeface = new Lazy<SKTypeface>(() =>
            File.Exists(FontPath) ? SKTypeface.FromFile(FontPath) ?? SKTypeface.Default : SKTypeface.Default);
    }

    /// <summary>
    ///     Everything a card shows.
    /// </summary>
    public sealed class CardData
    {
        /// <summary>
        ///     Display name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        ///     Avatar URL.
        /// </summary>
        public required string AvatarUrl { get; init; }

        /// <summary>
        ///     Server name, or "Global" for the global card.
        /// </summary>
        public required string Scope { get; init; }

        /// <summary>
        ///     The member's totals.
        /// </summary>
        public required AchievementMemberSummary Summary { get; init; }

        /// <summary>
        ///     Equipped badges by slot, null for empty.
        /// </summary>
        public required AchievementBadge?[] Badges { get; init; }

        /// <summary>
        ///     Accent color, or null for the rank color.
        /// </summary>
        public uint? Accent { get; init; }

        /// <summary>
        ///     Label for the rank stat.
        /// </summary>
        public required string RankLabel { get; init; }

        /// <summary>
        ///     Label for the points stat.
        /// </summary>
        public required string PointsLabel { get; init; }

        /// <summary>
        ///     Label for the achievements stat.
        /// </summary>
        public required string AchievementsLabel { get; init; }

        /// <summary>
        ///     Line under the progress bar.
        /// </summary>
        public required string ProgressLabel { get; init; }

        /// <summary>
        ///     Label for an empty badge slot.
        /// </summary>
        public required string EmptySlotLabel { get; init; }
    }

    /// <summary>
    ///     Draws a card.
    /// </summary>
    /// <param name="data">What to show.</param>
    /// <returns>A PNG stream positioned at the start.</returns>
    public async Task<MemoryStream> RenderAsync(CardData data)
    {
        var summary = data.Summary;
        var tierColor = summary.Tier.Grade is { } grade ? AchievementCatalog.GetGrade(grade).Color : 0x7C8DB5u;
        var accent = ToColor(data.Accent ?? tierColor);
        var avatar = await LoadAvatarAsync(data.AvatarUrl);

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var card = new SKRect(0, 0, Width, Height);
        using (var background = new SKPaint())
        {
            background.IsAntialias = true;
            background.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(Width, Height),
                [Canvas, Surface, Canvas.WithAlpha(255)],
                [0f, 0.6f, 1f],
                SKShaderTileMode.Clamp);
            canvas.DrawRoundRect(card, Radius, Radius, background);
        }

        using (var glow = new SKPaint())
        {
            glow.IsAntialias = true;
            glow.Shader = SKShader.CreateRadialGradient(new SKPoint(140, 60), 520,
                [accent.WithAlpha(70), accent.WithAlpha(20), SKColors.Transparent],
                [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRoundRect(card, Radius, Radius, glow);
        }

        using (var border = new SKPaint())
        {
            border.IsAntialias = true;
            border.Style = SKPaintStyle.Stroke;
            border.StrokeWidth = 2;
            border.Color = accent.WithAlpha(90);
            canvas.DrawRoundRect(new SKRect(1, 1, Width - 1, Height - 1), Radius, Radius, border);
        }

        DrawAvatar(canvas, avatar, new SKRect(40, 40, 180, 180), accent);

        using var titleFont = new SKFont(typeface.Value, 40);
        using var bodyFont = new SKFont(typeface.Value, 22);
        using var smallFont = new SKFont(typeface.Value, 18);
        using var statFont = new SKFont(typeface.Value, 30);
        using var textPaint = new SKPaint();
        textPaint.IsAntialias = true;
        textPaint.Color = Text;
        using var mutedPaint = new SKPaint();
        mutedPaint.IsAntialias = true;
        mutedPaint.Color = Muted;
        using var accentPaint = new SKPaint();
        accentPaint.IsAntialias = true;
        accentPaint.Color = accent;

        canvas.DrawText(Fit(data.Name, titleFont, 520), 210, 92, SKTextAlign.Left, titleFont, textPaint);
        canvas.DrawText(Fit(data.Scope, bodyFont, 520), 210, 128, SKTextAlign.Left, bodyFont, mutedPaint);

        DrawPill(canvas, summary.Tier.Name, new SKPoint(Width - 40, 60), accent, bodyFont);

        var fraction = TierFraction(summary);
        var bar = new SKRect(210, 152, Width - 40, 170);
        using (var track = new SKPaint())
        {
            track.IsAntialias = true;
            track.Color = SKColors.White.WithAlpha(24);
            canvas.DrawRoundRect(bar, 9, 9, track);
        }

        if (fraction > 0)
        {
            using var fill = new SKPaint();
            fill.IsAntialias = true;
            fill.Shader = SKShader.CreateLinearGradient(new SKPoint(bar.Left, 0), new SKPoint(bar.Right, 0),
                [accent.WithAlpha(200), accent], SKShaderTileMode.Clamp);
            var filled = new SKRect(bar.Left, bar.Top, bar.Left + Math.Max(bar.Height, bar.Width * fraction), bar.Bottom);
            canvas.DrawRoundRect(filled, 9, 9, fill);
        }

        canvas.DrawText(Fit(data.ProgressLabel, smallFont, bar.Width), 210, 196, SKTextAlign.Left, smallFont, mutedPaint);

        var stats = new[]
        {
            (Value: summary.Points.ToString("N0"), Label: data.PointsLabel),
            (Value: $"{summary.Unlocked:N0} / {summary.Total:N0}", Label: data.AchievementsLabel),
            (Value: summary.Rank > 0 ? $"#{summary.Rank:N0}" : "-", Label: data.RankLabel)
        };
        const float statTop = 222;
        const float statHeight = 64;
        var statWidth = (Width - 80 - 2 * 16f) / 3;
        for (var i = 0; i < stats.Length; i++)
        {
            var left = 40 + i * (statWidth + 16);
            var rect = new SKRect(left, statTop, left + statWidth, statTop + statHeight);
            using var tile = new SKPaint();
            tile.IsAntialias = true;
            tile.Color = accent.WithAlpha(22);
            canvas.DrawRoundRect(rect, 14, 14, tile);
            canvas.DrawText(stats[i].Value, rect.Left + 18, rect.Top + 34, SKTextAlign.Left, statFont, accentPaint);
            canvas.DrawText(stats[i].Label, rect.Left + 18, rect.Top + 56, SKTextAlign.Left, smallFont, mutedPaint);
        }

        var slotWidth = (Width - 80 - 3 * 16f) / 4;
        for (var i = 0; i < AchievementCatalog.BadgeSlots; i++)
        {
            var left = 40 + i * (slotWidth + 16);
            var rect = new SKRect(left, 304, left + slotWidth, 364);
            DrawBadgeSlot(canvas, rect, i < data.Badges.Length ? data.Badges[i] : null, data.EmptySlotLabel, bodyFont,
                smallFont);
        }

        avatar?.Dispose();
        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var stream = new MemoryStream();
        encoded.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

    private static float TierFraction(AchievementMemberSummary summary)
    {
        if (summary.NextTier is null)
            return 1;
        var span = summary.NextTier.MinPoints - summary.Tier.MinPoints;
        return span <= 0 ? 1 : Math.Clamp((float)(summary.Points - summary.Tier.MinPoints) / span, 0, 1);
    }

    private void DrawBadgeSlot(SKCanvas canvas, SKRect rect, AchievementBadge? badge, string emptyLabel,
        SKFont labelFont, SKFont smallFont)
    {
        using var paint = new SKPaint();
        paint.IsAntialias = true;

        if (badge is null)
        {
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2;
            paint.Color = SKColors.White.WithAlpha(40);
            paint.PathEffect = SKPathEffect.CreateDash([8, 6], 0);
            canvas.DrawRoundRect(rect, 14, 14, paint);
            using var muted = new SKPaint();
            muted.IsAntialias = true;
            muted.Color = Muted.WithAlpha(150);
            canvas.DrawText(emptyLabel, rect.MidX, rect.MidY + 7, SKTextAlign.Center, smallFont, muted);
            return;
        }

        var color = ToColor(AchievementCatalog.GetGrade(badge.Grade).Color);
        paint.Color = color.WithAlpha(36);
        canvas.DrawRoundRect(rect, 14, 14, paint);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2;
        paint.Color = color.WithAlpha(150);
        canvas.DrawRoundRect(rect, 14, 14, paint);

        var hexCenter = new SKPoint(rect.Left + 32, rect.MidY);
        DrawHexagon(canvas, hexCenter, 21, color);
        using var hexText = new SKPaint();
        hexText.IsAntialias = true;
        hexText.Color = Canvas;
        using var hexFont = new SKFont(typeface.Value, badge.Short.Length > 2 ? 12 : 16);
        canvas.DrawText(badge.Short, hexCenter.X, hexCenter.Y + (badge.Short.Length > 2 ? 4 : 6), SKTextAlign.Center,
            hexFont, hexText);

        using var namePaint = new SKPaint();
        namePaint.IsAntialias = true;
        namePaint.Color = Text;
        canvas.DrawText(Fit(badge.Name, smallFont, rect.Width - 70), rect.Left + 62, rect.MidY + 7, SKTextAlign.Left,
            smallFont, namePaint);
    }

    private static void DrawHexagon(SKCanvas canvas, SKPoint center, float radius, SKColor color)
    {
        var builder = new SKPathBuilder();
        for (var i = 0; i < 6; i++)
        {
            var angle = Math.PI / 180 * (60 * i - 90);
            var point = new SKPoint(center.X + radius * (float)Math.Cos(angle), center.Y + radius * (float)Math.Sin(angle));
            if (i == 0)
                builder.MoveTo(point);
            else
                builder.LineTo(point);
        }

        builder.Close();
        using var path = builder.Detach();
        using var paint = new SKPaint();
        paint.IsAntialias = true;
        paint.Color = color;
        canvas.DrawPath(path, paint);
    }

    private static void DrawPill(SKCanvas canvas, string text, SKPoint topRight, SKColor color, SKFont font)
    {
        var width = font.MeasureText(text) + 36;
        var rect = new SKRect(topRight.X - width, topRight.Y - 26, topRight.X, topRight.Y + 14);
        using var fill = new SKPaint();
        fill.IsAntialias = true;
        fill.Color = color.WithAlpha(40);
        canvas.DrawRoundRect(rect, 20, 20, fill);
        using var stroke = new SKPaint();
        stroke.IsAntialias = true;
        stroke.Style = SKPaintStyle.Stroke;
        stroke.StrokeWidth = 2;
        stroke.Color = color.WithAlpha(120);
        canvas.DrawRoundRect(rect, 20, 20, stroke);
        using var ink = new SKPaint();
        ink.IsAntialias = true;
        ink.Color = color;
        canvas.DrawText(text, rect.MidX, rect.MidY + 8, SKTextAlign.Center, font, ink);
    }

    private static void DrawAvatar(SKCanvas canvas, SKBitmap? avatar, SKRect rect, SKColor accent)
    {
        using var ring = new SKPaint();
        ring.IsAntialias = true;
        ring.Style = SKPaintStyle.Stroke;
        ring.StrokeWidth = 5;
        ring.Color = accent;

        if (avatar is not null)
        {
            canvas.Save();
            using var clip = new SKRoundRect(rect, rect.Width / 2, rect.Height / 2);
            canvas.ClipRoundRect(clip, SKClipOperation.Intersect, true);
            using var image = SKImage.FromBitmap(avatar);
            canvas.DrawImage(image, rect, new SKSamplingOptions(SKFilterMode.Linear));
            canvas.Restore();
        }
        else
        {
            using var fill = new SKPaint();
            fill.IsAntialias = true;
            fill.Color = accent.WithAlpha(60);
            canvas.DrawOval(rect, fill);
        }

        canvas.DrawOval(rect, ring);
    }

    private async Task<SKBitmap?> LoadAvatarAsync(string url)
    {
        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(5);
            var bytes = await http.GetByteArrayAsync(url);
            return SKBitmap.Decode(bytes);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not load an avatar for an achievement card");
            return null;
        }
    }

    private static string Fit(string text, SKFont font, float maxWidth)
    {
        if (font.MeasureText(text) <= maxWidth)
            return text;
        var trimmed = text;
        while (trimmed.Length > 1 && font.MeasureText(trimmed + "...") > maxWidth)
            trimmed = trimmed[..^1];
        return trimmed + "...";
    }

    private static SKColor ToColor(uint rgb)
    {
        return new SKColor((byte)(rgb >> 16 & 0xFF), (byte)(rgb >> 8 & 0xFF), (byte)(rgb & 0xFF));
    }
}
