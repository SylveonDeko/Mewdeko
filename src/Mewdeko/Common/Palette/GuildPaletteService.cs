using System.Net.Http;
using SkiaSharp;

namespace Mewdeko.Common.Palette;

/// <summary>
///     The dashboard palette of each guild, derived from its icon with the same pipeline the web dashboard runs, so
///     images the bot draws carry the colors the dashboard shows for that server.
/// </summary>
/// <param name="httpClientFactory">HTTP clients, for icons.</param>
/// <param name="logger">Logger instance.</param>
public sealed class GuildPaletteService(IHttpClientFactory httpClientFactory, ILogger<GuildPaletteService> logger)
    : INService
{
    private const int CacheLimit = 500;

    private readonly ConcurrentDictionary<string, DashboardPalette> cache = new();

    /// <summary>
    ///     The palette for a guild's icon, or the dashboard's default palette when it has none.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <returns>The palette.</returns>
    public Task<DashboardPalette> GetAsync(IGuild guild)
    {
        return GetAsync(guild.IconUrl);
    }

    /// <summary>
    ///     The palette for an icon URL. Discord CDN URLs are fetched without a size parameter, the natural size image the
    ///     dashboard quantizes.
    /// </summary>
    /// <param name="iconUrl">The icon URL.</param>
    /// <returns>The palette.</returns>
    public async Task<DashboardPalette> GetAsync(string? iconUrl)
    {
        if (string.IsNullOrEmpty(iconUrl))
            return DashboardPalette.Default;

        var url = iconUrl.StartsWith("https://cdn.discordapp.com/") ? iconUrl.Split('?')[0] : iconUrl;
        if (cache.TryGetValue(url, out var cached))
            return cached;

        var palette = DashboardPalette.Default;
        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(5);
            using var bitmap = SKBitmap.Decode(await http.GetByteArrayAsync(url));
            if (bitmap is null)
                return palette;
            palette = DashboardColorStore.Extract(bitmap);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not derive a palette from {IconUrl}", url);
            return palette;
        }

        if (cache.Count >= CacheLimit)
            cache.Clear();
        cache[url] = palette;
        return palette;
    }
}
