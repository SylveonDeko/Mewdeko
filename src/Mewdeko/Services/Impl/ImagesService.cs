using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.Yml;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Services.Impl;

public sealed class RedisImagesCache : IImageCache, IReadyExecutor
{
    private readonly ConnectionMultiplexer _con;
        private readonly IBotCredentials _creds;
        private readonly HttpClient _http;
        private readonly string _imagesPath;


        private IDatabase Db => _con.GetDatabase();

        private const string BASE_PATH = "data/";
        private const string CARDS_PATH = "data/images/cards";

        public ImageUrls ImageUrls { get; private set; }

        public enum ImageKeys
        {
            CoinHeads,
            CoinTails,
            Dice,
            SlotBg,
            SlotEmojis,
            Currency,
            RategirlMatrix,
            RategirlDot,
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

        public byte[] RategirlMatrix 
            => GetByteData(ImageKeys.RategirlMatrix);

        public byte[] RategirlDot 
            => GetByteData(ImageKeys.RategirlDot);

        public byte[] XpBackground 
            => GetByteData(ImageKeys.XpBg);

        public byte[] Rip 
            => GetByteData(ImageKeys.RipBg);

        public byte[] RipOverlay 
            => GetByteData(ImageKeys.RipOverlay);

        public byte[] GetCard(string key) =>
            // since cards are always local for now, don't cache them
            File.ReadAllBytes(Path.Join(CARDS_PATH, key + ".jpg"));


        public async Task OnReadyAsync()
        {
            if (await AllKeysExist())
                return;

            await Reload();
        }

        public RedisImagesCache(ConnectionMultiplexer con, IBotCredentials creds)
        {
            _con = con;
            _creds = creds;
            _http = new HttpClient();
            _imagesPath = Path.Combine(BASE_PATH, "images.yml");

            Migrate();

            ImageUrls = Yaml.Deserializer.Deserialize<ImageUrls>(File.ReadAllText(_imagesPath));
        }
        
        private void Migrate()
        {
            // migrate to yml
            if (File.Exists(Path.Combine(BASE_PATH, "images.json")))
            {
                var oldFilePath = Path.Combine(BASE_PATH, "images.json");
                var backupFilePath = Path.Combine(BASE_PATH, "images.json.backup");
                
                var oldData = JsonConvert.DeserializeObject<OldImageUrls>(
                    File.ReadAllText(oldFilePath));

                if (oldData is not null)
                {
                    var newData = new ImageUrls()
                    {
                        Coins = new ImageUrls.CoinData()
                        {
                            Heads = oldData.Coins.Heads.Length == 1 && 
                                oldData.Coins.Heads[0].ToString() == "https://nadeko-pictures.nyc3.digitaloceanspaces.com/other/coins/heads.png"
                            ? new[] { new Uri("https://cdn.nadeko.bot/coins/heads3.png") }
                            : oldData.Coins.Heads,
                            Tails = oldData.Coins.Tails.Length == 1 && 
                                    oldData.Coins.Tails[0].ToString() == "https://nadeko-pictures.nyc3.digitaloceanspaces.com/other/coins/tails.png"
                                ? new[] { new Uri("https://cdn.nadeko.bot/coins/tails3.png") }
                                : oldData.Coins.Tails,
                        },
                        Dice = oldData.Dice.Map(x => x.ToNewCdn()),
                        Currency = oldData.Currency.Map(x => x.ToNewCdn()),
                        Rategirl = new ImageUrls.RategirlData()
                        {
                            Dot = oldData.Rategirl.Dot.ToNewCdn(),
                            Matrix = oldData.Rategirl.Matrix.ToNewCdn()
                        },
                        Rip = new ImageUrls.RipData()
                        {
                            Bg = oldData.Rip.Bg.ToNewCdn(),
                            Overlay = oldData.Rip.Overlay.ToNewCdn(),
                        },
                        Slots = new ImageUrls.SlotData()
                        {
                            Bg = new Uri("https://cdn.nadeko.bot/slots/slots_bg.png"),
                            Emojis = new[]
                            {
                                "https://cdn.nadeko.bot/slots/0.png",
                                "https://cdn.nadeko.bot/slots/1.png",
                                "https://cdn.nadeko.bot/slots/2.png",
                                "https://cdn.nadeko.bot/slots/3.png",
                                "https://cdn.nadeko.bot/slots/4.png",
                                "https://cdn.nadeko.bot/slots/5.png"
                            }.Map(x => new Uri(x))
                        },
                        Xp = new ImageUrls.XpData()
                        {
                            Bg = oldData.Xp.Bg.ToNewCdn(),
                        },
                        Version = 2,
                    };

                    File.Move(oldFilePath, backupFilePath, true);
                    File.WriteAllText(_imagesPath, Yaml.Serializer.Serialize(newData));
                }
            }

            // removed numbers from slots
            var localImageUrls = Yaml.Deserializer.Deserialize<ImageUrls>(File.ReadAllText(_imagesPath));
            if (localImageUrls.Version == 2)
            {
                localImageUrls.Version = 3;
                File.WriteAllText(_imagesPath, Yaml.Serializer.Serialize(localImageUrls));
            }
        }

