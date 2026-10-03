using System.IO;
using System.Net.Http;
using Mewdeko.Common.Palette;
using Mewdeko.Modules.Achievements.Common;
using SkiaSharp;

namespace Mewdeko.Modules.Achievements.Services;

/// <summary>
///     Draws an achievement card from a server's card template: its background, then each element back to front,
///     with colors resolved from the server's dashboard palette and the achievement's grade.
/// </summary>
public sealed class AchievementUnlockRenderer : INService
{
    /// <summary>
    ///     Transparent space around the card. Discord rounds the corners of image attachments, much more on mobile,
    ///     so the card sits inside this margin and the rounding only cuts empty pixels and the edge of the shadow.
    /// </summary>
    public const int Margin = 40;

    private const string BoldFontPath = "data/fonts/NotoSans-Bold.ttf";
    private const string RegularFontPath = "data/fonts/NotoSans-Regular.ttf";
    private const string GlyphFontPath = "data/fonts/FontAwesomeUtilityDuo.otf";
    private const float PillPadding = 16;
    private const float PillGlyphSlot = 36;
    private const float TextGlyphSlot = 1.8f;

    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly AchievementIconService icons;
    private readonly ILogger<AchievementUnlockRenderer> logger;
    private readonly Lazy<SKTypeface> boldFace;
    private readonly Lazy<SKTypeface> regularFace;
    private readonly Lazy<SKTypeface?> glyphFace;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AchievementUnlockRenderer" /> class.
    /// </summary>
    /// <param name="icons">Icon and uploaded images.</param>
    /// <param name="httpClientFactory">HTTP clients, for avatars.</param>
    /// <param name="logger">Logger instance.</param>
    public AchievementUnlockRenderer(AchievementIconService icons, IHttpClientFactory httpClientFactory,
        ILogger<AchievementUnlockRenderer> logger)
    {
        this.icons = icons;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
        boldFace = new Lazy<SKTypeface>(() => LoadFace(BoldFontPath));
        regularFace = new Lazy<SKTypeface>(() => File.Exists(RegularFontPath) ? LoadFace(RegularFontPath) : boldFace.Value);
        glyphFace = new Lazy<SKTypeface?>(() => File.Exists(GlyphFontPath) ? SKTypeface.FromFile(GlyphFontPath) : null);
    }

    /// <summary>
    ///     Everything the card shows.
    /// </summary>
    public sealed class UnlockImageData
    {
        /// <summary>
        ///     The server the achievement belongs to.
        /// </summary>
        public required ulong GuildId { get; init; }

        /// <summary>
        ///     The server's dashboard palette.
        /// </summary>
        public required DashboardPalette Palette { get; init; }

        /// <summary>
        ///     The achievement.
        /// </summary>
        public required AchievementDefinition Definition { get; init; }

        /// <summary>
        ///     Its icon, already resolved to the category's when it has none.
        /// </summary>
        public required AchievementIconRef Icon { get; init; }

        /// <summary>
        ///     Its category's name.
        /// </summary>
        public required string CategoryName { get; init; }

        /// <summary>
        ///     Its category's icon.
        /// </summary>
        public required AchievementIconRef CategoryIcon { get; init; }

        /// <summary>
        ///     The state line, such as "Achievement unlocked".
        /// </summary>
        public required string Label { get; init; }

        /// <summary>
        ///     The points badge, such as "+50 points".
        /// </summary>
        public required string PointsLabel { get; init; }

        /// <summary>
        ///     Who earned it.
        /// </summary>
        public required string MemberName { get; init; }

        /// <summary>
        ///     Their avatar.
        /// </summary>
        public string? AvatarUrl { get; init; }

        /// <summary>
        ///     Where, shown after the member's name.
        /// </summary>
        public required string Scope { get; init; }

        /// <summary>
        ///     A badge for other unlocks at the same time, such as "+2 more", or null.
        /// </summary>
        public string? MoreLabel { get; init; }

        /// <summary>
        ///     Progress toward it from 0 to 1, or null for none.
        /// </summary>
        public float? Progress { get; init; }

        /// <summary>
        ///     Text beside the progress bar, such as "120 / 250".
        /// </summary>
        public string? ProgressLabel { get; init; }

        /// <summary>
        ///     Whether the achievement is not unlocked yet.
        /// </summary>
        public bool Locked { get; init; }
    }

