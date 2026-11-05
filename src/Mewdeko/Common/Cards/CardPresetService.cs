using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Mewdeko.Services.Settings;
using SkiaSharp;

namespace Mewdeko.Common.Cards;

/// <summary>
///     Serves the shared background presets: the catalog hosted on the CDN, and the images in it, which cost servers
///     no storage. Both are cached, and nothing here throws; a preset that can't be loaded is just missing.
/// </summary>
public sealed class CardPresetService : INService
{
    /// <summary>
    ///     The folder the catalog and images are served from unless the bot config says otherwise.
    /// </summary>
    public const string DefaultBaseUrl = "https://cdn.mewdeko.tech/card-backgrounds";

    /// <summary>
    ///     The catalog file name inside the folder.
    /// </summary>
    public const string CatalogFile = "catalog.json";

    private const int MaxImageBytes = 8 * 1024 * 1024;
    private const long MaxMemoryBytes = 48L * 1024 * 1024;
    private const long MaxDiskBytes = 256L * 1024 * 1024;
    private const string DiskFolder = "data/card_presets";

    private static readonly TimeSpan CatalogLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan FailureRetry = TimeSpan.FromMinutes(2);

    private readonly Func<string> baseUrl;
    private readonly string diskFolder;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<CardPresetService> logger;
    private readonly Dictionary<string, MemoryEntry> memory = new();
    private readonly object memoryLock = new();
    private readonly SemaphoreSlim catalogLock = new(1, 1);
    private readonly SemaphoreSlim diskLock = new(1, 1);

    private CardPresetCatalog? catalog;
    private string catalogBase = "";
    private DateTime catalogExpires = DateTime.MinValue;
    private long memoryBytes;
    private long tick;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CardPresetService" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The bot config, for the preset folder URL.</param>
    /// <param name="logger">The logger instance.</param>
    public CardPresetService(IHttpClientFactory httpClientFactory, BotConfigService config,
        ILogger<CardPresetService> logger)
        : this(httpClientFactory, () => config.Data.CardPresetBaseUrl, DiskFolder, logger)
    {
    }

    private CardPresetService(IHttpClientFactory httpClientFactory, Func<string> baseUrl, string diskFolder,
        ILogger<CardPresetService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.baseUrl = baseUrl;
        this.diskFolder = diskFolder;
        this.logger = logger;
    }

    /// <summary>
    ///     Creates a service that reads from a given folder URL and caches in a given folder, without the bot config.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="baseUrl">The folder URL.</param>
    /// <param name="diskFolder">Where images are cached on disk.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>The service.</returns>
    public static CardPresetService Create(IHttpClientFactory httpClientFactory, string baseUrl, string diskFolder,
        ILogger<CardPresetService> logger)
    {
        return new CardPresetService(httpClientFactory, () => baseUrl, diskFolder, logger);
    }

    /// <summary>
    ///     The folder URL presets are served from.
    /// </summary>
    public string BaseUrl
    {
        get
        {
            var value = (baseUrl() ?? "").Trim().TrimEnd('/');
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   uri.Scheme is "https" or "http"
                ? value
                : DefaultBaseUrl;
        }
    }

    /// <summary>
    ///     Gets the catalog. It is fetched again after 30 minutes, and when that fails the last good copy is kept.
    /// </summary>
    /// <returns>The catalog, empty when it has never been fetched.</returns>
    public async Task<CardPresetCatalog> GetCatalogAsync()
    {
        var current = BaseUrl;
        if (catalog is { } cached && catalogBase == current && DateTime.UtcNow < catalogExpires)
            return cached;

        await catalogLock.WaitAsync();
        try
        {
            if (catalog is { } fresh && catalogBase == current && DateTime.UtcNow < catalogExpires)
                return fresh;

            var loaded = await FetchCatalogAsync(current);
            if (loaded is not null)
            {
                catalog = loaded;
                catalogBase = current;
                catalogExpires = DateTime.UtcNow + CatalogLifetime;
            }
            else
            {
                if (catalogBase != current)
                    catalog = null;
                catalogBase = current;
                catalogExpires = DateTime.UtcNow + FailureRetry;
            }

            return catalog ?? CardPresetCatalog.Empty;
        }
        finally
        {
            catalogLock.Release();
        }
    }

    /// <summary>
    ///     Finds a preset by id.
    /// </summary>
    /// <param name="id">The preset id.</param>
    /// <returns>The preset, or null when it isn't in the catalog.</returns>
    public async Task<CardPreset?> FindAsync(string id)
    {
        return (await GetCatalogAsync()).Find(id);
    }