        public async Task Reload()
        {
            ImageUrls = Yaml.Deserializer.Deserialize<ImageUrls>(await File.ReadAllTextAsync(_imagesPath));
            foreach (var key in GetAllKeys())
            {
                switch (key)
                {
                    case ImageKeys.CoinHeads:
                        await Load(key, ImageUrls.Coins.Heads);
                        break;
                    case ImageKeys.CoinTails:
                        await Load(key, ImageUrls.Coins.Tails);
                        break;
                    case ImageKeys.Dice:
                        await Load(key, ImageUrls.Dice);
                        break;
                    case ImageKeys.SlotBg:
                        await Load(key, ImageUrls.Slots.Bg);
                        break;
                    case ImageKeys.SlotEmojis:
                        await Load(key, ImageUrls.Slots.Emojis);
                        break;
                    case ImageKeys.Currency:
                        await Load(key, ImageUrls.Currency);
                        break;
                    case ImageKeys.RategirlMatrix:
                        await Load(key, ImageUrls.Rategirl.Matrix);
                        break;
                    case ImageKeys.RategirlDot:
                        await Load(key, ImageUrls.Rategirl.Dot);
                        break;
                    case ImageKeys.RipOverlay:
                        await Load(key, ImageUrls.Rip.Overlay);
                        break;
                    case ImageKeys.RipBg:
                        await Load(key, ImageUrls.Rip.Bg);
                        break;
                    case ImageKeys.XpBg:
                        await Load(key, ImageUrls.Xp.Bg);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private async Task Load(ImageKeys key, Uri uri)
        {
            var data = await GetImageData(uri);
            if (data is null)
                return;

            await Db.StringSetAsync(GetRedisKey(key), data);
        }

        private async Task Load(ImageKeys key, Uri[] uris)
        {
            await Db.KeyDeleteAsync(GetRedisKey(key));
            var imageData = await Task.WhenAll(uris.Select(GetImageData));
            var vals = imageData
                .Where(x => x is not null)
                .Select(x => (RedisValue)x)
                .ToArray();

            await Db.ListRightPushAsync(GetRedisKey(key), vals);
            
            if (uris.Length != vals.Length)
            {
                Log.Information("{Loaded}/{Max} URIs for the key '{ImageKey}' have been loaded.\n" +
                                "Some of the supplied URIs are either unavailable or invalid.", 
                    vals.Length, uris.Length, key);
            }
        }

        private async Task<byte[]> GetImageData(Uri uri)
        {
            if (uri.IsFile)
            {
                try
                {
                    var bytes = await File.ReadAllBytesAsync(uri.LocalPath);
                    return bytes;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed reading image bytes from uri: {Uri}", uri.ToString());
                    return null;
                }
            }

            try
            {
                return await _http.GetByteArrayAsync(uri);
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
                .Select(x => Db.KeyExistsAsync(GetRedisKey(x))));

            return tasks.All(exist => exist);
        }

        private IEnumerable<ImageKeys> GetAllKeys() =>
            Enum.GetValues<ImageKeys>();

        private byte[][] GetByteArrayData(ImageKeys key)
            => Db.ListRange(GetRedisKey(key)).Map(x => (byte[])x);

        private byte[] GetByteData(ImageKeys key)
            => Db.StringGet(GetRedisKey(key));

        private RedisKey GetRedisKey(ImageKeys key) 
            => _creds.RedisKey() + "_image_" + key;
    }
    