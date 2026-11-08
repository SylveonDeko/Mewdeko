using System.Globalization;
using System.IO;
using System.Net.Http;
using Mewdeko.Common.Palette;
using Mewdeko.Modules.Achievements.Common;
using SkiaSharp;

namespace Mewdeko.Common.Cards;

/// <summary>
///     Where an element landed on the card, for editors to draw handles over the image.
/// </summary>
/// <param name="Id">The element ID.</param>
/// <param name="X">Left edge in card pixels.</param>
/// <param name="Y">Top edge in card pixels.</param>
/// <param name="W">Width.</param>
/// <param name="H">Height.</param>
/// <param name="Drawn">Whether it was drawn on this card.</param>
public sealed record CardElementBox(string Id, float X, float Y, float W, float H, bool Drawn);

/// <summary>
///     A drawn card.
/// </summary>
/// <param name="Image">The PNG, positioned at the start.</param>
/// <param name="CardWidth">The card's width, without the margin.</param>
/// <param name="CardHeight">The card's height, without the margin.</param>
/// <param name="Layout">Where each element landed.</param>
/// <param name="Margin">The transparent space around the card on every side.</param>
public sealed record CardRenderResult(MemoryStream Image, int CardWidth, int CardHeight,
    IReadOnlyList<CardElementBox> Layout, int Margin = CardRenderer.Margin);

/// <summary>
///     Draws a card from a <see cref="CardTemplate" />: its background, then each element back to front, with colors
///     resolved from the server's dashboard palette. What the card shows comes from a <see cref="CardContent" />.
/// </summary>
public sealed class CardRenderer : INService
{
    /// <summary>
    ///     Transparent space around the card. Discord rounds the corners of image attachments, much more on mobile,
    ///     so the card sits inside this margin and the rounding only cuts empty pixels and the edge of the shadow.
    /// </summary>
    public const int Margin = 40;

    /// <summary>
    ///     The <see cref="CardElement.Font" /> value of the Uni Sans typeface.
    /// </summary>
    public const string UniSans = "unisans";

