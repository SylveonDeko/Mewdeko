using System.Net.Http;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Services.Impl;
using SkiaSharp;

namespace Mewdeko.Modules.Achievements.Services;

/// <summary>
///     Images servers upload for achievement icons, and loading any image icon for drawing.
/// </summary>
public sealed class AchievementIconService : INService
{
    /// <summary>
    ///     Largest upload accepted before it is shrunk.
    /// </summary>
    public const int MaxUploadBytes = 4 * 1024 * 1024;

    /// <summary>
    ///     Most uploads a server keeps.
    /// </summary>
    public const int MaxUploads = 100;

    /// <summary>
    ///     Longest side of a stored icon, in pixels.
    /// </summary>
    public const int IconSize = 256;

    /// <summary>
    ///     Upload kind of icons.
    /// </summary>
    public const short IconKind = 0;

    /// <summary>
    ///     Upload kind of card designer images: backgrounds and image elements.
    /// </summary>
    public const short CardKind = 1;

    /// <summary>
    ///     Most card images a server keeps.
    /// </summary>
    public const int MaxCardUploads = 30;

    /// <summary>
    ///     Longest side of a stored card image, in pixels.
    /// </summary>
    public const int CardImageSize = 1600;

    private const string CdnFolder = "achievements";
    private const int MaxFetchBytes = 8 * 1024 * 1024;

