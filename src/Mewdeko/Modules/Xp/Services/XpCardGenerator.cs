using System.IO;
using System.Net.Http;
using System.Text.Json;
using DataModel;
using Humanizer;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Xp.Models;
using SkiaSharp;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     A service for generating XP cards using the SkiaSharp graphics library.
/// </summary>
public class XpCardGenerator : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly byte[] defaultBackground;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<XpCardGenerator> logger;
    private readonly XpService xpService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpCardGenerator" /> class.
    /// </summary>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="xpService">The XP service.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public XpCardGenerator(
        IDataConnectionFactory dbFactory,
        XpService xpService,
        IHttpClientFactory httpClientFactory, ILogger<XpCardGenerator> logger)
    {
        this.dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        this.xpService = xpService ?? throw new ArgumentNullException(nameof(xpService));
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.logger = logger;
        defaultBackground = xpService.GetDefaultBackgroundImage();
    }

    /// <summary>
    ///     Generates an XP image for a user based on their statistics.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <returns>A stream containing the generated image.</returns>
    public async Task<Stream> GenerateXpImageAsync(IGuildUser user)
    {
        var stats = await GetFullUserStatsAsync(user);
        var template = await GetTemplateAsync(user.Guild.Id);
        return await GenerateXpImageAsync(stats, template);
    }

    /// <summary>
    ///     Gets full user XP statistics.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <returns>The full user statistics.</returns>
    private async Task<FullUserStats> GetFullUserStatsAsync(IGuildUser user)
    {
        var xpStats = await xpService.GetUserXpStatsAsync(user.GuildId, user.Id);
        var timeOnLevel = await xpService.GetTimeOnCurrentLevelAsync(user.GuildId, user.Id);

        await using var db = await dbFactory.CreateConnectionAsync();
        var userXp = await db.GuildUserXps
            .FirstOrDefaultAsync(x => x.GuildId == user.GuildId && x.UserId == user.Id);

        if (userXp == null)
        {
            userXp = new GuildUserXp
            {
                GuildId = user.GuildId, UserId = user.Id, LastActivity = DateTime.UtcNow, LastLevelUp = DateTime.UtcNow
            };
        }

        return new FullUserStats
        {
            User = user,
            Guild = new UserLevelStats
            {
                Level = xpStats.Level, LevelXp = xpStats.LevelXp, RequiredXp = xpStats.RequiredXp
            },
            GuildRanking = xpStats.Rank,
            FullGuildStats = userXp
        };
    }

    /// <summary>
    ///     Generates an XP image for a user based on their statistics and template.
    /// </summary>
    /// <param name="stats">The user statistics.</param>
    /// <param name="template">The template to use.</param>
    /// <returns>A stream containing the generated image.</returns>
    private async Task<Stream> GenerateXpImageAsync(FullUserStats stats, Template template)
    {
        // Load the background image
        await using var xpstream = new MemoryStream();
        var xpImage = await GetXpImageAsync(stats.FullGuildStats.GuildId);
        if (xpImage is not null)
        {
            using var httpClient = httpClientFactory.CreateClient();
            var httpResponse = await httpClient.GetAsync(xpImage);
            if (httpResponse.IsSuccessStatusCode)
            {
                await httpResponse.Content.CopyToAsync(xpstream);
                xpstream.Position = 0;
            }
        }
        else
        {
            await xpstream.WriteAsync(defaultBackground.AsMemory(0, defaultBackground.Length));
            xpstream.Position = 0;
        }

        var imgData = SKData.Create(xpstream);
        var originalImg = SKBitmap.Decode(imgData);

        // The background defines the card's natural canvas size. Template dimensions are retained
        // for compatibility, but stretching or cropping the source image produces surprising cards.
        var canvasWidth = originalImg.Width;
        var canvasHeight = originalImg.Height;

        // Create a surface with template dimensions
        using var surface = SKSurface.Create(new SKImageInfo(canvasWidth, canvasHeight));
        var canvas = surface.Canvas;
        var builtInSurfaces = new Dictionary<string, SKSurface>();

        SKCanvas Layer(string id)
        {
            if (!builtInSurfaces.TryGetValue(id, out var layerSurface))
            {
                layerSurface = SKSurface.Create(new SKImageInfo(canvasWidth, canvasHeight));
                layerSurface.Canvas.Clear(SKColors.Transparent);
                builtInSurfaces[id] = layerSurface;
            }

            return layerSurface.Canvas;
        }


        // Scale the background image to fit template dimensions
        var destRect = new SKRect(0, 0, canvasWidth, canvasHeight);
        var srcRect = new SKRect(0, 0, originalImg.Width, originalImg.Height);
        canvas.DrawBitmap(originalImg, srcRect, destRect, new SKSamplingOptions(SKFilterMode.Linear));

        // Create general paint for drawing
        using var paint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Fill
        };

        // Draw the username
        if (template.TemplateUser.ShowText)
        {
            var color = SKColor.Parse(template.TemplateUser.TextColor);
            paint.Color = color;

            // Create a font for the username using modern APIs
            using var font = new SKFont
            {
                Size = template.TemplateUser.FontSize,
                Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright)
            };

            var username = stats.User.Username;
            Layer("user-text").DrawText(username, template.TemplateUser.TextX, template.TemplateUser.TextY,
                SKTextAlign.Left, font, paint);
        }

        // Draw the guild level
        if (template.TemplateGuild.ShowGuildLevel)
        {
            var color = SKColor.Parse(template.TemplateGuild.GuildLevelColor);
            paint.Color = color;

            using var font = new SKFont
            {
                Size = template.TemplateGuild.GuildLevelFontSize,
                Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright)
            };

            Layer("guild-level").DrawText(stats.Guild.Level.ToString(), template.TemplateGuild.GuildLevelX,
                template.TemplateGuild.GuildLevelY, SKTextAlign.Left, font, paint);
        }

        var guild = stats.Guild;

        // Draw the XP bar
        if (template.TemplateBar.ShowBar)
        {
            var xpPercent = guild.LevelXp / (float)guild.RequiredXp;
            DrawXpBar(xpPercent, template.TemplateBar, Layer("progress-bar"));
        }

        // Draw awarded XP
        if (stats.FullGuildStats.BonusXp != 0 && template.ShowAwarded)
        {
            var sign = stats.FullGuildStats.BonusXp > 0 ? "+ " : "";
            var color = SKColor.Parse(template.AwardedColor);
            paint.Color = color;

            using var font = new SKFont
            {
                Size = template.AwardedFontSize,
                Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright)
            };

            var text = $"({sign}{stats.FullGuildStats.BonusXp})";
            Layer("awarded").DrawText(text, template.AwardedX, template.AwardedY,
                SKTextAlign.Left, font, paint);
        }

        // Draw guild rank
        if (template.TemplateGuild.ShowGuildRank)
        {
            var color = SKColor.Parse(template.TemplateGuild.GuildRankColor);
            paint.Color = color;

            using var font = new SKFont
            {
                Size = template.TemplateGuild.GuildRankFontSize,
                Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright)
            };

            Layer("guild-rank").DrawText(stats.GuildRanking.ToString(), template.TemplateGuild.GuildRankX,
                template.TemplateGuild.GuildRankY, SKTextAlign.Left, font, paint);
        }

        // Draw time on level
        if (template.ShowTimeOnLevel)
        {
            var color = SKColor.Parse(template.TimeOnLevelColor);
            paint.Color = color;

            using var font = new SKFont
            {
                Size = template.TimeOnLevelFontSize,
                Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright)
            };

            var text = GetTimeSpent(stats.FullGuildStats.LastLevelUp);
            Layer("time-on-level").DrawText(text, template.TimeOnLevelX, template.TimeOnLevelY,
                SKTextAlign.Left, font, paint);
        }

        // Draw user avatar
        if (stats.User.GetAvatarUrl() != null && template.TemplateUser.ShowIcon)
        {
            try
            {
                var avatarUrl = GetAvatarUrl(stats.User);

                using var httpClient = httpClientFactory.CreateClient();
                var httpResponse = await httpClient.GetAsync(avatarUrl);
                if (httpResponse.IsSuccessStatusCode)
                {
                    var avatarData = await httpResponse.Content.ReadAsByteArrayAsync();
                    await using var avatarStream = new MemoryStream(avatarData);
                    var avatarImgData = SKData.Create(avatarStream);
                    var avatarImg = SKBitmap.Decode(avatarImgData);

                    // Create a new bitmap with the desired size
                    var targetSize = new SKImageInfo(
                        template.TemplateUser.IconSizeX,
                        template.TemplateUser.IconSizeY);

                    // Create sampling options for scaling the avatar
                    var avatarSamplingOptions = new SKSamplingOptions(
                        SKFilterMode.Linear,
                        SKMipmapMode.Nearest);

                    var resizedAvatar = new SKBitmap(targetSize);
                    avatarImg.ScalePixels(resizedAvatar, avatarSamplingOptions);

                    // Apply rounded corners
                    var roundedAvatar = ApplyRoundedCorners(resizedAvatar, template.TemplateUser.IconSizeX / 2);

                    // Draw the avatar onto the main image
                    Layer("user-icon").DrawImage(roundedAvatar, template.TemplateUser.IconX,
                        template.TemplateUser.IconY,
                        new SKSamplingOptions(SKFilterMode.Linear));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error drawing avatar image: {Message}", ex.Message);
            }
        }

        var defaultOrder = new[]
        {
            "user-text", "guild-level", "progress-bar", "awarded", "guild-rank", "time-on-level", "user-icon"
        };
        var builtInOrder = defaultOrder.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(template.BuiltInOrderJson))
        {
            try
            {
                var savedOrder = JsonSerializer.Deserialize<List<string>>(template.BuiltInOrderJson);
                if (savedOrder is { Count: > 0 })
                    builtInOrder = savedOrder.Where(defaultOrder.Contains).Concat(defaultOrder.Except(savedOrder))
                        .Distinct();
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Ignoring invalid built-in XP card layer order");
            }
        }

        foreach (var id in builtInOrder)
        {
            if (!builtInSurfaces.TryGetValue(id, out var layerSurface)) continue;
            using var image = layerSurface.Snapshot();
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        }

        foreach (var layerSurface in builtInSurfaces.Values) layerSurface.Dispose();

        // Custom layers intentionally render last so their z-order matches the dashboard layer stack.
        await DrawCustomElementsAsync(canvas, template.CustomElementsJson, stats);

        // Convert to Stream and return
        var finalImage = surface.Snapshot();
        var finalData = finalImage.Encode(SKEncodedImageFormat.Png, 100);
        return finalData.AsStream();
    }

    private async Task DrawCustomElementsAsync(SKCanvas canvas, string? json, FullUserStats stats)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        List<XpCardElement>? elements;
        try
        {
            elements = JsonSerializer.Deserialize<List<XpCardElement>>(json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Ignoring invalid custom XP card elements");
            return;
        }

        if (elements == null) return;
        foreach (var element in elements.Where(x => x.Visible).OrderBy(x => x.ZIndex))
        {
            canvas.Save();
            canvas.RotateDegrees(element.Rotation, element.X + element.Width / 2, element.Y + element.Height / 2);
            using var paint = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Fill
            };
            paint.Color = ParseElementColor(element.Fill).WithAlpha((byte)(255 * Math.Clamp(element.Opacity, 0, 1)));
            var rect = new SKRect(element.X, element.Y, element.X + element.Width, element.Y + element.Height);
            ApplyElementEffects(paint, element, rect);

            switch (element.Type.ToLowerInvariant())
            {
                case "ellipse":
                    canvas.DrawOval(rect, paint);
                    break;
                case "line":
                    paint.Style = SKPaintStyle.Stroke;
                    paint.StrokeWidth = Math.Max(1, element.StrokeWidth);
                    canvas.DrawLine(element.X, element.Y, element.X + element.Width, element.Y + element.Height, paint);
                    break;
                case "text":
                    using (var font = new SKFont(SKTypeface.FromFamilyName("NotoSans"), element.FontSize))
                        canvas.DrawText(ResolveElementText(element.Text, stats),
                            element.TextAlign == "center" ? element.X + element.Width / 2 :
                            element.TextAlign == "right" ? element.X + element.Width : element.X,
                            element.Y + element.FontSize,
                            element.TextAlign == "center" ? SKTextAlign.Center :
                            element.TextAlign == "right" ? SKTextAlign.Right : SKTextAlign.Left,
                            font, paint);
                    break;
                case "image":
                    await DrawCustomImageAsync(canvas, element, rect, paint);
                    break;
                case "progress":
                    DrawCustomProgress(canvas, element, rect, stats, paint);
                    break;
                default:
                    canvas.DrawRoundRect(rect, element.CornerRadius, element.CornerRadius, paint);
                    break;
            }

            if (element.StrokeWidth > 0 && element.Type is not ("line" or "text" or "image"))
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = element.StrokeWidth;
                paint.Color = ParseElementColor(element.Stroke)
                    .WithAlpha((byte)(255 * Math.Clamp(element.Opacity, 0, 1)));
                if (element.Type == "ellipse") canvas.DrawOval(rect, paint);
                else canvas.DrawRoundRect(rect, element.CornerRadius, element.CornerRadius, paint);
            }

            canvas.Restore();
        }
    }

    private static void ApplyElementEffects(SKPaint paint, XpCardElement element,
        SKRect rect)
    {
        if (!string.IsNullOrWhiteSpace(element.GradientEnd))
        {
            var radians = element.GradientAngle * MathF.PI / 180;
            var center = new SKPoint(rect.MidX, rect.MidY);
            var radius = MathF.Max(rect.Width, rect.Height) / 2;
            var vector = new SKPoint(MathF.Cos(radians) * radius, MathF.Sin(radians) * radius);
            paint.Shader = SKShader.CreateLinearGradient(center - vector, center + vector,
                [ParseElementColor(element.Fill), ParseElementColor(element.GradientEnd)], null,
                SKShaderTileMode.Clamp);
        }

        if (element.ShadowBlur > 0)
            paint.ImageFilter = SKImageFilter.CreateDropShadow(element.ShadowX, element.ShadowY, element.ShadowBlur,
                element.ShadowBlur, ParseElementColor(element.ShadowColor));
    }

    private static void DrawCustomProgress(SKCanvas canvas, XpCardElement element,
        SKRect rect, FullUserStats stats, SKPaint paint)
    {
        var progress = stats.Guild.RequiredXp == 0
            ? 1
            : Math.Clamp(stats.Guild.LevelXp / (float)stats.Guild.RequiredXp, 0, 1);
        using var track = new SKPaint
        {
            IsAntialias = true, Color = ParseElementColor(element.TrackFill)
        };
        if (element.ProgressStyle == "radial")
        {
            track.Style = paint.Style = SKPaintStyle.Stroke;
            track.StrokeWidth = paint.StrokeWidth = Math.Max(2,
                element.StrokeWidth > 0 ? element.StrokeWidth : Math.Min(rect.Width, rect.Height) / 8);
            track.StrokeCap = paint.StrokeCap = SKStrokeCap.Round;
            canvas.DrawArc(rect, -90, 360, false, track);
            canvas.DrawArc(rect, -90, 360 * progress, false, paint);
            return;
        }

        if (element.ProgressStyle == "segmented")
        {
            var count = Math.Clamp(element.Segments, 2, 50);
            var gap = Math.Max(2, rect.Width * .01f);
            var width = (rect.Width - gap * (count - 1)) / count;
            for (var i = 0; i < count; i++)
            {
                var segment = new SKRect(rect.Left + i * (width + gap), rect.Top, rect.Left + i * (width + gap) + width,
                    rect.Bottom);
                canvas.DrawRoundRect(segment, element.CornerRadius, element.CornerRadius,
                    i < Math.Ceiling(progress * count) ? paint : track);
            }

            return;
        }

        canvas.DrawRoundRect(rect, element.CornerRadius, element.CornerRadius, track);
        var filled = new SKRect(rect.Left, rect.Top, rect.Left + rect.Width * progress, rect.Bottom);
        canvas.DrawRoundRect(filled, element.CornerRadius, element.CornerRadius, paint);
    }

    private async Task DrawCustomImageAsync(SKCanvas canvas, XpCardElement element,
        SKRect rect, SKPaint paint)
    {
        if (!Uri.TryCreate(element.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        try
        {
            using var client = httpClientFactory.CreateClient();
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return;
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var bitmap = SKBitmap.Decode(stream);
            if (bitmap != null)
                canvas.DrawBitmap(bitmap, new SKRect(0, 0, bitmap.Width, bitmap.Height), rect,
                    new SKSamplingOptions(SKFilterMode.Linear), paint);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not render custom XP card image {Url}", element.Url);
        }
    }

    private static SKColor ParseElementColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return SKColors.Transparent;
        return SKColor.TryParse(value.StartsWith('#') ? value : $"#{value}", out var color)
            ? color
            : SKColors.Transparent;
    }

    private static string ResolveElementText(string text, FullUserStats stats)
    {
        var percent = stats.Guild.RequiredXp == 0 ? 100 : stats.Guild.LevelXp * 100.0 / stats.Guild.RequiredXp;
        var guildUser = stats.User as IGuildUser;
        return text
            .Replace("%xp.user%", stats.User.Username, StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.user.name%", stats.User.Username, StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.user.displayname%", guildUser?.DisplayName ?? stats.User.GlobalName ?? stats.User.Username,
                StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.user.nickname%", guildUser?.Nickname ?? stats.User.Username,
                StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.user.id%", stats.User.Id.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.level.current%", stats.Guild.Level.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.level.next%", (stats.Guild.Level + 1).ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.total%", stats.FullGuildStats.TotalXp.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.current%", stats.Guild.LevelXp.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.needed%", stats.Guild.RequiredXp.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.remaining%", Math.Max(0, stats.Guild.RequiredXp - stats.Guild.LevelXp).ToString(),
                StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.progress%", $"{percent:F1}%", StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.rank%", stats.GuildRanking.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.guild%", guildUser?.Guild.Name ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.guild.name%", guildUser?.Guild.Name ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("%xp.guild.id%", stats.FullGuildStats.GuildId.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Applies rounded corners to an image.
    /// </summary>
    /// <param name="src">The source image.</param>
    /// <param name="cornerRadius">The corner radius.</param>
    /// <returns>An image with rounded corners.</returns>
    private static SKImage ApplyRoundedCorners(SKBitmap src, float cornerRadius)
    {
        var width = src.Width;
        var height = src.Height;
        var info = new SKImageInfo(width, height);

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        using var paint = new SKPaint
        {
            IsAntialias = true, Color = SKColors.White
        };

        var rect = new SKRect(0, 0, width, height);
        using var roundRect = new SKRoundRect();
        roundRect.SetRectRadii(rect, [
            new SKPoint(cornerRadius, cornerRadius), new SKPoint(cornerRadius, cornerRadius),
            new SKPoint(cornerRadius, cornerRadius), new SKPoint(cornerRadius, cornerRadius)
        ]);

        // Clear canvas and create clipping region
        canvas.Clear(SKColors.Transparent);
        canvas.ClipRoundRect(roundRect, SKClipOperation.Intersect, true);

        // Draw the bitmap
        canvas.DrawBitmap(src, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), paint);

        return surface.Snapshot();
    }

    /// <summary>
    ///     Draws the XP progress bar.
    /// </summary>
    /// <param name="percent">The completion percentage.</param>
    /// <param name="info">The template bar information.</param>
    /// <param name="canvas">The canvas to draw on.</param>
    private static void DrawXpBar(float percent, TemplateBar info, SKCanvas canvas)
    {
        var x1 = info.BarPointAx;
        var y1 = info.BarPointAy;

        var x2 = info.BarPointBx;
        var y2 = info.BarPointBy;

        var length = info.BarLength * percent;

        float x3, x4, y3, y4;

        switch ((XpTemplateDirection)info.BarDirection)
        {
            case XpTemplateDirection.Down:
                x3 = x1;
                x4 = x2;
                y3 = y1 + length;
                y4 = y2 + length;
                break;
            case XpTemplateDirection.Up:
                x3 = x1;
                x4 = x2;
                y3 = y1 - length;
                y4 = y2 - length;
                break;
            case XpTemplateDirection.Left:
                x3 = x1 - length;
                x4 = x2 - length;
                y3 = y1;
                y4 = y2;
                break;
            default: // Right
                x3 = x1 + length;
                x4 = x2 + length;
                y3 = y1;
                y4 = y2;
                break;
        }

        var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(x1, y1);
        pathBuilder.LineTo(x3, y3);
        pathBuilder.LineTo(x4, y4);
        pathBuilder.LineTo(x2, y2);
        pathBuilder.Close();

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill
        };

        var color = SKColor.Parse(info.BarColor);
        // Fixed bug: was using Green twice instead of Blue
        paint.Color = new SKColor(color.Red, color.Green, color.Blue, (byte)info.BarTransparency);
        using var path = pathBuilder.Detach();
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    ///     Gets the template for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The template.</returns>
    public async Task<Template> GetTemplateAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var template = await db.Templates
            .LoadWithAsTable(t => t.TemplateUser)
            .LoadWithAsTable(t => t.TemplateBar)
            .LoadWithAsTable(t => t.TemplateClub)
            .LoadWithAsTable(t => t.TemplateGuild)
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (template != null)
            return template;

        // Create related entities with default values
        var templateBar = new TemplateBar
        {
            // Default values are set in the class properties
            BarColor = "FF000000",
            BarPointAx = 319,
            BarPointAy = 119,
            BarPointBx = 284,
            BarPointBy = 250,
            BarLength = 452,
            BarTransparency = 90,
            BarDirection = (int)XpTemplateDirection.Right,
            ShowBar = true
        };

        var templateClub = new TemplateClub
        {
            // Default values are set in the class properties
            ClubIconX = 717,
            ClubIconY = 37,
            ClubIconSizeX = 49,
            ClubIconSizeY = 49,
            ShowClubIcon = true,
            ClubNameColor = "FF000000",
            ClubNameFontSize = 32,
            ClubNameX = 649,
            ClubNameY = 50,
            ShowClubName = true
        };

        var templateGuild = new TemplateGuild
        {
            // Default values are set in the class properties
            GuildLevelColor = "FF000000",
            GuildLevelFontSize = 27,
            GuildLevelX = 42,
            GuildLevelY = 206,
            ShowGuildLevel = true,
            GuildRankColor = "FF000000",
            GuildRankFontSize = 25,
            GuildRankX = 148,
            GuildRankY = 211,
            ShowGuildRank = true
        };

        var templateUser = new TemplateUser
        {
            // Default values are set in the class properties
            TextColor = "FF000000",
            FontSize = 50,
            TextX = 120,
            TextY = 70,
            ShowText = true,
            IconX = 27,
            IconY = 24,
            IconSizeX = 73,
            IconSizeY = 74,
            ShowIcon = true
        };

        // Important: Insert the related entities FIRST to get their IDs
        templateBar.Id = await db.InsertWithInt32IdentityAsync(templateBar);
        templateClub.Id = await db.InsertWithInt32IdentityAsync(templateClub);
        templateGuild.Id = await db.InsertWithInt32IdentityAsync(templateGuild);
        templateUser.Id = await db.InsertWithInt32IdentityAsync(templateUser);

        // Now create the Template with proper foreign key IDs
        var toAdd = new Template
        {
            GuildId = guildId,

            // Set the default values for Template
            OutputSizeX = 797,
            OutputSizeY = 279,
            TimeOnLevelFormat = "{0}d{1}h{2}m",
            TimeOnLevelX = 50,
            TimeOnLevelY = 204,
            TimeOnLevelFontSize = 20,
            TimeOnLevelColor = "FF000000",
            ShowTimeOnLevel = true,
            AwardedX = 445,
            AwardedY = 347,
            AwardedFontSize = 25,
            AwardedColor = "ffffffff",
            ShowAwarded = false,

            // Set the foreign key IDs
            TemplateBarId = templateBar.Id,
            TemplateClubId = templateClub.Id,
            TemplateGuildId = templateGuild.Id,
            TemplateUserId = templateUser.Id,

            // Set the navigation properties
            TemplateBar = templateBar,
            TemplateClub = templateClub,
            TemplateGuild = templateGuild,
            TemplateUser = templateUser
        };

        // Finally, insert the Template with the correct foreign keys
        await db.InsertAsync(toAdd);

        return await db.Templates
            .LoadWithAsTable(t => t.TemplateUser)
            .LoadWithAsTable(t => t.TemplateBar)
            .LoadWithAsTable(t => t.TemplateClub)
            .LoadWithAsTable(t => t.TemplateGuild)
            .FirstOrDefaultAsync(x => x.GuildId == guildId);
    }

    /// <summary>
    ///     Gets the URL for a custom XP background image.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The image URL or null.</returns>
    private async Task<string?> GetXpImageAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var settings = await db.GuildXpSettings
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings != null && !string.IsNullOrEmpty(settings.CustomXpImageUrl))
        {
            return settings.CustomXpImageUrl;
        }

        return null;
    }

    /// <summary>
    ///     Gets the avatar URL for a user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The avatar URL.</returns>
    private static string GetAvatarUrl(IUser user)
    {
        return user.GetAvatarUrl(ImageFormat.Png, 256) ?? user.GetDefaultAvatarUrl();
    }

    /// <summary>
    ///     Gets a formatted string for time spent on a level.
    /// </summary>
    /// <param name="time">The time to format.</param>
    /// <returns>A formatted time string.</returns>
    private static string GetTimeSpent(DateTime time)
    {
        var offset = DateTime.UtcNow - time;
        return $"{offset.Humanize()} ago";
    }
}

/// <summary>
///     Represents the full statistics for a user in a guild.
/// </summary>
public class FullUserStats
{
    /// <summary>
    ///     Gets or sets the user information.
    /// </summary>
    public IUser User { get; set; }

    /// <summary>
    ///     Gets or sets the guild level statistics.
    /// </summary>
    public UserLevelStats Guild { get; set; }

    /// <summary>
    ///     Gets or sets the user's ranking in the guild.
    /// </summary>
    public int GuildRanking { get; set; }

    /// <summary>
    ///     Gets or sets the full guild statistics.
    /// </summary>
    public GuildUserXp FullGuildStats { get; set; }
}

/// <summary>
///     Represents a user's level statistics.
/// </summary>
public class UserLevelStats
{
    /// <summary>
    ///     Gets or sets the user's level.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    ///     Gets or sets the XP in the current level.
    /// </summary>
    public long LevelXp { get; set; }

    /// <summary>
    ///     Gets or sets the XP required for the next level.
    /// </summary>
    public long RequiredXp { get; set; }
}