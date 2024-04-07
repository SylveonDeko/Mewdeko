using System.IO;
using System.Net.Http;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.Yml;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Services.Impl;

/// <summary>
/// Service for caching images in Redis.
/// </summary>
public sealed class RedisImagesCache : IImageCache, IReadyExecutor, INService
{
    private readonly ConnectionMultiplexer con;
    private readonly HttpClient http;
    private readonly string imagesPath;
    private readonly string redisKey;

    private IDatabase Db => con.GetDatabase();

    private const string BasePath = "data/";
    private const string CardsPath = "data/images/cards";

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisImagesCache"/> class.
    /// </summary>
    /// <param name="con">The Redis connection multiplexer.</param>
    /// <param name="creds">The bot credentials.</param>
    public RedisImagesCache(ConnectionMultiplexer con, IBotCredentials creds)
    {
        this.con = con;
        http = new HttpClient();
        imagesPath = Path.Combine(BasePath, "images.yml");
        redisKey = creds.RedisKey();
        ImageUrls = Yaml.Deserializer.Deserialize<ImageUrls>(File.ReadAllText(imagesPath));
    }

    /// <summary>
    /// Image URLs.
    /// </summary>
    public ImageUrls ImageUrls { get; private set; }

    /// <summary>
    /// Enum representing the keys for the images. Used to retrieve images from the cache.
    /// </summary>
    public enum ImageKeys
    {
        /// <summary>
        /// Coin heads image key.
        /// </summary>
        CoinHeads,

        /// <summary>
        /// Coin tails image key.
        /// </summary>
        CoinTails,

        /// <summary>
        /// Dice image key.
        /// </summary>
        Dice,

        /// <summary>
        /// Slot machine background image key.
        /// </summary>
        SlotBg,

        /// <summary>
        /// Slot machine emojis image key.
        /// </summary>
        SlotEmojis,

        /// <summary>
        /// Currency image key.
        /// </summary>
        Currency,

        /// <summary>
        /// Rip overlay image key.
        /// </summary>
        RipOverlay,

        /// <summary>
        /// Rip background image key.
        /// </summary>
        RipBg,

        /// <summary>
        /// XP background image key.
        /// </summary>
        XpBg
    }

    /// <summary>
    /// Retrieves a list of byte arrays representing images of coin heads.
    /// </summary>
    public IReadOnlyList<byte[]> Heads => GetByteArrayData(ImageKeys.CoinHeads);

    /// <summary>
    /// Retrieves a list of byte arrays representing images of coin tails.
    /// </summary>
    public IReadOnlyList<byte[]> Tails => GetByteArrayData(ImageKeys.CoinTails);

    /// <summary>
    /// Retrieves a list of byte arrays representing dice images.
    /// </summary>
    public IReadOnlyList<byte[]> Dice => GetByteArrayData(ImageKeys.Dice);

    /// <summary>
    /// Retrieves a list of byte arrays representing slot machine emojis.
    /// </summary>
    public IReadOnlyList<byte[]> SlotEmojis => GetByteArrayData(ImageKeys.SlotEmojis);

    /// <summary>
    /// Retrieves a list of byte arrays representing currency symbols.
    /// </summary>
    public IReadOnlyList<byte[]> Currency => GetByteArrayData(ImageKeys.Currency);

    /// <summary>
    /// Retrieves a byte array representing the background image for the slot machine.
    /// </summary>
    public byte[] SlotBackground => GetByteData(ImageKeys.SlotBg);

    /// <summary>
    /// Retrieves a byte array representing the background image for the XP system.
    /// </summary>
    public byte[] XpBackground => GetByteData(ImageKeys.XpBg);

    /// <summary>
    /// Retrieves a byte array representing the RIP background image.
    /// </summary>
    public byte[] Rip => GetByteData(ImageKeys.RipBg);

    /// <summary>
    /// Retrieves a byte array representing the RIP overlay image.
    /// </summary>
    public byte[] RipOverlay => GetByteData(ImageKeys.RipOverlay);

    /// <summary>
    /// Retrieves a card image byte array based on the provided key.
    /// </summary>
    /// <param name="key">The key of the card image.</param>
    /// <returns>The byte array representing the card image.</returns>
    public byte[] GetCard(string key) =>
        // since cards are always local for now, don't cache them
        File.ReadAllBytes(Path.Join(CardsPath, $"{key}.jpg"));

