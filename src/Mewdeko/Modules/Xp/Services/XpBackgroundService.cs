using System.IO;
using System.Net.Http;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Cards;
using SkiaSharp;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     The result of setting a rank card background.
/// </summary>
public enum XpBackgroundResult
{
    /// <summary>
    ///     The background was saved.
    /// </summary>
    Saved,

    /// <summary>
    ///     The image could not be downloaded.
    /// </summary>
    DownloadFailed,

    /// <summary>
    ///     The file is larger than the limit.
    /// </summary>
    TooLarge,

    /// <summary>
    ///     The file is not an image.
    /// </summary>
    NotAnImage,

    /// <summary>
    ///     The preset is not in the catalog.
    /// </summary>
    UnknownPreset
}

/// <summary>
///     Stores and loads rank card backgrounds, for single users and for whole servers.
/// </summary>
/// <param name="dbFactory">The database connection factory.</param>
/// <param name="httpFactory">The HTTP client factory.</param>
/// <param name="xpService">The XP service.</param>
/// <param name="presets">The shared background presets.</param>
/// <param name="logger">The logger instance.</param>
public class XpBackgroundService(
    IDataConnectionFactory dbFactory,
    IHttpClientFactory httpFactory,
    XpService xpService,
    CardPresetService presets,
    ILogger<XpBackgroundService> logger) : INService
{
    private const int MaxFittedPresets = 24;

    private readonly ConcurrentDictionary<string, (byte[] Source, byte[] Fitted)> fitted = new();

    /// <summary>
    ///     The prefix marking a stored value as an uploaded file rather than a URL.
    /// </summary>
    public const string LocalPrefix = "local:";

    private const int MaxBytes = 8 * 1024 * 1024;
    private const string Folder = "data/xp_backgrounds";

    private readonly Lazy<(int Width, int Height)> cardSize = new(() =>
    {
        using var bitmap = SKBitmap.Decode(xpService.GetDefaultBackgroundImage());
        return (bitmap.Width, bitmap.Height);
    });

    /// <summary>
    ///     Gets the image a user chose for their own card.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The stored value, or null when the user has none.</returns>
    public async Task<string?> GetUserBackgroundAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.UserXpBackgrounds.Where(x => x.UserId == userId).Select(x => x.Image)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Sets a user's own card background.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="source">An image URL or attachment URL.</param>
    /// <returns>What happened.</returns>
    public async Task<XpBackgroundResult> SetUserBackgroundAsync(ulong userId, string source)
    {
        var (result, stored) = await ResolveAsync($"user-{userId}", source);
        if (result != XpBackgroundResult.Saved)
            return result;

        await using var db = await dbFactory.CreateConnectionAsync();
        var updated = await db.UserXpBackgrounds.Where(x => x.UserId == userId)
            .Set(x => x.Image, stored)
            .UpdateAsync();
        if (updated == 0)
            await db.InsertAsync(new UserXpBackground
            {
                UserId = userId, Image = stored, DateAdded = DateTime.UtcNow
            });
        return result;
    }

    /// <summary>
    ///     Removes a user's own card background.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>True when the user had one.</returns>
    public async Task<bool> ClearUserBackgroundAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var removed = await db.UserXpBackgrounds.Where(x => x.UserId == userId).DeleteAsync();
        DeleteFile($"user-{userId}.png");
        return removed > 0;
    }

    /// <summary>
    ///     Sets the card background used for the whole server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="source">An image URL or attachment URL.</param>
    /// <returns>What happened.</returns>
    public async Task<XpBackgroundResult> SetGuildBackgroundAsync(ulong guildId, string source)
    {
        var (result, stored) = await ResolveAsync($"guild-{guildId}", source);
        if (result == XpBackgroundResult.Saved)
            await xpService.UpdateGuildXpSettingsAsync(guildId, x => x.CustomXpImageUrl = stored);
        return result;
    }

    /// <summary>
    ///     Removes the server-wide card background.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>True when the server had one.</returns>
    public async Task<bool> ClearGuildBackgroundAsync(ulong guildId)
    {
        var settings = await xpService.GetGuildXpSettingsAsync(guildId);
        var had = !string.IsNullOrWhiteSpace(settings.CustomXpImageUrl);
        await xpService.UpdateGuildXpSettingsAsync(guildId, x => x.CustomXpImageUrl = "");
        DeleteFile($"guild-{guildId}.png");
        return had;
    }

    /// <summary>
    ///     Loads the bytes of a stored background.
    /// </summary>
    /// <param name="value">A stored value: an uploaded file marked with the local prefix, or a URL.</param>
    /// <returns>The image bytes, or null when they cannot be loaded.</returns>
    public async Task<byte[]?> LoadAsync(string value)
    {
        try
        {
            if (CardPresets.TryParse(value, out _))
                return await LoadPresetAsync(value);

            if (value.StartsWith(LocalPrefix, StringComparison.Ordinal))
            {
                var path = PathFor(value[LocalPrefix.Length..]);
                return path is not null && File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
            }

            using var http = httpFactory.CreateClient();
            using var response = await http.GetAsync(value);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load rank card background {Value}", value);
            return null;
        }
    }

    private async Task<(XpBackgroundResult Result, string Stored)> ResolveAsync(string key, string source)
    {
        if (source.Trim().StartsWith(CardPresets.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            if (!await presets.ExistsAsync(source))
                return (XpBackgroundResult.UnknownPreset, "");

            CardPresets.TryParse(source, out var id);
            DeleteFile($"{key}.png");
            return (XpBackgroundResult.Saved, CardPresets.Reference(id));
        }

        var (result, name) = await StoreAsync(key, source);
        return (result, LocalPrefix + name);
    }

    private async Task<(XpBackgroundResult, string)> StoreAsync(string key, string source)
    {
        var name = $"{key}.png";
        byte[] raw;
        try
        {
            using var http = httpFactory.CreateClient();
            using var response = await http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return (XpBackgroundResult.DownloadFailed, name);
            if (response.Content.Headers.ContentLength > MaxBytes)
                return (XpBackgroundResult.TooLarge, name);

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes)
                    return (XpBackgroundResult.TooLarge, name);
            }

            raw = buffer.ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException
                                       or UriFormatException)
        {
            return (XpBackgroundResult.DownloadFailed, name);
        }

        using var bitmap = SKBitmap.Decode(raw);
        if (bitmap is null)
            return (XpBackgroundResult.NotAnImage, name);

        Directory.CreateDirectory(Folder);
        await File.WriteAllBytesAsync(Path.Combine(Folder, name), FitToCard(bitmap));
        return (XpBackgroundResult.Saved, name);
    }

    private byte[] FitToCard(SKBitmap bitmap)
    {
        var (width, height) = cardSize.Value;
        var scale = Math.Max((float)width / bitmap.Width, (float)height / bitmap.Height);
        var drawWidth = bitmap.Width * scale;
        var drawHeight = bitmap.Height * scale;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);
        using var image = SKImage.FromBitmap(bitmap);
        using var paint = new SKPaint
        {
            IsAntialias = true
        };
        canvas.DrawImage(image,
            new SKRect((width - drawWidth) / 2, (height - drawHeight) / 2, (width + drawWidth) / 2,
                (height + drawHeight) / 2), new SKSamplingOptions(SKCubicResampler.Mitchell), paint);

        using var encoded = surface.Snapshot().Encode(SKEncodedImageFormat.Png, 90);
        return encoded.ToArray();
    }

    private async Task<byte[]?> LoadPresetAsync(string reference)
    {
        var raw = await presets.LoadBytesAsync(reference);
        if (raw is null)
            return null;
        if (fitted.TryGetValue(reference, out var cached) && ReferenceEquals(cached.Source, raw))
            return cached.Fitted;

        using var bitmap = SKBitmap.Decode(raw);
        if (bitmap is null)
            return null;

        var result = FitToCard(bitmap);
        if (fitted.Count >= MaxFittedPresets)
            fitted.Clear();
        fitted[reference] = (raw, result);
        return result;
    }

    private static string? PathFor(string name)
    {
        if (name != Path.GetFileName(name))
            return null;
        return Path.Combine(Folder, name);
    }

    private static void DeleteFile(string name)
    {
        var path = PathFor(name);
        if (path is not null && File.Exists(path))
            File.Delete(path);
    }
}