    /// <summary>
    ///     Where an element landed on the card, for editors to draw handles over the image.
    /// </summary>
    /// <param name="Id">The element ID.</param>
    /// <param name="X">Left edge in card pixels.</param>
    /// <param name="Y">Top edge in card pixels.</param>
    /// <param name="W">Width.</param>
    /// <param name="H">Height.</param>
    /// <param name="Drawn">Whether it was drawn on this card.</param>
    public sealed record ElementBox(string Id, float X, float Y, float W, float H, bool Drawn);

    /// <summary>
    ///     A drawn card.
    /// </summary>
    /// <param name="Image">The PNG, positioned at the start.</param>
    /// <param name="CardWidth">The card's width, without the margin.</param>
    /// <param name="CardHeight">The card's height, without the margin.</param>
    /// <param name="Layout">Where each element landed.</param>
    public sealed record RenderResult(MemoryStream Image, int CardWidth, int CardHeight, IReadOnlyList<ElementBox> Layout);

    /// <summary>
    ///     Draws a card.
    /// </summary>
    /// <param name="data">What to show.</param>
    /// <param name="template">The design, or null for the default.</param>
    /// <returns>A PNG stream positioned at the start.</returns>
    public async Task<MemoryStream> RenderAsync(UnlockImageData data, AchievementCardTemplate? template = null)
    {
        return (await RenderWithLayoutAsync(data, template ?? AchievementCardTemplate.Default())).Image;
    }

    /// <summary>
    ///     Draws a card and reports where each element landed.
    /// </summary>
    /// <param name="data">What to show.</param>
    /// <param name="template">The design.</param>
    /// <returns>The image and layout.</returns>
    public async Task<RenderResult> RenderWithLayoutAsync(UnlockImageData data, AchievementCardTemplate template)
    {
        var palette = data.Palette;
        var grade = data.Locked
            ? palette.MutedColor
            : ToColor(AchievementCatalog.GetGrade(data.Definition.Grade).Color);
        var images = await LoadImagesAsync(data, template);
        try
        {
            var context = new DrawContext(this, data, template, palette, grade, images);
            var laid = context.Layout();

            using var surface = SKSurface.Create(new SKImageInfo(template.Width + Margin * 2,
                template.Height + Margin * 2));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(Margin, Margin);

            if (template.Shadow)
                DrawShadow(canvas, template);
            canvas.Save();
            using (var clip = new SKRoundRect(new SKRect(0, 0, template.Width, template.Height), template.Radius))
                canvas.ClipRoundRect(clip, SKClipOperation.Intersect, true);
            DrawBackground(canvas, template, palette, grade, images);
            foreach (var item in laid.Where(l => l.Drawn))
                context.Draw(canvas, item);
            canvas.Restore();
            DrawBorder(canvas, template, palette, grade);

            using var image = surface.Snapshot();
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            var stream = new MemoryStream();
            encoded.SaveTo(stream);
            stream.Position = 0;
            return new RenderResult(stream, template.Width, template.Height,
                laid.Select(l => new ElementBox(l.Element.Id, l.Rect.Left, l.Rect.Top, l.Rect.Width, l.Rect.Height,
                    l.Drawn)).ToList());
        }
        finally
        {
            foreach (var bitmap in images.Values)
                bitmap?.Dispose();
        }
    }

    private async Task<Dictionary<string, SKBitmap?>> LoadImagesAsync(UnlockImageData data,
        AchievementCardTemplate template)
    {
        var images = new Dictionary<string, SKBitmap?>
        {
            ["icon"] = await icons.LoadImageAsync(data.GuildId, data.Icon),
            ["avatar"] = data.AvatarUrl is null ? null : await LoadAsync(data.AvatarUrl)
        };
        var links = template.Elements
            .Where(e => e.Visible && e.Type == AchievementCardElementType.Image && e.Url.Length > 0)
            .Select(e => e.Url)
            .Append(template.Background.Kind == "image" ? template.Background.Url : "")
            .Where(u => u.Length > 0)
            .Distinct();
        foreach (var link in links)
            images["url:" + link] = await icons.LoadImageAsync(data.GuildId, AchievementIcons.Parse(link));
        return images;
    }