    /// <summary>
    /// Called when the bot is ready. Checks if all required keys exist in the cache and reloads them if necessary.
    /// </summary>
    public async Task OnReadyAsync()
    {
        if (await AllKeysExist().ConfigureAwait(false))
            return;

        await Reload().ConfigureAwait(false);
    }

    /// <summary>
    /// Reloads all image data from the specified sources.
    /// </summary>
    public async Task Reload()
    {
        ImageUrls = Yaml.Deserializer.Deserialize<ImageUrls>(await File.ReadAllTextAsync(imagesPath)
            .ConfigureAwait(false));
        foreach (var key in GetAllKeys())
        {
            switch (key)
            {
                case ImageKeys.CoinHeads:
                    await Load(key, ImageUrls.Coins.Heads).ConfigureAwait(false);
                    break;
                case ImageKeys.CoinTails:
                    await Load(key, ImageUrls.Coins.Tails).ConfigureAwait(false);
                    break;
                case ImageKeys.Dice:
                    await Load(key, ImageUrls.Dice).ConfigureAwait(false);
                    break;
                case ImageKeys.SlotBg:
                    await Load(key, ImageUrls.Slots.Bg).ConfigureAwait(false);
                    break;
                case ImageKeys.SlotEmojis:
                    await Load(key, ImageUrls.Slots.Emojis).ConfigureAwait(false);
                    break;
                case ImageKeys.Currency:
                    await Load(key, ImageUrls.Currency).ConfigureAwait(false);
                    break;
                case ImageKeys.RipOverlay:
                    await Load(key, ImageUrls.Rip.Overlay).ConfigureAwait(false);
                    break;
                case ImageKeys.RipBg:
                    await Load(key, ImageUrls.Rip.Bg).ConfigureAwait(false);
                    break;
                case ImageKeys.XpBg:
                    await Load(key, ImageUrls.Xp.Bg).ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private async Task Load(ImageKeys key, Uri uri)
    {
        var data = await GetImageData(uri).ConfigureAwait(false);
        if (data is null)
            return;

        await Db.StringSetAsync(GetRedisKey(key), data, flags: CommandFlags.FireAndForget).ConfigureAwait(false);
    }

    private async Task Load(ImageKeys key, Uri[] uris)
    {
        await Db.KeyDeleteAsync(GetRedisKey(key)).ConfigureAwait(false);
        var imageData = await Task.WhenAll(uris.Select(GetImageData)).ConfigureAwait(false);
        var vals = imageData
            .Where(x => x is not null)
            .Select(x => (RedisValue)x)
            .ToArray();

        await Db.ListRightPushAsync(GetRedisKey(key), vals, flags: CommandFlags.FireAndForget).ConfigureAwait(false);

        if (uris.Length != vals.Length)
        {
            Log.Information("{Loaded}/{Max} URIs for the key '{ImageKey}' have been loaded.\n" +
                            "Some of the supplied URIs are either unavailable or invalid",
                vals.Length, uris.Length, key);
        }
    }

    private async Task<byte[]?> GetImageData(Uri uri)
    {
        if (uri.IsFile)
        {
            try
            {
                return await File.ReadAllBytesAsync(uri.LocalPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed reading image bytes from uri: {Uri}", uri.ToString());
                return null;
            }
        }

        try
        {
            return await http.GetByteArrayAsync(uri).ConfigureAwait(false);
        }
        catch
        {
            Log.Warning("Image url you provided is not a valid image: {Uri}", uri.ToString());
            return null;
        }
    }

    private async Task<bool> AllKeysExist()
    {
        var tasks = await Task.WhenAll(GetAllKeys()
            .Select(x => Db.KeyExistsAsync(GetRedisKey(x)))).ConfigureAwait(false);

        return tasks.All(exist => exist);
    }

    private static IEnumerable<ImageKeys> GetAllKeys() =>
        Enum.GetValues<ImageKeys>();

    private byte[][] GetByteArrayData(ImageKeys key)
        => Db.ListRange(GetRedisKey(key)).Map(x => (byte[])x);

    private byte[] GetByteData(ImageKeys key)
        => Db.StringGet(GetRedisKey(key));

    private RedisKey GetRedisKey(ImageKeys key)
        => $"{redisKey}_image_{key}";
}