    private const string BoldFontPath = "data/fonts/NotoSans-Bold.ttf";
    private const string RegularFontPath = "data/fonts/NotoSans-Regular.ttf";
    private const string UniSansFontPath = "data/fonts/Uni Sans.ttf";
    private const string GlyphFontPath = "data/fonts/FontAwesomeUtilityDuo.otf";
    private const float PillPadding = 16;
    private const float PillGlyphSlot = 36;
    private const float TextGlyphSlot = 1.8f;
    private const float InlineSlot = 1.3f;
    private const float MinShrinkSize = 20;
    private const int MaxInlineImages = 40;
    private const int MaxCachedEmoji = 500;

    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear);
    private static readonly ConcurrentDictionary<string, byte[]> EmojiCache = new();
    private static readonly ConcurrentDictionary<string, DateTime> EmojiMisses = new();
    private static readonly TimeSpan EmojiRetry = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<CardRenderer> logger;
    private readonly CardPresetService presets;
    private readonly Lazy<SKTypeface> boldFace;
    private readonly Lazy<SKTypeface> regularFace;
    private readonly Lazy<SKTypeface> uniSansFace;
    private readonly Lazy<SKTypeface?> glyphFace;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CardRenderer" /> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP clients, for emoji and avatars.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="presets">The shared background presets, for <c>preset:id</c> images.</param>
    public CardRenderer(IHttpClientFactory httpClientFactory, ILogger<CardRenderer> logger, CardPresetService presets)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
        this.presets = presets;
        boldFace = new Lazy<SKTypeface>(() => LoadFace(BoldFontPath));
        regularFace = new Lazy<SKTypeface>(() => File.Exists(RegularFontPath) ? LoadFace(RegularFontPath) : boldFace.Value);
        uniSansFace = new Lazy<SKTypeface>(() => File.Exists(UniSansFontPath) ? LoadFace(UniSansFontPath) : boldFace.Value);
        glyphFace = new Lazy<SKTypeface?>(() => File.Exists(GlyphFontPath) ? SKTypeface.FromFile(GlyphFontPath) : null);
    }

    /// <summary>
    ///     Draws a card and reports where each element landed.
    /// </summary>
    /// <param name="content">What the card shows.</param>
    /// <param name="template">The design.</param>
    /// <param name="hiddenTextId">An element drawn without its text, as the designer shows one being typed over.</param>
    /// <param name="margin">Transparent space around the card.</param>
    /// <returns>The image and layout.</returns>
    public async Task<CardRenderResult> RenderAsync(CardContent content, CardTemplate template,
        string? hiddenTextId = null, int margin = Margin)
    {
        var palette = content.Palette;
        var accent = content.Accent;
        var images = await LoadImagesAsync(content, template);
        try
        {
            var context = new DrawContext(this, content, template, palette, accent, images, hiddenTextId);
            var urls = context.EmojiUrls().Take(MaxInlineImages).ToList();
            var emoji = await Task.WhenAll(urls.Select(LoadEmojiAsync));
            for (var i = 0; i < urls.Count; i++)
                images["emoji:" + urls[i]] = emoji[i];
            var laid = context.Layout();

            using var surface = SKSurface.Create(new SKImageInfo(template.Width + margin * 2,
                template.Height + margin * 2));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(margin, margin);

            if (template.Shadow)
                DrawShadow(canvas, template);
            canvas.Save();
            using (var clip = new SKRoundRect(new SKRect(0, 0, template.Width, template.Height), template.Radius))
                canvas.ClipRoundRect(clip, SKClipOperation.Intersect, true);
            DrawBackground(canvas, template, palette, accent, images);
            foreach (var item in laid.Where(l => l.Drawn))
                context.Draw(canvas, item);
            canvas.Restore();
            DrawBorder(canvas, template, palette, accent);

            using var image = surface.Snapshot();
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            var stream = new MemoryStream();
            encoded.SaveTo(stream);
            stream.Position = 0;
            return new CardRenderResult(stream, template.Width, template.Height,
                laid.Select(l => new CardElementBox(l.Element.Id, l.Rect.Left, l.Rect.Top, l.Rect.Width, l.Rect.Height,
                    l.Drawn)).ToList(), margin);
        }
        finally
        {
            foreach (var bitmap in images.Values)
                bitmap?.Dispose();
        }
    }

    /// <summary>
    ///     Downloads and decodes an image.
    /// </summary>
    /// <param name="url">The link.</param>
    /// <returns>The image, or null when it can't be loaded.</returns>
    public async Task<SKBitmap?> LoadBitmapAsync(string url)
    {
        return await FetchAsync(url) is { } bytes ? SKBitmap.Decode(bytes) : null;
    }

    private async Task<Dictionary<string, SKBitmap?>> LoadImagesAsync(CardContent content,
        CardTemplate template)
    {
        var images = new Dictionary<string, SKBitmap?>();
        foreach (var key in content.ImageKeys)
            images[key] = await content.LoadImageAsync(key);
        var links = template.Elements
            .Where(e => e.Visible && e.Type == CardElementType.Image && e.Url.Length > 0)
            .Select(e => e.Url)
            .Append(template.Background.Kind == "image" ? template.Background.Url : "")
            .Where(u => u.Length > 0)
            .Distinct();
        foreach (var link in links)
            images["url:" + link] = CardPresets.TryParse(link, out _)
                ? await presets.LoadBitmapAsync(link)
                : await content.LoadLinkAsync(link);
        return images;
    }

    private static void DrawShadow(SKCanvas canvas, CardTemplate template)
    {
        using var shadow = new SKPaint();
        shadow.IsAntialias = true;
        shadow.Color = SKColors.Black.WithAlpha(90);
        shadow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10);
        canvas.DrawRoundRect(new SKRect(6, 12, template.Width - 6, template.Height + 4), template.Radius,
            template.Radius, shadow);
    }

    private static void DrawBackground(SKCanvas canvas, CardTemplate template, DashboardPalette palette,
        SKColor accent, Dictionary<string, SKBitmap?> images)
    {
        var bounds = new SKRect(0, 0, template.Width, template.Height);
        var bg = template.Background;
        canvas.Clear(DashboardPalette.Page);
        switch (bg.Kind)
        {
            case "solid":
                if (CardRules.Resolve(bg.Color, palette, accent) is { } solid)
                    canvas.Clear(solid);
                break;
            case "gradient":
            {
                using var paint = new SKPaint();
                paint.IsAntialias = true;
                var (start, end) = CssAngle(bg.Angle, bounds);
                paint.Shader = SKShader.CreateLinearGradient(start, end,
                [
                    CardRules.Resolve(bg.Color, palette, accent) ?? DashboardPalette.Page,
                    CardRules.Resolve(bg.Color2, palette, accent) ?? DashboardPalette.Page
                ], SKShaderTileMode.Clamp);
                canvas.DrawRect(bounds, paint);
                break;
            }
            case "image":
                if (images.GetValueOrDefault("url:" + bg.Url) is { } bitmap)
                {
                    using var img = SKImage.FromBitmap(bitmap);
                    canvas.DrawImage(img, bg.Fit == "contain" ? Contain(bitmap, bounds) : Cover(bitmap, bounds),
                        Sampling);
                }
                else if (CardRules.Resolve(bg.Color, palette, accent) is { } fallback)
                {
                    canvas.Clear(fallback);
                }

                using (var dim = new SKPaint())
                {
                    dim.Color = DashboardPalette.Page.WithAlpha((byte)(255 * bg.Dim));
                    canvas.DrawRect(bounds, dim);
                }

                break;
        }

        if (bg.Kind == "palette" || bg.Wash)
            DrawWash(canvas, bounds, palette);
    }

    private static void DrawWash(SKCanvas canvas, SKRect bounds, DashboardPalette palette)
    {
        using (var glow = new SKPaint())
        {
            glow.IsAntialias = true;
            var radius = MathF.Sqrt(bounds.Width / 2 * (bounds.Width / 2) + bounds.Height * bounds.Height);
            glow.Shader = SKShader.CreateRadialGradient(new SKPoint(bounds.MidX, 0), radius,
            [
                palette.GradientStartColor.WithAlpha(0x15), palette.GradientMidColor.WithAlpha(0x10),
                palette.GradientEndColor.WithAlpha(0x05)
            ], [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawRect(bounds, glow);
        }

        using var wash = new SKPaint();
        wash.IsAntialias = true;
        var (start, end) = CssAngle(135, bounds);
        wash.Shader = SKShader.CreateLinearGradient(start, end,
        [
            palette.GradientStartColor.WithAlpha(0x10), palette.GradientMidColor.WithAlpha(0x15),
            palette.GradientEndColor.WithAlpha(0x10)
        ], [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
        canvas.DrawRect(bounds, wash);
    }

    private static void DrawBorder(SKCanvas canvas, CardTemplate template, DashboardPalette palette, SKColor accent)
    {
        if (template.BorderWidth <= 0 || CardRules.Resolve(template.BorderColor, palette, accent) is not
                { } color)
            return;
        using var border = new SKPaint();
        border.IsAntialias = true;
        border.Style = SKPaintStyle.Stroke;
        border.StrokeWidth = template.BorderWidth;
        border.Color = color;
        var inset = template.BorderWidth / 2;
        canvas.DrawRoundRect(new SKRect(inset, inset, template.Width - inset, template.Height - inset),
            Math.Max(0, template.Radius - inset), Math.Max(0, template.Radius - inset), border);
    }

    /// <summary>
    ///     An element after layout: where it sits, whether it is drawn, and the text lines it wraps to.
    /// </summary>
    private sealed class Laid
    {
        public required CardElement Element { get; set; }
        public SKRect Rect { get; set; }
        public bool Drawn { get; set; }
        public List<List<CardTextPiece>> Lines { get; set; } = [];
        public float LineHeight { get; set; }
        public float BaseHeight { get; set; }
        public float UsedHeight { get; set; }
        public List<CardTextPiece> Pill { get; set; } = [];
        public AchievementGlyph? Glyph { get; set; }
    }

    /// <summary>
    ///     The state of one card being drawn.
    /// </summary>
    private sealed class DrawContext(
        CardRenderer renderer,
        CardContent content,
        CardTemplate template,
        DashboardPalette palette,
        SKColor accent,
        Dictionary<string, SKBitmap?> images,
        string? hiddenTextId)
    {
        private readonly Dictionary<string, Laid> laid = new();
        private readonly HashSet<string> visiting = [];

        public List<Laid> Layout()
        {
            return template.Elements.Select(LayOut).ToList();
        }

        public IEnumerable<string> EmojiUrls()
        {
            return template.Elements
                .Where(e => e.Visible)
                .SelectMany(e => CardText.Parse(content.TextOf(e)))
                .Where(p => p.Kind == CardTextPieceKind.Emoji)
                .Select(p => p.Url!)
                .Distinct();
        }

        private List<CardTextPiece> Pieces(CardElement e)
        {
            return CardText.Parse(content.TextOf(e)).Select(p => p.Kind switch
            {
                CardTextPieceKind.Text when e.Uppercase => p with
                {
                    Text = p.Text.ToUpperInvariant()
                },
                CardTextPieceKind.Emoji when images.GetValueOrDefault("emoji:" + p.Url) is null =>
                    new CardTextPiece(CardTextPieceKind.Text, p.Text),
                _ => p
            }).ToList();
        }

        private Laid LayOut(CardElement e)
        {
            if (laid.TryGetValue(e.Id, out var done))
                return done;
            var item = new Laid
            {
                Element = e
            };
            if (!visiting.Add(e.Id))
            {
                item.Rect = new SKRect(e.X, e.Y, e.X + e.W, e.Y + e.H);
                return item;
            }

            var top = e.Y;
            var height = e.H;
            if (e.FollowId.Length > 0 && template.Elements.FirstOrDefault(x => x.Id == e.FollowId) is { } followed)
            {
                var anchor = LayOut(followed);
                var shift = anchor.UsedHeight - anchor.BaseHeight;
                top += shift;
                if (shift > 0)
                    height = Math.Max(0, height - shift);
            }

            item.Rect = new SKRect(e.X, top, e.X + e.W, top + height);
            item.Drawn = e.Visible && content.IsShown(e);

            if (CardElementType.TextKinds.Contains(e.Type))
            {
                LayOutText(item);
            }
            else if (CardElementType.PillKinds.Contains(e.Type))
            {
                LayOutPill(item);
            }
            else
            {
                switch (e.Type)
                {
                    case CardElementType.Progress:
                        item.Drawn &= content.Progress is not null;
                        break;
                    case CardElementType.Image:
                        item.Drawn &= images.GetValueOrDefault("url:" + e.Url) is not null;
                        break;
                    case CardElementType.Glyph:
                        item.Drawn &= AchievementIcons.FindGlyph(e.Glyph) is not null;
                        break;
                }
            }

            if (item.BaseHeight == 0)
            {
                item.BaseHeight = e.H;
                item.UsedHeight = item.Drawn ? item.Rect.Height : 0;
            }

            visiting.Remove(e.Id);
            laid[e.Id] = item;
            return item;
        }

        private void LayOutText(Laid item)
        {
            var e = item.Element;
            item.Glyph = TextGlyph(e);
            var pieces = Pieces(e);
            if (e.Shrink && item.Drawn && e.MaxLines == 1 && e.Type != CardElementType.Member && HasContent(pieces))
                e = item.Element = Shrunk(e, pieces, item.Rect.Width, item.Glyph is not null);
            using var font = renderer.Font(e);
            item.LineHeight = e.FontSize * e.LineHeight;
            item.BaseHeight = item.LineHeight;
            var maxLines = e.Type == CardElementType.Member
                ? 1
                : Math.Min(e.MaxLines, (int)Math.Floor(item.Rect.Height / item.LineHeight + 0.01f));
            var glyphWidth = item.Glyph is null ? 0 : e.FontSize * TextGlyphSlot;
            item.Lines = item.Drawn && maxLines > 0 && HasContent(pieces)
                ? Wrap(pieces, font, item.Rect.Width - glyphWidth, maxLines, e.Spacing, e.FontSize)
                : [];
            item.Drawn &= item.Lines.Count > 0;
            item.UsedHeight = item.Drawn ? item.Lines.Count * item.LineHeight : 0;
        }

        private CardElement Shrunk(CardElement e, List<CardTextPiece> pieces, float width, bool glyph)
        {
            var size = e.FontSize;
            while (size > MinShrinkSize)
            {
                var trial = e.WithFontSize(size);
                using var font = renderer.Font(trial);
                if (MeasurePieces(pieces, font, e.Spacing, size) + (glyph ? size * TextGlyphSlot : 0) <= width)
                    break;
                size -= 2;
            }

            return size == e.FontSize ? e : e.WithFontSize(size);
        }

        private void LayOutPill(Laid item)
        {
            var e = item.Element;
            item.Pill = Pieces(e)
                .Select(p => p.Kind == CardTextPieceKind.Break ? CardText.Space : p)
                .ToList();
            item.Drawn &= HasContent(item.Pill);
            item.Glyph = TextGlyph(e);
            if (!e.AutoWidth)
                return;

            using var font = renderer.Font(e);
            var width = Math.Min(item.Rect.Width,
                MeasurePieces(item.Pill, font, e.Spacing, e.FontSize) + PillPadding * 2 +
                (item.Glyph is null ? 0 : PillGlyphSlot));
            var box = item.Rect;
            var left = e.Align switch
            {
                "right" => box.Right - width,
                "center" => box.MidX - width / 2,
                _ => box.Left
            };
            if (e.BesideId.Length > 0 && template.Elements.FirstOrDefault(x => x.Id == e.BesideId) is { } other)
            {
                var beside = LayOut(other);
                if (beside.Drawn && e.Align == "right")
                    left = beside.Rect.Left - e.Gap - width;
                else if (beside.Drawn && e.Align == "left")
                    left = beside.Rect.Right + e.Gap;
            }

            item.Rect = new SKRect(left, box.Top, left + width, box.Bottom);
        }

        private AchievementGlyph? TextGlyph(CardElement e)
        {
            if (e.Glyph == "none")
                return null;
            return e.Glyph.Length > 0 ? AchievementIcons.FindGlyph(e.Glyph) : content.DefaultGlyph(e);
        }

        private SKColor? Color(string token)
        {
            return CardRules.Resolve(token, palette, accent);
        }

        public void Draw(SKCanvas canvas, Laid item)
        {
            var e = item.Element;
            var rect = item.Rect;
            canvas.Save();
            if (e.Rotation != 0)
                canvas.RotateDegrees(e.Rotation, rect.MidX, rect.MidY);
            var layered = e.Opacity < 1;
            if (layered)
            {
                using var alpha = new SKPaint();
                alpha.Color = SKColors.White.WithAlpha((byte)(255 * e.Opacity));
                canvas.SaveLayer(alpha);
            }

            switch (e.Type)
            {
                case CardElementType.Rectangle:
                    DrawShape(canvas, rect, e, false);
                    break;
                case CardElementType.Ellipse:
                    DrawShape(canvas, rect, e, true);
                    break;
                case CardElementType.Icon:
                    DrawIcon(canvas, rect, e);
                    break;
                case CardElementType.Glyph:
                    renderer.DrawGlyph(canvas, AchievementIcons.FindGlyph(e.Glyph), new SKPoint(rect.MidX, rect.MidY),
                        Math.Min(rect.Width, rect.Height) * 0.9f, Color(e.Color), Color(e.Color3) ?? Color(e.Color));
                    break;
                case CardElementType.Image:
                    DrawImage(canvas, rect, e, images.GetValueOrDefault("url:" + e.Url));
                    break;
                case CardElementType.Avatar:
                    DrawPortrait(canvas, rect, e, "avatar");
                    break;
                case CardElementType.ServerIcon:
                    DrawPortrait(canvas, rect, e, "servericon");
                    break;
                case CardElementType.Progress:
                    DrawProgress(canvas, rect, e);
                    break;
                case CardElementType.Member:
                    DrawMember(canvas, item);
                    break;
                case CardElementType.Grade:
                case CardElementType.Category:
                case CardElementType.Points:
                case CardElementType.More:
                    DrawPill(canvas, item);
                    break;
                default:
                    DrawText(canvas, item);
                    break;
            }

            if (layered)
                canvas.Restore();
            canvas.Restore();
        }

        private void DrawShape(SKCanvas canvas, SKRect rect, CardElement e, bool ellipse)
        {
            var radius = Math.Min(e.Radius, Math.Min(rect.Width, rect.Height) / 2);
            if (Color(e.Fill) is { } fill)
            {
                using var paint = new SKPaint();
                paint.IsAntialias = true;
                paint.Color = fill;
                if (Color(e.Fill2) is { } fill2)
                {
                    var (start, end) = CssAngle(e.FillAngle, rect);
                    paint.Shader = SKShader.CreateLinearGradient(start, end, [fill, fill2], SKShaderTileMode.Clamp);
                }

                if (e.ShadowBlur > 0 || e.ShadowX != 0 || e.ShadowY != 0)
                {
                    if (Color(e.ShadowColor) is { } shadow)
                        paint.ImageFilter = SKImageFilter.CreateDropShadow(e.ShadowX, e.ShadowY, e.ShadowBlur / 2,
                            e.ShadowBlur / 2, shadow);
                }

                if (ellipse)
                    canvas.DrawOval(rect, paint);
                else
                    canvas.DrawRoundRect(rect, radius, radius, paint);
            }

            if (e.StrokeWidth <= 0 || Color(e.Stroke) is not { } stroke)
                return;
            using var outline = new SKPaint();
            outline.IsAntialias = true;
            outline.Style = SKPaintStyle.Stroke;
            outline.StrokeWidth = e.StrokeWidth;
            outline.Color = stroke;
            var inner = Inset(rect, e.StrokeWidth / 2);
            if (ellipse)
                canvas.DrawOval(inner, outline);
            else
                canvas.DrawRoundRect(inner, Math.Max(0, radius - e.StrokeWidth / 2),
                    Math.Max(0, radius - e.StrokeWidth / 2), outline);
        }

        private void DrawIcon(SKCanvas canvas, SKRect rect, CardElement e)
        {
            DrawShape(canvas, rect, e, false);
            var center = new SKPoint(rect.MidX, rect.MidY);
            var side = Math.Min(rect.Width, rect.Height);
            var ink = Color(e.Color) ?? accent;
            if (images.GetValueOrDefault("icon") is not { } image)
            {
                renderer.DrawGlyph(canvas, content.IconGlyph, center, side * 0.44f, ink, Color(e.Color3) ?? ink);
                return;
            }

            var size = side * 0.62f;
            var box = Contain(image, new SKRect(center.X - size / 2, center.Y - size / 2, center.X + size / 2,
                center.Y + size / 2));
            using var paint = new SKPaint();
            if (content.Dimmed)
                paint.ColorFilter = SKColorFilter.CreateBlendMode(ink.WithAlpha(150), SKBlendMode.SrcATop);
            using var img = SKImage.FromBitmap(image);
            canvas.DrawImage(img, box, Sampling, paint);
        }

        private void DrawImage(SKCanvas canvas, SKRect rect, CardElement e, SKBitmap? image)
        {
            if (image is null)
                return;
            canvas.Save();
            var radius = Math.Min(e.Radius, Math.Min(rect.Width, rect.Height) / 2);
            using (var clip = new SKRoundRect(rect, radius))
                canvas.ClipRoundRect(clip, SKClipOperation.Intersect, true);
            using var img = SKImage.FromBitmap(image);
            canvas.DrawImage(img, e.Fit == "contain" ? Contain(image, rect) : Cover(image, rect), Sampling);
            canvas.Restore();
            if (e.StrokeWidth > 0)
                DrawShape(canvas, rect, new CardElement
                {
                    Stroke = e.Stroke, StrokeWidth = e.StrokeWidth, Radius = e.Radius
                }, false);
        }

        private void DrawPortrait(SKCanvas canvas, SKRect rect, CardElement e, string key)
        {
            var radius = Math.Min(e.Radius, Math.Min(rect.Width, rect.Height) / 2);
            if (images.GetValueOrDefault(key) is { } portrait)
            {
                canvas.Save();
                using (var clip = new SKRoundRect(rect, radius))
                    canvas.ClipRoundRect(clip, SKClipOperation.Intersect, true);
                using var img = SKImage.FromBitmap(portrait);
                canvas.DrawImage(img, Cover(portrait, rect), Sampling);
                canvas.Restore();
                DrawShape(canvas, rect, new CardElement
                {
                    Stroke = e.Stroke, StrokeWidth = e.StrokeWidth, Radius = e.Radius
                }, false);
                return;
            }

            DrawShape(canvas, rect, e, false);
        }

        private void DrawProgress(SKCanvas canvas, SKRect rect, CardElement e)
        {
            using var font = renderer.Font(e);
            var label = content.ProgressLabel;
            var labelWidth = string.IsNullOrEmpty(label) ? 0 : UnicodeText.Measure(label, font) + 20;
            var track = new SKRect(rect.Left, rect.Top, rect.Right - labelWidth, rect.Bottom);
            var radius = Math.Min(e.Radius > 0 ? e.Radius : track.Height / 2, track.Height / 2);
            if (Color(e.Fill) is { } trackColor)
            {
                using var paint = Ink(trackColor);
                canvas.DrawRoundRect(track, radius, radius, paint);
            }

            var fraction = Math.Clamp(content.Progress ?? 0, 0, 1);
            if (fraction > 0 && Color(e.Color) is { } barColor)
            {
                using var paint = Ink(barColor);
                if (Color(e.Fill2) is { } barEnd)
                    paint.Shader = SKShader.CreateLinearGradient(new SKPoint(track.Left, 0),
                        new SKPoint(track.Right, 0), [barColor, barEnd], SKShaderTileMode.Clamp);
                var width = Math.Max(track.Height, track.Width * fraction);
                canvas.DrawRoundRect(new SKRect(track.Left, track.Top, track.Left + width, track.Bottom), radius,
                    radius, paint);
            }

            if (string.IsNullOrEmpty(label) || Color(e.Color2) is not { } labelColor)
                return;
            using var ink = Ink(labelColor);
            UnicodeText.Draw(canvas, rect.Right - UnicodeText.Measure(label, font), Baseline(font, rect.MidY), label,
                font, ink);
        }

        private void DrawMember(SKCanvas canvas, Laid item)
        {
            var e = item.Element;
            var rect = item.Rect;
            var scopeText = content.Scope ?? "";
            using var nameFont = renderer.Font(e);
            using var scopeFont = new SKFont(renderer.regularFace.Value, e.FontSize);
            var centerY = rect.Top + item.LineHeight / 2;
            var name = Fit(item.Lines[0], nameFont, rect.Width * 0.6f, 0, e.FontSize);
            var nameWidth = MeasurePieces(name, nameFont, 0, e.FontSize);
            var scopeWidth = Math.Max(0, rect.Width - nameWidth - 10);
            var scope = Fit(e.Uppercase ? scopeText.ToUpperInvariant() : scopeText, scopeFont, scopeWidth);
            var total = nameWidth + (scope.Length > 0 ? 10 + UnicodeText.Measure(scope, scopeFont) : 0);
            var x = e.Align switch
            {
                "right" => rect.Right - total,
                "center" => rect.MidX - total / 2,
                _ => rect.Left
            };
            if (Color(e.Color) is { } nameColor)
                DrawPieces(canvas, name, x, centerY, nameFont, nameColor, e, 0);

            if (scope.Length == 0 || Color(e.Color2) is not { } scopeColor)
                return;
            using var scopeInk = Ink(scopeColor);
            UnicodeText.Draw(canvas, x + nameWidth + 10, Baseline(scopeFont, centerY), scope, scopeFont, scopeInk);
        }

        private void DrawPill(SKCanvas canvas, Laid item)
        {
            var e = item.Element;
            var rect = item.Rect;
            DrawShape(canvas, rect, e, false);
            using var font = renderer.Font(e);
            var glyphWidth = item.Glyph is null ? 0 : PillGlyphSlot - 4;
            var available = rect.Width - PillPadding * 2 - glyphWidth;
            var text = Fit(item.Pill, font, available, e.Spacing, e.FontSize);
            var textWidth = MeasurePieces(text, font, e.Spacing, e.FontSize);
            var left = rect.MidX - (glyphWidth + textWidth) / 2;
            if (item.Glyph is not null)
            {
                var glyphInk = Color(e.Color2) ?? Color(e.Color);
                renderer.DrawGlyph(canvas, item.Glyph, new SKPoint(left + e.FontSize * 0.52f, rect.MidY),
                    e.FontSize * 1.05f, glyphInk, Color(e.Color3) ?? glyphInk);
                left += glyphWidth;
            }

            if (Color(e.Color) is { } ink)
                DrawPieces(canvas, text, left, rect.MidY, font, ink, e, e.Spacing);
        }

        private void DrawText(SKCanvas canvas, Laid item)
        {
            var e = item.Element;
            var rect = item.Rect;
            using var font = renderer.Font(e);
            var ink = Color(e.Color);
            var glyphWidth = item.Glyph is null ? 0 : e.FontSize * TextGlyphSlot;
            var shadow = content.TextShadows && e.ShadowColor.Length > 0 && (e.ShadowX != 0 || e.ShadowY != 0)
                ? Color(e.ShadowColor)
                : null;
            for (var i = 0; i < item.Lines.Count; i++)
            {
                var line = item.Lines[i];
                var centerY = rect.Top + item.LineHeight * i + item.LineHeight / 2;
                var lineGlyph = i == 0 ? glyphWidth : 0;
                var width = MeasurePieces(line, font, e.Spacing, e.FontSize) + lineGlyph;
                var x = e.Align switch
                {
                    "right" => rect.Right - width,
                    "center" => rect.MidX - width / 2,
                    _ => rect.Left
                };
                if (i == 0 && item.Glyph is not null)
                {
                    var glyphInk = Color(e.Color2) ?? ink;
                    renderer.DrawGlyph(canvas, item.Glyph, new SKPoint(x + e.FontSize * 0.6f, centerY),
                        e.FontSize * 1.2f, glyphInk, Color(e.Color3) ?? glyphInk);
                }

                if (shadow is { } shadowColor)
                    DrawPieces(canvas, line, x + lineGlyph + e.ShadowX, centerY + e.ShadowY, font, shadowColor, e,
                        e.Spacing, true);
                if (ink is { } color)
                    DrawPieces(canvas, line, x + lineGlyph, centerY, font, color, e, e.Spacing);
            }
        }

        private void DrawPieces(SKCanvas canvas, List<CardTextPiece> pieces, float x, float centerY,
            SKFont font, SKColor ink, CardElement e, float spacing, bool onlyText = false)
        {
            if (e.Id == hiddenTextId)
                return;
            using var paint = Ink(ink);
            var baseline = Baseline(font, centerY);
            var slot = e.FontSize * InlineSlot;
            foreach (var piece in pieces)
            {
                switch (piece.Kind)
                {
                    case CardTextPieceKind.Text:
                        DrawSpaced(canvas, piece.Text, x, baseline, font, paint, spacing);
                        break;
                    case CardTextPieceKind.Glyph when !onlyText:
                        renderer.DrawGlyph(canvas, piece.Glyph, new SKPoint(x + slot / 2, centerY), e.FontSize, ink,
                            Color(e.Color3) ?? ink);
                        break;
                    case CardTextPieceKind.Emoji
                        when !onlyText && images.GetValueOrDefault("emoji:" + piece.Url) is { } image:
                    {
                        var size = e.FontSize * 1.15f;
                        using var img = SKImage.FromBitmap(image);
                        canvas.DrawImage(img,
                            Contain(image, SKRect.Create(x + (slot - size) / 2, centerY - size / 2, size, size)),
                            Sampling);
                        break;
                    }
                }

                x += Measure(piece, font, spacing, e.FontSize);
            }
        }
    }

    private static bool HasContent(List<CardTextPiece> pieces)
    {
        return pieces.Any(p => p.Kind is not (CardTextPieceKind.Space or CardTextPieceKind.Break) &&
                               (p.Kind != CardTextPieceKind.Text || p.Text.Trim().Length > 0));
    }

    private static float Measure(CardTextPiece piece, SKFont font, float spacing, float size)
    {
        return piece.Kind switch
        {
            CardTextPieceKind.Glyph or CardTextPieceKind.Emoji => size * InlineSlot,
            CardTextPieceKind.Space or CardTextPieceKind.Break => font.MeasureText(" ") + spacing * 2,
            _ => MeasureSpaced(piece.Text, font, spacing)
        };
    }

    private static float MeasurePieces(List<CardTextPiece> pieces, SKFont font, float spacing, float size)
    {
        return pieces.Sum(p => Measure(p, font, spacing, size));
    }

    private static List<List<CardTextPiece>> Wrap(List<CardTextPiece> pieces, SKFont font,
        float maxWidth, int maxLines, float spacing, float size)
    {
        var words = new List<List<CardTextPiece>?>();
        var word = new List<CardTextPiece>();
        foreach (var piece in pieces)
        {
            if (piece.Kind is not (CardTextPieceKind.Space or CardTextPieceKind.Break))
            {
                word.Add(piece);
                continue;
            }

            if (word.Count > 0)
                words.Add(word);
            word = [];
            if (piece.Kind == CardTextPieceKind.Break)
                words.Add(null);
        }

        if (word.Count > 0)
            words.Add(word);

        var space = Measure(CardText.Space, font, spacing, size);
        var lines = new List<List<CardTextPiece>>();
        var current = new List<CardTextPiece>();
        for (var i = 0; i < words.Count; i++)
        {
            if (lines.Count == maxLines - 1)
            {
                foreach (var rest in words.Skip(i).OfType<List<CardTextPiece>>())
                    Append(current, rest);
                break;
            }

            if (words[i] is not { } next)
            {
                lines.Add(current);
                current = [];
                continue;
            }

            if (current.Count == 0 || MeasurePieces(current, font, spacing, size) + space +
                MeasurePieces(next, font, spacing, size) <= maxWidth)
            {
                Append(current, next);
                continue;
            }

            lines.Add(current);
            current = [..next];
        }

        if (current.Count > 0)
            lines.Add(current);
        while (lines.Count > 0 && lines[^1].Count == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines.Take(maxLines).Select(l => Fit(l, font, maxWidth, spacing, size)).ToList();

        static void Append(List<CardTextPiece> line, List<CardTextPiece> next)
        {
            if (line.Count > 0)
                line.Add(CardText.Space);
            line.AddRange(next);
        }
    }

    private static List<CardTextPiece> Fit(List<CardTextPiece> pieces, SKFont font, float maxWidth,
        float spacing, float size)
    {
        if (maxWidth <= 0)
            return [];
        if (MeasurePieces(pieces, font, spacing, size) <= maxWidth)
            return pieces;
        var ellipsis = MeasureSpaced("...", font, spacing);
        var trimmed = pieces.ToList();
        while (trimmed.Count > 0 && MeasurePieces(trimmed, font, spacing, size) + ellipsis > maxWidth)
        {
            var last = trimmed[^1];
            if (last.Kind == CardTextPieceKind.Text && last.Text.Length > 1)
            {
                var cut = char.IsLowSurrogate(last.Text[^1]) ? 2 : 1;
                trimmed[^1] = last with
                {
                    Text = last.Text[..^cut]
                };
            }
            else
            {
                trimmed.RemoveAt(trimmed.Count - 1);
            }
        }

        while (trimmed.Count > 0 && trimmed[^1].Kind == CardTextPieceKind.Space)
            trimmed.RemoveAt(trimmed.Count - 1);
        trimmed.Add(new CardTextPiece(CardTextPieceKind.Text, "..."));
        return trimmed;
    }

    private SKFont Font(CardElement e)
    {
        var face = e.Font == UniSans ? uniSansFace.Value : e.Bold ? boldFace.Value : regularFace.Value;
        return new SKFont(face, e.FontSize);
    }

    /// <summary>
    ///     A Font Awesome utility duo glyph: the back layer at half opacity under the front layer, as the dashboard's
    ///     <c>fa-utility-duo</c> class draws it.
    /// </summary>
    private void DrawGlyph(SKCanvas canvas, AchievementGlyph? glyph, SKPoint center, float size, SKColor? front,
        SKColor? back)
    {
        if (glyph is null || glyphFace.Value is null || front is null)
            return;
        using var font = new SKFont(glyphFace.Value, size);
        var frontText = char.ConvertFromUtf32(glyph.Codepoint);
        var backText = char.ConvertFromUtf32(glyph.Codepoint + 0x100000);
        font.MeasureText(frontText, out var bounds);
        var x = center.X - bounds.MidX;
        var y = center.Y - bounds.MidY;
        var backColor = back ?? front.Value;
        using var backPaint = Ink(backColor.WithAlpha((byte)(backColor.Alpha / 2)));
        using var frontPaint = Ink(front.Value);
        canvas.DrawText(backText, x, y, SKTextAlign.Left, font, backPaint);
        canvas.DrawText(frontText, x, y, SKTextAlign.Left, font, frontPaint);
    }

    /// <summary>
    ///     The baseline that centers a line of text vertically on a point.
    /// </summary>
    private static float Baseline(SKFont font, float centerY)
    {
        var metrics = font.Metrics;
        return centerY - (metrics.Ascent + metrics.Descent) / 2;
    }

    /// <summary>
    ///     The start and end points of a CSS <c>linear-gradient(&lt;angle&gt;deg, ...)</c> over a box.
    /// </summary>
    private static (SKPoint Start, SKPoint End) CssAngle(float degrees, SKRect box)
    {
        var radians = degrees * MathF.PI / 180;
        var dx = MathF.Sin(radians);
        var dy = -MathF.Cos(radians);
        var half = (MathF.Abs(box.Width * dx) + MathF.Abs(box.Height * dy)) / 2;
        return (new SKPoint(box.MidX - dx * half, box.MidY - dy * half),
            new SKPoint(box.MidX + dx * half, box.MidY + dy * half));
    }

    private static SKRect Inset(SKRect rect, float amount)
    {
        return new SKRect(rect.Left + amount, rect.Top + amount, rect.Right - amount, rect.Bottom - amount);
    }

    private static SKTypeface LoadFace(string path)
    {
        return File.Exists(path) ? SKTypeface.FromFile(path) ?? SKTypeface.Default : SKTypeface.Default;
    }

    private static void DrawSpaced(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint,
        float spacing)
    {
        if (spacing == 0)
        {
            UnicodeText.Draw(canvas, x, y, text, font, paint);
            return;
        }

        var letters = StringInfo.GetTextElementEnumerator(text);
        while (letters.MoveNext())
            x += UnicodeText.Draw(canvas, x, y, letters.GetTextElement(), font, paint) + spacing;
    }

    private static float MeasureSpaced(string text, SKFont font, float spacing)
    {
        if (spacing == 0 || text.Length == 0)
            return UnicodeText.Measure(text, font);
        var width = 0f;
        var count = 0;
        var letters = StringInfo.GetTextElementEnumerator(text);
        while (letters.MoveNext())
        {
            width += UnicodeText.Measure(letters.GetTextElement(), font);
            count++;
        }

        return width + spacing * (count - 1);
    }

    private static string Fit(string text, SKFont font, float maxWidth, float spacing = 0)
    {
        if (maxWidth <= 0)
            return "";
        if (MeasureSpaced(text, font, spacing) <= maxWidth)
            return text;
        var trimmed = text;
        while (trimmed.Length > 1 && MeasureSpaced(trimmed + "...", font, spacing) > maxWidth)
            trimmed = trimmed[..StringInfo.ParseCombiningCharacters(trimmed)[^1]];
        return trimmed.TrimEnd() + "...";
    }

    private static SKRect Contain(SKBitmap image, SKRect box)
    {
        var scale = Math.Min(box.Width / image.Width, box.Height / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        return new SKRect(box.MidX - width / 2, box.MidY - height / 2, box.MidX + width / 2, box.MidY + height / 2);
    }

    private static SKRect Cover(SKBitmap image, SKRect box)
    {
        var scale = Math.Max(box.Width / image.Width, box.Height / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        return new SKRect(box.MidX - width / 2, box.MidY - height / 2, box.MidX + width / 2, box.MidY + height / 2);
    }

    private async Task<SKBitmap?> LoadEmojiAsync(string url)
    {
        if (!EmojiCache.TryGetValue(url, out var bytes))
        {
            if (EmojiMisses.TryGetValue(url, out var missed) && DateTime.UtcNow - missed < EmojiRetry)
                return null;
            bytes = await FetchAsync(url);
            if (bytes is null)
            {
                if (EmojiMisses.Count >= MaxCachedEmoji)
                    EmojiMisses.Clear();
                EmojiMisses[url] = DateTime.UtcNow;
                return null;
            }

            if (EmojiCache.Count >= MaxCachedEmoji)
                EmojiCache.Clear();
            EmojiCache[url] = bytes;
        }

        return SKBitmap.Decode(bytes);
    }

    private async Task<byte[]?> FetchAsync(string url)
    {
        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(5);
            return await http.GetByteArrayAsync(url);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not load {Url} for a card image", url);
            return null;
        }
    }

    private static SKPaint Ink(SKColor color)
    {
        var paint = new SKPaint();
        paint.IsAntialias = true;
        paint.Color = color;
        return paint;
    }
}