    private readonly AchievementService achievements;
    private readonly CdnStorageService cdn;
    private readonly IBotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<AchievementIconService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AchievementIconService" /> class.
    /// </summary>
    /// <param name="dbFactory">Database connections.</param>
    /// <param name="cdn">The CDN, for public URLs.</param>
    /// <param name="creds">Credentials, for the dashboard URL fallback.</param>
    /// <param name="achievements">The achievement service, to refresh catalogs.</param>
    /// <param name="httpClientFactory">HTTP clients, for linked images.</param>
    /// <param name="logger">Logger instance.</param>
    public AchievementIconService(IDataConnectionFactory dbFactory, CdnStorageService cdn, IBotCredentials creds,
        AchievementService achievements, IHttpClientFactory httpClientFactory, ILogger<AchievementIconService> logger)
    {
        this.dbFactory = dbFactory;
        this.cdn = cdn;
        this.creds = creds;
        this.achievements = achievements;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     Stores an image, shrunk to fit <see cref="IconSize" /> for icons or <see cref="CardImageSize" /> for card
    ///     images, and saved as PNG.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">Who uploaded it.</param>
    /// <param name="bytes">The image.</param>
    /// <param name="kind"><see cref="IconKind" /> or <see cref="CardKind" />.</param>
    /// <returns>The stored upload.</returns>
    public async Task<AchievementResult<AchievementIconUpload>> UploadAsync(ulong guildId, ulong userId, byte[] bytes,
        short kind = IconKind)
    {
        if (bytes.Length == 0 || bytes.Length > MaxUploadBytes)
            return AchievementResult<AchievementIconUpload>.Fail(AchievementError.IconInvalid);

        var png = Normalize(bytes, kind == CardKind ? CardImageSize : IconSize);
        if (png is null)
            return AchievementResult<AchievementIconUpload>.Fail(AchievementError.IconInvalid);

        await using var db = await dbFactory.CreateConnectionAsync();
        var limit = kind == CardKind ? MaxCardUploads : MaxUploads;
        if (await db.AchievementIconUploads.CountAsync(x => x.GuildId == guildId && x.Kind == kind) >= limit)
            return AchievementResult<AchievementIconUpload>.Fail(kind == CardKind
                ? AchievementError.TooManyCardImages
                : AchievementError.TooManyUploads);

        var row = new AchievementIconUpload
        {
            GuildId = guildId,
            Kind = kind,
            Data = png,
            UploadedBy = userId,
            DateAdded = DateTime.UtcNow
        };
        row.Id = await db.InsertWithInt32IdentityAsync(row);

        row.PublicUrl = await PublishAsync(guildId, row.Id, png);
        if (row.PublicUrl is not null)
        {
            await db.AchievementIconUploads.Where(x => x.Id == row.Id)
                .Set(x => x.PublicUrl, row.PublicUrl)
                .UpdateAsync();
        }

        achievements.RefreshCatalog(guildId);
        return AchievementResult<AchievementIconUpload>.Ok(row);
    }

    /// <summary>
    ///     Deletes an upload. Achievements and categories using it go back to their default icon.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The upload ID.</param>
    /// <returns>True when it existed.</returns>
    public async Task<bool> DeleteAsync(ulong guildId, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var deleted = await db.AchievementIconUploads.Where(x => x.GuildId == guildId && x.Id == id).DeleteAsync();
        if (deleted == 0)
            return false;

        var stored = AchievementIcons.UploadPrefix + id;
        await db.CustomAchievements.Where(x => x.GuildId == guildId && x.Icon == stored)
            .Set(x => x.Icon, (string?)null).UpdateAsync();
        await db.AchievementOverrides.Where(x => x.GuildId == guildId && x.Icon == stored)
            .Set(x => x.Icon, (string?)null).UpdateAsync();
        await db.AchievementCategories.Where(x => x.GuildId == guildId && x.Icon == stored)
            .Set(x => x.Icon, (string?)null).UpdateAsync();

        if (cdn.IsConfigured)
            await cdn.DeleteAsync(CdnFolder, FileName(guildId, id));

        await achievements.DropCardUploadAsync(guildId, id);

        achievements.RefreshCatalog(guildId);
        return true;
    }

    /// <summary>
    ///     The stored bytes of an upload.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The upload ID.</param>
    /// <returns>The PNG, or null.</returns>
    public async Task<byte[]?> GetBytesAsync(ulong guildId, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.AchievementIconUploads
            .Where(x => x.GuildId == guildId && x.Id == id)
            .Select(x => x.Data)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Loads the image of an image icon for drawing.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="icon">The icon.</param>
    /// <returns>The image, or null for glyphs and anything that fails to load.</returns>
    public async Task<SKBitmap?> LoadImageAsync(ulong guildId, AchievementIconRef icon)
    {
        try
        {
            byte[]? bytes = icon.Kind switch
            {
                AchievementIconKind.Upload when AchievementIcons.UploadId(icon) is { } id => await GetBytesAsync(guildId, id),
                AchievementIconKind.Url => await FetchAsync(icon.Value),
                AchievementIconKind.Emoji when AchievementIcons.EmojiUrl(icon) is { } url => await FetchAsync(url),
                _ => null
            };
            return bytes is null ? null : SKBitmap.Decode(bytes);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not load an achievement icon image");
            return null;
        }
    }

    private async Task<byte[]?> FetchAsync(string url)
    {
        using var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(5);
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxFetchBytes)
            return null;
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return bytes.Length > MaxFetchBytes ? null : bytes;
    }

    private async Task<string?> PublishAsync(ulong guildId, int id, byte[] png)
    {
        if (cdn.IsConfigured)
        {
            var saved = await cdn.SaveAsync(CdnFolder, FileName(guildId, id), png);
            if (saved is not null)
                return saved;
        }

        return string.IsNullOrWhiteSpace(creds.DashboardUrl)
            ? null
            : $"{creds.DashboardUrl.TrimEnd('/')}/cdn/achievement/{guildId}/{id}.png";
    }

    private static string FileName(ulong guildId, int id)
    {
        return $"icon-{guildId}-{id}.png";
    }

    private static byte[]? Normalize(byte[] bytes, int maxSide)
    {
        using var source = SKBitmap.Decode(bytes);
        if (source is null || source.Width == 0 || source.Height == 0)
            return null;

        var scale = Math.Min(1f, (float)maxSide / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var resized = source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKCubicResampler.Mitchell));
        if (resized is null)
            return null;
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