    /// <summary>
    ///     Whether a <c>preset:id</c> reference points at a preset in the catalog.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>True when the preset exists.</returns>
    public async Task<bool> ExistsAsync(string? reference)
    {
        return CardPresets.TryParse(reference, out var id) && await FindAsync(id) is not null;
    }

    /// <summary>
    ///     Gets the bytes of a preset's image from the memory cache, the disk cache or the CDN.
    /// </summary>
    /// <param name="reference">A <c>preset:id</c> reference.</param>
    /// <returns>The image bytes, or null when the preset is unknown or its image can't be loaded.</returns>
    public async Task<byte[]?> LoadBytesAsync(string reference)
    {
        try
        {
            if (!CardPresets.TryParse(reference, out var id) || await FindAsync(id) is not { } preset)
                return null;

            var key = $"{preset.Id}|{preset.Image}";
            if (FromMemory(key) is { } hit)
                return hit;

            var path = DiskPath(preset);
            if (await ReadDiskAsync(path) is { } onDisk)
            {
                ToMemory(key, onDisk);
                return onDisk;
            }

            if (await DownloadAsync(CardPresets.Url(BaseUrl, preset.Image)) is not { } bytes)
                return null;

            ToMemory(key, bytes);
            await WriteDiskAsync(path, bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load card preset {Reference}", reference);
            return null;
        }
    }

    /// <summary>
    ///     Loads a preset's image.
    /// </summary>
    /// <param name="reference">A <c>preset:id</c> reference.</param>
    /// <returns>The image, or null when the preset is unknown or its image can't be loaded.</returns>
    public async Task<SKBitmap?> LoadBitmapAsync(string reference)
    {
        return await LoadBytesAsync(reference) is { } bytes ? SKBitmap.Decode(bytes) : null;
    }

    /// <summary>
    ///     Whether bytes are an image that can be decoded.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>True when they are.</returns>
    public static bool IsImage(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
            return false;
        using var stream = new MemoryStream(bytes);
        using var codec = SKCodec.Create(stream);
        return codec is { Info: { Width: > 0, Height: > 0 } };
    }

    private async Task<CardPresetCatalog?> FetchCatalogAsync(string folder)
    {
        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            var json = await http.GetStringAsync(CardPresets.Url(folder, CatalogFile));
            var parsed = CardPresets.ParseCatalog(json);
            if (parsed is null)
                logger.LogWarning("The card preset catalog at {Folder} could not be read", folder);
            return parsed;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch the card preset catalog from {Folder}", folder);
            return null;
        }
    }

    private async Task<byte[]?> DownloadAsync(string url)
    {
        using var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(20);
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxImageBytes)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxImageBytes)
                return null;
        }

        var bytes = buffer.ToArray();
        return IsImage(bytes) ? bytes : null;
    }

    private byte[]? FromMemory(string key)
    {
        lock (memoryLock)
        {
            if (!memory.TryGetValue(key, out var entry))
                return null;
            entry.Used = ++tick;
            return entry.Data;
        }
    }

    private void ToMemory(string key, byte[] data)
    {
        if (data.Length > MaxMemoryBytes)
            return;

        lock (memoryLock)
        {
            if (memory.Remove(key, out var old))
                memoryBytes -= old.Data.Length;
            memory[key] = new MemoryEntry(data)
            {
                Used = ++tick
            };
            memoryBytes += data.Length;

            while (memoryBytes > MaxMemoryBytes && memory.Count > 1)
            {
                var oldest = memory.MinBy(e => e.Value.Used).Key;
                memoryBytes -= memory[oldest].Data.Length;
                memory.Remove(oldest);
            }
        }
    }

    private string DiskPath(CardPreset preset)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(preset.Image)))[..8].ToLowerInvariant();
        return Path.Combine(diskFolder, $"{preset.Id}-{hash}.img");
    }

    private static async Task<byte[]?> ReadDiskAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            var bytes = await File.ReadAllBytesAsync(path);
            if (IsImage(bytes))
            {
                File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
                return bytes;
            }

            File.Delete(path);
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task WriteDiskAsync(string path, byte[] bytes)
    {
        await diskLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(diskFolder);
            await File.WriteAllBytesAsync(path, bytes);

            var files = new DirectoryInfo(diskFolder).GetFiles("*.img").OrderBy(f => f.LastAccessTimeUtc).ToList();
            var total = files.Sum(f => f.Length);
            foreach (var file in files)
            {
                if (total <= MaxDiskBytes)
                    break;
                if (file.FullName == Path.GetFullPath(path))
                    continue;
                total -= file.Length;
                file.Delete();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not cache a card preset on disk");
        }
        finally
        {
            diskLock.Release();
        }
    }

    private sealed class MemoryEntry(byte[] data)
    {
        public byte[] Data { get; } = data;

        public long Used { get; set; }
    }
}
