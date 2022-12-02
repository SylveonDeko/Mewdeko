using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.Yml;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Services.Impl;

public sealed class RedisImagesCache : IImageCache, IReadyExecutor, INService
{
    private readonly ConnectionMultiplexer con;
    private readonly IBotCredentials creds;
    private readonly HttpClient http;
    private readonly string imagesPath;

    private IDatabase Db => con.GetDatabase();

    private const string BasePath = "data/";
    private const string CardsPath = "data/images/cards";

    public ImageUrls ImageUrls { get; private set; }

    public enum ImageKeys
    {
        CoinHeads,
        CoinTails,
        Dice,
        SlotBg,
        SlotEmojis,
        Currency,
        RipOverlay,
        RipBg,
        XpBg
    }

    public IReadOnlyList<byte[]> Heads
        => GetByteArrayData(ImageKeys.CoinHeads);

    public IReadOnlyList<byte[]> Tails
        => GetByteArrayData(ImageKeys.CoinTails);

    public IReadOnlyList<byte[]> Dice
        => GetByteArrayData(ImageKeys.Dice);

    public IReadOnlyList<byte[]> SlotEmojis
        => GetByteArrayData(ImageKeys.SlotEmojis);

    public IReadOnlyList<byte[]> Currency
        => GetByteArrayData(ImageKeys.Currency);

    public byte[] SlotBackground
        => GetByteData(ImageKeys.SlotBg);

    public byte[] XpBackground
        => GetByteData(ImageKeys.XpBg);

    public byte[] Rip
        => GetByteData(ImageKeys.RipBg);

    public byte[] RipOverlay
        => GetByteData(ImageKeys.RipOverlay);

    public byte[] GetCard(string key) =>
        // since cards are always local for now, don't cache them
        File.ReadAllBytes(Path.Join(CardsPath, $"{key}.jpg"));

    public async Task OnReadyAsync()
    {
        if (await AllKeysExist().ConfigureAwait(false))
            return;

        await Reload().ConfigureAwait(false);
    }

    public RedisImagesCache(ConnectionMultiplexer con, IBotCredentials creds)
    {
        this.con = con;
        this.creds = creds;
        http = new HttpClient();
        imagesPath = Path.Combine(BasePath, "images.yml");

        ImageUrls = Yaml.Deserializer.Deserialize<ImageUrls>(File.ReadAllText(imagesPath));
    }

    public async Task Reload()
    {
        ImageUrls = Yaml.Deserializer.Deserialize<ImageUrls>(await File.ReadAllTextAsync(imagesPath).ConfigureAwait(false));
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
                            "Some of the supplied URIs are either unavailable or invalid.",
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
        catch (Exception ex)
        {
            Log.Warning(ex, "Image url you provided is not a valid image: {Uri}", uri.ToString());
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
        => $"{creds.RedisKey()}_image_{key}";
}