    private static void DrawShadow(SKCanvas canvas, AchievementCardTemplate template)
    {
        using var shadow = new SKPaint();
        shadow.IsAntialias = true;
        shadow.Color = SKColors.Black.WithAlpha(90);
        shadow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10);
        canvas.DrawRoundRect(new SKRect(6, 12, template.Width - 6, template.Height + 4), template.Radius,
            template.Radius, shadow);
    }

    private static void DrawBackground(SKCanvas canvas, AchievementCardTemplate template, DashboardPalette palette,
        SKColor grade, Dictionary<string, SKBitmap?> images)
    {
        var bounds = new SKRect(0, 0, template.Width, template.Height);
        var bg = template.Background;
        canvas.Clear(DashboardPalette.Page);
        switch (bg.Kind)
        {
            case "solid":
                if (AchievementCardRules.Resolve(bg.Color, palette, grade) is { } solid)
                    canvas.Clear(solid);
                break;
            case "gradient":
            {
                using var paint = new SKPaint();
                paint.IsAntialias = true;
                var (start, end) = CssAngle(bg.Angle, bounds);
                paint.Shader = SKShader.CreateLinearGradient(start, end,
                [
                    AchievementCardRules.Resolve(bg.Color, palette, grade) ?? DashboardPalette.Page,
                    AchievementCardRules.Resolve(bg.Color2, palette, grade) ?? DashboardPalette.Page
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

    private static void DrawBorder(SKCanvas canvas, AchievementCardTemplate template, DashboardPalette palette,
        SKColor grade)
    {
        if (template.BorderWidth <= 0 || AchievementCardRules.Resolve(template.BorderColor, palette, grade) is not
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
        public required AchievementCardElement Element { get; init; }
        public SKRect Rect { get; set; }
        public bool Drawn { get; set; }
        public List<string> Lines { get; set; } = [];
        public float LineHeight { get; set; }
        public float BaseHeight { get; set; }
        public float UsedHeight { get; set; }
        public string PillText { get; set; } = "";
        public AchievementGlyph? Glyph { get; set; }
    }

    /// <summary>
    ///     The state of one card being drawn.
    /// </summary>
    private sealed class DrawContext(
        AchievementUnlockRenderer renderer,
        UnlockImageData data,
        AchievementCardTemplate template,
        DashboardPalette palette,
        SKColor grade,
        Dictionary<string, SKBitmap?> images)
    {
        private readonly Dictionary<string, Laid> laid = new();
        private readonly HashSet<string> visiting = [];

        public List<Laid> Layout()
        {
            return template.Elements.Select(LayOut).ToList();
        }

        private Laid LayOut(AchievementCardElement e)
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
            item.Drawn = e.Visible && e.Show switch
            {
                AchievementCardShow.Unlocked => !data.Locked,
                AchievementCardShow.Locked => data.Locked,
                _ => true
            };

            switch (e.Type)
            {
                case AchievementCardElementType.Label:
                case AchievementCardElementType.Title:
                case AchievementCardElementType.Description:
                case AchievementCardElementType.Text:
                case AchievementCardElementType.Member:
                    LayOutText(item);
                    break;
                case AchievementCardElementType.Grade:
                case AchievementCardElementType.Category:
                case AchievementCardElementType.Points:
                case AchievementCardElementType.More:
                    LayOutPill(item);
                    break;
                case AchievementCardElementType.Progress:
                    item.Drawn &= data.Progress is not null;
                    break;
                case AchievementCardElementType.Image:
                    item.Drawn &= images.GetValueOrDefault("url:" + e.Url) is not null;
                    break;
                case AchievementCardElementType.Glyph:
                    item.Drawn &= AchievementIcons.FindGlyph(e.Glyph) is not null;
                    break;
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
            using var font = renderer.Font(e);
            item.LineHeight = e.FontSize * e.LineHeight;
            item.BaseHeight = item.LineHeight;
            item.Glyph = TextGlyph(e);
            var text = e.Type switch
            {
                AchievementCardElementType.Label => data.Label,
                AchievementCardElementType.Title => data.Definition.Name,
                AchievementCardElementType.Description => data.Definition.Description,
                AchievementCardElementType.Member => data.MemberName,
                _ => Resolve(e.Text)
            };
            if (e.Uppercase)
                text = text.ToUpperInvariant();

            var maxLines = e.Type == AchievementCardElementType.Member
                ? 1
                : Math.Min(e.MaxLines, (int)Math.Floor(item.Rect.Height / item.LineHeight + 0.01f));
            var glyphWidth = item.Glyph is null ? 0 : e.FontSize * TextGlyphSlot;
            item.Lines = item.Drawn && maxLines > 0 && text.Trim().Length > 0
                ? Wrap(text, font, item.Rect.Width - glyphWidth, maxLines, e.Spacing)
                : [];
            item.Drawn &= item.Lines.Count > 0;
            item.UsedHeight = item.Drawn ? item.Lines.Count * item.LineHeight : 0;
        }

        private void LayOutPill(Laid item)
        {
            var e = item.Element;
            item.PillText = e.Type switch
            {
                AchievementCardElementType.Grade => AchievementCatalog.GetGrade(data.Definition.Grade).Name,
                AchievementCardElementType.Category => data.CategoryName,
                AchievementCardElementType.Points => data.PointsLabel,
                _ => data.MoreLabel ?? ""
            };
            if (e.Uppercase)
                item.PillText = item.PillText.ToUpperInvariant();
            item.Drawn &= item.PillText.Length > 0;
            item.Glyph = e.Glyph == "none"
                ? null
                : e.Glyph.Length > 0
                    ? AchievementIcons.FindGlyph(e.Glyph)
                    : e.Type switch
                    {
                        AchievementCardElementType.Grade => AchievementIcons.FindGlyph(AchievementIcons.GradeGlyph),
                        AchievementCardElementType.Category => GlyphFor(data.CategoryIcon),
                        _ => null
                    };
            if (!e.AutoWidth)
                return;

            using var font = renderer.Font(e);
            var width = Math.Min(item.Rect.Width,
                MeasureSpaced(item.PillText, font, e.Spacing) + PillPadding * 2 + (item.Glyph is null ? 0 : PillGlyphSlot));
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

        private AchievementGlyph? TextGlyph(AchievementCardElement e)
        {
            if (e.Glyph == "none")
                return null;
            if (e.Glyph.Length > 0)
                return AchievementIcons.FindGlyph(e.Glyph);
            return e.Type == AchievementCardElementType.Label
                ? AchievementIcons.FindGlyph(data.Locked ? "lock" : AchievementIcons.GradeGlyph)
                : null;
        }

        private string Resolve(string text)
        {
            var def = data.Definition;
            var percent = data.Progress is { } p ? $"{Math.Round(Math.Clamp(p, 0, 1) * 100)}%" : "";
            return text
                .Replace("{achievement.name}", def.Name)
                .Replace("{achievement.description}", def.Description)
                .Replace("{achievement.grade}", AchievementCatalog.GetGrade(def.Grade).Name)
                .Replace("{achievement.points}", def.Points.ToString("N0"))
                .Replace("{achievement.category}", data.CategoryName)
                .Replace("{user.name}", data.MemberName)
                .Replace("{server.name}", data.Scope)
                .Replace("{label}", data.Label)
                .Replace("{progress}", data.ProgressLabel ?? "")
                .Replace("{progress.percent}", percent)
                .Replace("{more}", data.MoreLabel ?? "");
        }

        private SKColor? Color(string token)
        {
            return AchievementCardRules.Resolve(token, palette, grade);
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
                case AchievementCardElementType.Rectangle:
                    DrawShape(canvas, rect, e, false);
                    break;
                case AchievementCardElementType.Ellipse:
                    DrawShape(canvas, rect, e, true);
                    break;
                case AchievementCardElementType.Icon:
                    DrawIcon(canvas, rect, e);
                    break;
                case AchievementCardElementType.Glyph:
                    renderer.DrawGlyph(canvas, AchievementIcons.FindGlyph(e.Glyph), new SKPoint(rect.MidX, rect.MidY),
                        Math.Min(rect.Width, rect.Height) * 0.9f, Color(e.Color), Color(e.Color3) ?? Color(e.Color));
                    break;
                case AchievementCardElementType.Image:
                    DrawImage(canvas, rect, e, images.GetValueOrDefault("url:" + e.Url));
                    break;
                case AchievementCardElementType.Avatar:
                    DrawAvatar(canvas, rect, e);
                    break;
                case AchievementCardElementType.Progress:
                    DrawProgress(canvas, rect, e);
                    break;
                case AchievementCardElementType.Member:
                    DrawMember(canvas, item);
                    break;
                case AchievementCardElementType.Grade:
                case AchievementCardElementType.Category:
                case AchievementCardElementType.Points:
                case AchievementCardElementType.More:
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

        private void DrawShape(SKCanvas canvas, SKRect rect, AchievementCardElement e, bool ellipse)
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

        private void DrawIcon(SKCanvas canvas, SKRect rect, AchievementCardElement e)
        {
            DrawShape(canvas, rect, e, false);
            var center = new SKPoint(rect.MidX, rect.MidY);
            var side = Math.Min(rect.Width, rect.Height);
            var ink = Color(e.Color) ?? grade;
            if (images.GetValueOrDefault("icon") is not { } image)
            {
                renderer.DrawGlyph(canvas, GlyphFor(data.Icon), center, side * 0.44f, ink, Color(e.Color3) ?? ink);
                return;
            }

            var size = side * 0.62f;
            var box = Contain(image, new SKRect(center.X - size / 2, center.Y - size / 2, center.X + size / 2,
                center.Y + size / 2));
            using var paint = new SKPaint();
            if (data.Locked)
                paint.ColorFilter = SKColorFilter.CreateBlendMode(ink.WithAlpha(150), SKBlendMode.SrcATop);
            using var img = SKImage.FromBitmap(image);
            canvas.DrawImage(img, box, Sampling, paint);
        }

        private void DrawImage(SKCanvas canvas, SKRect rect, AchievementCardElement e, SKBitmap? image)
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
                DrawShape(canvas, rect, new AchievementCardElement
                {
                    Stroke = e.Stroke, StrokeWidth = e.StrokeWidth, Radius = e.Radius
                }, false);
        }

        private void DrawAvatar(SKCanvas canvas, SKRect rect, AchievementCardElement e)
        {
            var radius = Math.Min(e.Radius, Math.Min(rect.Width, rect.Height) / 2);
            if (images.GetValueOrDefault("avatar") is { } avatar)
            {
                canvas.Save();
                using (var clip = new SKRoundRect(rect, radius))
                    canvas.ClipRoundRect(clip, SKClipOperation.Intersect, true);
                using var img = SKImage.FromBitmap(avatar);
                canvas.DrawImage(img, Cover(avatar, rect), Sampling);
                canvas.Restore();
                DrawShape(canvas, rect, new AchievementCardElement
                {
                    Stroke = e.Stroke, StrokeWidth = e.StrokeWidth, Radius = e.Radius
                }, false);
                return;
            }

            DrawShape(canvas, rect, e, false);
        }

        private void DrawProgress(SKCanvas canvas, SKRect rect, AchievementCardElement e)
        {
            using var font = renderer.Font(e);
            var label = data.ProgressLabel;
            var labelWidth = string.IsNullOrEmpty(label) ? 0 : font.MeasureText(label) + 20;
            var track = new SKRect(rect.Left, rect.Top, rect.Right - labelWidth, rect.Bottom);
            var radius = Math.Min(e.Radius > 0 ? e.Radius : track.Height / 2, track.Height / 2);
            if (Color(e.Fill) is { } trackColor)
            {
                using var paint = Ink(trackColor);
                canvas.DrawRoundRect(track, radius, radius, paint);
            }

            var fraction = Math.Clamp(data.Progress ?? 0, 0, 1);
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
            canvas.DrawText(label, rect.Right, Baseline(font, rect.MidY), SKTextAlign.Right, font, ink);
        }

        private void DrawMember(SKCanvas canvas, Laid item)
        {
            var e = item.Element;
            var rect = item.Rect;
            using var nameFont = renderer.Font(e);
            using var scopeFont = new SKFont(renderer.regularFace.Value, e.FontSize);
            var centerY = rect.Top + item.LineHeight / 2;
            var name = Fit(item.Lines[0], nameFont, rect.Width * 0.6f);
            var nameWidth = nameFont.MeasureText(name);
            var scopeWidth = Math.Max(0, rect.Width - nameWidth - 10);
            var scope = Fit(e.Uppercase ? data.Scope.ToUpperInvariant() : data.Scope, scopeFont, scopeWidth);
            var total = nameWidth + (scope.Length > 0 ? 10 + scopeFont.MeasureText(scope) : 0);
            var x = e.Align switch
            {
                "right" => rect.Right - total,
                "center" => rect.MidX - total / 2,
                _ => rect.Left
            };
            if (Color(e.Color) is { } nameColor)
            {
                using var ink = Ink(nameColor);
                canvas.DrawText(name, x, Baseline(nameFont, centerY), SKTextAlign.Left, nameFont, ink);
            }

            if (scope.Length == 0 || Color(e.Color2) is not { } scopeColor)
                return;
            using var scopeInk = Ink(scopeColor);
            canvas.DrawText(scope, x + nameWidth + 10, Baseline(scopeFont, centerY), SKTextAlign.Left, scopeFont,
                scopeInk);
        }

        private void DrawPill(SKCanvas canvas, Laid item)
        {
            var e = item.Element;
            var rect = item.Rect;
            DrawShape(canvas, rect, e, false);
            using var font = renderer.Font(e);
            var textWidth = MeasureSpaced(item.PillText, font, e.Spacing);
            var glyphWidth = item.Glyph is null ? 0 : PillGlyphSlot - 4;
            var available = rect.Width - PillPadding * 2 - glyphWidth;
            var text = textWidth > available ? Fit(item.PillText, font, available) : item.PillText;
            textWidth = MeasureSpaced(text, font, e.Spacing);
            var left = rect.MidX - (glyphWidth + textWidth) / 2;
            if (item.Glyph is not null)
            {
                var glyphInk = Color(e.Color2) ?? Color(e.Color);
                renderer.DrawGlyph(canvas, item.Glyph, new SKPoint(left + e.FontSize * 0.52f, rect.MidY),
                    e.FontSize * 1.05f, glyphInk, Color(e.Color3) ?? glyphInk);
                left += glyphWidth;
            }

            if (Color(e.Color) is not { } ink)
                return;
            using var paint = Ink(ink);
            DrawSpaced(canvas, text, left, Baseline(font, rect.MidY), font, paint, e.Spacing);
        }

        private void DrawText(SKCanvas canvas, Laid item)
        {
            var e = item.Element;
            var rect = item.Rect;
            using var font = renderer.Font(e);
            var ink = Color(e.Color);
            using var paint = ink is { } c ? Ink(c) : null;
            var glyphWidth = item.Glyph is null ? 0 : e.FontSize * TextGlyphSlot;
            for (var i = 0; i < item.Lines.Count; i++)
            {
                var line = item.Lines[i];
                var centerY = rect.Top + item.LineHeight * i + item.LineHeight / 2;
                var lineGlyph = i == 0 ? glyphWidth : 0;
                var width = MeasureSpaced(line, font, e.Spacing) + lineGlyph;
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

                if (paint is not null)
                    DrawSpaced(canvas, line, x + lineGlyph, Baseline(font, centerY), font, paint, e.Spacing);
            }
        }
    }

    private SKFont Font(AchievementCardElement e)
    {
        return new SKFont(e.Bold ? boldFace.Value : regularFace.Value, e.FontSize);
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

    private static AchievementGlyph? GlyphFor(AchievementIconRef icon)
    {
        return icon.Kind == AchievementIconKind.Glyph
            ? AchievementIcons.FindGlyph(icon.Value)
            : AchievementIcons.FindGlyph(AchievementIcons.GradeGlyph);
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
            canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
            return;
        }

        foreach (var letter in text)
        {
            var s = letter.ToString();
            canvas.DrawText(s, x, y, SKTextAlign.Left, font, paint);
            x += font.MeasureText(s) + spacing;
        }
    }

    private static float MeasureSpaced(string text, SKFont font, float spacing)
    {
        if (spacing == 0 || text.Length == 0)
            return font.MeasureText(text);
        return text.Sum(c => font.MeasureText(c.ToString()) + spacing) - spacing;
    }

    private static List<string> Wrap(string text, SKFont font, float maxWidth, int maxLines, float spacing)
    {
        var lines = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = "";
        for (var i = 0; i < words.Length; i++)
        {
            var candidate = current.Length == 0 ? words[i] : $"{current} {words[i]}";
            if (MeasureSpaced(candidate, font, spacing) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
                lines.Add(current);
            current = words[i];
            if (lines.Count == maxLines - 1)
            {
                current = string.Join(' ', words[i..]);
                break;
            }
        }

        if (current.Length > 0)
            lines.Add(current);
        if (lines.Count > 0)
            lines[^1] = Fit(lines[^1], font, maxWidth, spacing);
        return lines.Take(maxLines).ToList();
    }

    private static string Fit(string text, SKFont font, float maxWidth, float spacing = 0)
    {
        if (maxWidth <= 0)
            return "";
        if (MeasureSpaced(text, font, spacing) <= maxWidth)
            return text;
        var trimmed = text;
        while (trimmed.Length > 1 && MeasureSpaced(trimmed + "...", font, spacing) > maxWidth)
            trimmed = trimmed[..^1];
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

    private async Task<SKBitmap?> LoadAsync(string url)
    {
        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(5);
            return SKBitmap.Decode(await http.GetByteArrayAsync(url));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not load an avatar for an achievement image");
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

    private static SKColor ToColor(uint rgb)
    {
        return new SKColor((byte)(rgb >> 16 & 0xFF), (byte)(rgb >> 8 & 0xFF), (byte)(rgb & 0xFF));
    }
}
