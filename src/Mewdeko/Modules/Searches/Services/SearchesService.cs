using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using GTranslate.Translators;
using Html2Markdown;
using Mewdeko.Modules.Searches.Common;
using Mewdeko.Services.Impl;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using Image = SixLabors.ImageSharp.Image;

namespace Mewdeko.Modules.Searches.Services;

public class SearchesService : INService, IUnloadableService
{
    public enum ImageTag
    {
        Food,
        Dogs,
        Cats,
        Birds
    }

    private static readonly HtmlParser GoogleParser = new(new HtmlParserOptions
    {
        IsScripting = false,
        IsEmbedded = false,
        IsSupportingProcessingInstructions = false,
        IsKeepingSourceReferences = false,
        IsNotSupportingFrames = true
    });

    private readonly ConcurrentDictionary<ulong, HashSet<string>> blacklistedTags;
    private readonly IDataCache cache;
    private readonly IBotCredentials creds;
    private readonly DbService db;
    private readonly FontProvider fonts;
    private readonly IGoogleApiService google;
    private readonly IHttpClientFactory httpFactory;

    private readonly ConcurrentDictionary<ulong, SearchImageCacher> imageCacher = new();
    private readonly IImageCache imgs;
    private readonly List<string> nsfwreddits;
    private readonly MewdekoRandom rng;
    private readonly List<string?> yomamaJokes;

    private readonly object yomamaLock = new();
    private int yomamaJokeIndex;

    public SearchesService(DiscordSocketClient client, IGoogleApiService google,
        DbService db, IDataCache cache, IHttpClientFactory factory,
        FontProvider fonts, IBotCredentials creds)
    {
        httpFactory = factory;
        this.google = google;
        this.db = db;
        imgs = cache.LocalImages;
        this.cache = cache;
        this.fonts = fonts;
        this.creds = creds;
        rng = new MewdekoRandom();
        using var uow = db.GetDbContext();
        var gc = uow.GuildConfigs.Include(x => x.NsfwBlacklistedTags).Where(x => client.Guilds.Select(socketGuild => socketGuild.Id).Contains(x.GuildId));
        blacklistedTags = new ConcurrentDictionary<ulong, HashSet<string>>(
            gc.ToDictionary(
                x => x.GuildId,
                x => new HashSet<string>(x.NsfwBlacklistedTags.Select(y => y.Tag))));

        //translate commands
        client.MessageReceived += msg =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (msg is not SocketUserMessage umsg)
                        return;

                    if (!TranslatedChannels.TryGetValue(umsg.Channel.Id, out var autoDelete))
                        return;

                    var key = (umsg.Author.Id, umsg.Channel.Id);

                    if (!UserLanguages.TryGetValue(key, out var langs))
                        return;
                    string text;
                    if (langs.Contains('<'))
                    {
                        var split = langs.Split('<');
                        text = await AutoTranslate(umsg.Resolve(TagHandling.Ignore), split[1], split[0])
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        var split = langs.Split('>');
                        text = await AutoTranslate(umsg.Resolve(TagHandling.Ignore), split[0], split[1])
                            .ConfigureAwait(false);
                    }

                    if (autoDelete)
                    {
                        try
                        {
                            await umsg.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    await umsg.Channel.SendConfirmAsync(
                            $"{umsg.Author.Mention} `:` {text.Replace("<@ ", "<@", StringComparison.InvariantCulture).Replace("<@! ", "<@!", StringComparison.InvariantCulture)}")
                        .ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        };

        //joke commands
        if (File.Exists("data/wowjokes.json"))
            WowJokes = JsonConvert.DeserializeObject<List<WoWJoke>>(File.ReadAllText("data/wowjokes.json"));
        else
            Log.Warning("data/wowjokes.json is missing. WOW Jokes are not loaded.");

        if (File.Exists("data/magicitems.json"))
            MagicItems = JsonConvert.DeserializeObject<List<MagicItem>>(File.ReadAllText("data/magicitems.json"));
        else
            Log.Warning("data/magicitems.json is missing. Magic items are not loaded.");

        if (File.Exists("data/yomama.txt"))
        {
            yomamaJokes = File.ReadAllLines("data/yomama.txt")
                .Shuffle()
                .ToList();
        }

        if (File.Exists("data/ultimatelist.txt"))
        {
            nsfwreddits = File.ReadAllLines("data/ultimatelist.txt")
                .ToList();
        }
    }

    public ConcurrentDictionary<ulong, bool> TranslatedChannels { get; } = new();

    // (userId, channelId)
    public ConcurrentDictionary<(ulong UserId, ulong ChannelId), string> UserLanguages { get; } = new();

    public List<WoWJoke> WowJokes { get; } = new();
    public List<MagicItem> MagicItems { get; } = new();

    public ConcurrentDictionary<ulong, Timer> AutoHentaiTimers { get; } = new();
    public ConcurrentDictionary<ulong, Timer> AutoBoobTimers { get; } = new();
    public ConcurrentDictionary<ulong, Timer> AutoButtTimers { get; } = new();

    public Task Unload()
    {
        AutoBoobTimers.ForEach(x => x.Value.Change(Timeout.Infinite, Timeout.Infinite));
        AutoBoobTimers.Clear();
        AutoButtTimers.ForEach(x => x.Value.Change(Timeout.Infinite, Timeout.Infinite));
        AutoButtTimers.Clear();
        AutoHentaiTimers.ForEach(x => x.Value.Change(Timeout.Infinite, Timeout.Infinite));
        AutoHentaiTimers.Clear();

        imageCacher.Clear();
        return Task.CompletedTask;
    }

    public async Task SetShip(ulong user1, ulong user2, int score)
        => await cache.SetShip(user1, user2, score);

    public async Task<ShipCache?> GetShip(ulong user1, ulong user2)
        => await cache.GetShip(user1, user2);

    public async Task<Stream> GetRipPictureAsync(string text, Uri imgUrl)
    {
        var data = await cache.GetOrAddCachedDataAsync($"Mewdeko_rip_{text}_{imgUrl}",
            GetRipPictureFactory,
            (text, imgUrl),
            TimeSpan.FromDays(1)).ConfigureAwait(false);

        return data.ToStream();
    }

    private static void DrawAvatar(Image bg, Image avatarImage) => bg.Mutate(x => x.Grayscale().DrawImage(avatarImage, new Point(83, 139), new GraphicsOptions()));

    public async Task<byte[]> GetRipPictureFactory((string text, Uri avatarUrl) arg)
    {
        var (text, avatarUrl) = arg;
        using var bg = Image.Load<Rgba32>(imgs.Rip.ToArray());
        var (succ, data) = (false, (byte[])null); //await _cache.TryGetImageDataAsync(avatarUrl);
        if (!succ)
        {
            using var http = httpFactory.CreateClient();
            data = await http.GetByteArrayAsync(avatarUrl).ConfigureAwait(false);
            using (var avatarImg = Image.Load<Rgba32>(data))
            {
                avatarImg.Mutate(x => x
                    .Resize(85, 85)
                    .ApplyRoundedCorners(42));
                data = avatarImg.ToStream().ToArray();
                DrawAvatar(bg, avatarImg);
            }

            await cache.SetImageDataAsync(avatarUrl, data).ConfigureAwait(false);
        }
        else
        {
            using var avatarImg = Image.Load<Rgba32>(data);
            DrawAvatar(bg, avatarImg);
        }

        bg.Mutate(x => x.DrawText(text, fonts.RipFont, Color.Black, new PointF(25, 225)));

        //flowa
        using (var flowers = Image.Load(imgs.RipOverlay.ToArray()))
        {
            bg.Mutate(x => x.DrawImage(flowers, new Point(0, 0), new GraphicsOptions()));
        }

        return bg.ToStream().ToArray();
    }

    public Task<WeatherData?> GetWeatherDataAsync(string query)
    {
        query = query.Trim().ToLowerInvariant();

        return cache.GetOrAddCachedDataAsync($"Mewdeko_weather_{query}",
            GetWeatherDataFactory,
            query,
            TimeSpan.FromHours(3));
    }

    private async Task<WeatherData>? GetWeatherDataFactory(string query)
    {
        using var http = httpFactory.CreateClient();
        try
        {
            var data = await http.GetStringAsync(
                $"https://api.openweathermap.org/data/2.5/weather?q={query}&appid=42cd627dd60debf25a5739e50a217d74&units=metric").ConfigureAwait(false);

            return string.IsNullOrEmpty(data) ? null : JsonConvert.DeserializeObject<WeatherData>(data);
        }
        catch (Exception ex)
        {
            Log.Warning(ex.Message);
            return null;
        }
    }

    public Task<((string Address, DateTime Time, string TimeZoneName), TimeErrors?)> GetTimeDataAsync(string arg) => GetTimeDataFactory(arg);

    //return _cache.GetOrAddCachedDataAsync($"Mewdeko_time_{arg}",
    //    GetTimeDataFactory,
    //    arg,
    //    TimeSpan.FromMinutes(1));
    private async Task<((string Address, DateTime Time, string TimeZoneName), TimeErrors?)> GetTimeDataFactory(
        string query)
    {
        query = query.Trim();

        if (string.IsNullOrEmpty(query)) return (default, TimeErrors.InvalidInput);

        if (string.IsNullOrWhiteSpace(creds.LocationIqApiKey)
            || string.IsNullOrWhiteSpace(creds.TimezoneDbApiKey))
        {
            return (default, TimeErrors.ApiKeyMissing);
        }

        try
        {
            using var http = httpFactory.CreateClient();
            var res = await cache.GetOrAddCachedDataAsync($"geo_{query}", _ =>
            {
                var url =
                    $"https://eu1.locationiq.com/v1/search.php?{(string.IsNullOrWhiteSpace(creds.LocationIqApiKey) ? "key=" : $"key={creds.LocationIqApiKey}&")}q={Uri.EscapeDataString(query)}&format=json";

                return http.GetStringAsync(url);
            }, "", TimeSpan.FromHours(1)).ConfigureAwait(false);

            var responses = JsonConvert.DeserializeObject<LocationIqResponse[]>(res);
            if (responses is null || responses.Length == 0)
            {
                Log.Warning("Geocode lookup failed for: {Query}", query);
                return (default, TimeErrors.NotFound);
            }

            var geoData = responses[0];

            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"http://api.timezonedb.com/v2.1/get-time-zone?key={creds.TimezoneDbApiKey}&format=json&by=position&lat={geoData.Lat}&lng={geoData.Lon}");
            using var geoRes = await http.SendAsync(req).ConfigureAwait(false);
            var resString = await geoRes.Content.ReadAsStringAsync().ConfigureAwait(false);
            var timeObj = JsonConvert.DeserializeObject<TimeZoneResult>(resString);

            var time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(timeObj.Timestamp);

            return ((
                Address: responses[0].DisplayName,
                Time: time,
                TimeZoneName: timeObj.TimezoneName
            ), default);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Weather error: {Message}", ex.Message);
            return (default, TimeErrors.NotFound);
        }
    }

    public Uri GetRandomImageUrl(ImageTag tag)
    {
        var subpath = tag.ToString().ToLowerInvariant();

        var max = tag switch
        {
            ImageTag.Food => 773,
            ImageTag.Dogs => 750,
            ImageTag.Cats => 773,
            ImageTag.Birds => 578,
            _ => 100
        };

        return new Uri($"https://nadeko-pictures.nyc3.digitaloceanspaces.com/{subpath}/{rng.Next(1, max):000}.png");
    }

    public static async Task<string> AutoTranslate(string str, string from, string to)
    {
        using var translator = new AggregateTranslator();
        var translation = await translator.TranslateAsync(str, to, from).ConfigureAwait(false);
        return translation.Translation == str ? (await translator.TransliterateAsync(str, to, from).ConfigureAwait(false)).Transliteration : translation.Translation;
    }

    public static async Task<string> Translate(string langs, string? text = null)
    {
        using var translator = new AggregateTranslator();
        var translation = await translator.TranslateAsync(text, langs).ConfigureAwait(false);
        return translation.Translation == text ? (await translator.TransliterateAsync(text, langs).ConfigureAwait(false)).Transliteration : translation.Translation;
    }

    public Task<ImageCacherObject?> DapiSearch(string? tag, DapiSearchType type, ulong? guild,
        bool isExplicit = false)
    {
        tag ??= "";
        if (string.IsNullOrWhiteSpace(tag)
            && (tag.Contains("loli") || tag.Contains("shota")))
        {
            return Task.FromResult<ImageCacherObject>(null);
        }

        var tags = tag
            .Split('+')
            .Select(x => x.ToLowerInvariant().Replace(' ', '_'))
            .ToArray();

        if (guild.HasValue)
        {
            var hashSet = GetBlacklistedTags(guild.Value);

            var cacher = imageCacher.GetOrAdd(guild.Value, _ => new SearchImageCacher(httpFactory));

            return cacher.GetImage(tags, isExplicit, type, hashSet);
        }
        else
        {
            var cacher = imageCacher.GetOrAdd(guild ?? 0, _ => new SearchImageCacher(httpFactory));

            return cacher.GetImage(tags, isExplicit, type);
        }
    }

    public HashSet<string> GetBlacklistedTags(ulong guildId)
    {
        return blacklistedTags.TryGetValue(guildId, out var tags) ? tags : new HashSet<string>();
    }

    public async Task<bool> ToggleBlacklistedTag(ulong guildId, string tag)
    {
        var tagObj = new NsfwBlacklitedTag
        {
            Tag = tag
        };

        bool added;
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set.Include(y => y.NsfwBlacklistedTags));
        if (gc.NsfwBlacklistedTags.Add(tagObj))
        {
            added = true;
        }
        else
        {
            gc.NsfwBlacklistedTags.Remove(tagObj);
            var toRemove = gc.NsfwBlacklistedTags.FirstOrDefault(x => x.Equals(tagObj));
            if (toRemove != null)
                uow.Remove(toRemove);
            added = false;
        }

        var newTags = new HashSet<string>(gc.NsfwBlacklistedTags.Select(x => x.Tag));
        blacklistedTags.AddOrUpdate(guildId, newTags, delegate { return newTags; });

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return added;
    }

    public void ClearCache()
    {
        foreach (var c in imageCacher) c.Value.Clear();
    }

    public bool NsfwCheck(string reddit) => nsfwreddits.Contains(reddit, StringComparer.OrdinalIgnoreCase);

    public Task<string?> GetYomamaJoke()
    {
        string? joke;
        lock (yomamaLock)
        {
            if (yomamaJokeIndex >= yomamaJokes.Count)
            {
                yomamaJokeIndex = 0;
                var newList = yomamaJokes.ToList();
                yomamaJokes.Clear();
                yomamaJokes.AddRange(newList.Shuffle());
            }

            joke = yomamaJokes[yomamaJokeIndex++];
        }

        return Task.FromResult(joke);

        // using (var http = _httpFactory.CreateClient())
        // {
        //     var response = await http.GetStringAsync(new Uri("http://api.yomomma.info/")).ConfigureAwait(false);
        //     return JObject.Parse(response)["joke"].ToString() + " 😆";
        // }
    }

    public async Task<(string? Setup, string Punchline)> GetRandomJoke()
    {
        using var http = httpFactory.CreateClient();
        var res = await http.GetStringAsync("https://official-joke-api.appspot.com/random_joke").ConfigureAwait(false);
        var resObj = JsonConvert.DeserializeAnonymousType(res, new
        {
            setup = "", punchline = ""
        });
        return (resObj.setup, resObj.punchline);
    }

    public async Task<string?> GetChuckNorrisJoke()
    {
        using var http = httpFactory.CreateClient();
        var response = await http.GetStringAsync(new Uri("https://api.icndb.com/jokes/random/"))
            .ConfigureAwait(false);
        return $"{JObject.Parse(response)["value"]["joke"]} 😆";
    }

    public async Task<MtgData?> GetMtgCardAsync(string search)
    {
        search = search.Trim().ToLowerInvariant();
        var data = await cache.GetOrAddCachedDataAsync($"Mewdeko_mtg_{search}",
            GetMtgCardFactory,
            search,
            TimeSpan.FromDays(1)).ConfigureAwait(false);

        return !data.Any() ? null : data[rng.Next(0, data.Length)];
    }

    private async Task<MtgData[]> GetMtgCardFactory(string search)
    {
        async Task<MtgData> GetMtgDataAsync(MtgResponse.Data card)
        {
            string storeUrl;
            try
            {
                storeUrl = await google.ShortenUrl(
                        $"https://shop.tcgplayer.com/productcatalog/product/show?newSearch=false&ProductType=All&IsProductNameExact=false&ProductName={Uri.EscapeDataString(card.Name)}")
                    .ConfigureAwait(false);
            }
            catch
            {
                storeUrl = "<url can't be found>";
            }

            return new MtgData
            {
                Description = card.Text,
                Name = card.Name,
                ImageUrl = card.ImageUrl,
                StoreUrl = storeUrl,
                Types = string.Join(",\n", card.Types),
                ManaCost = card.ManaCost
            };
        }

        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        var response = await http
            .GetStringAsync($"https://api.magicthegathering.io/v1/cards?name={Uri.EscapeDataString(search)}")
            .ConfigureAwait(false);

        var responseObject = JsonConvert.DeserializeObject<MtgResponse>(response);
        if (responseObject == null)
            return Array.Empty<MtgData>();

        var cards = responseObject.Cards.Take(5).ToArray();
        if (cards.Length == 0)
            return Array.Empty<MtgData>();

        var tasks = new List<Task<MtgData>>(cards.Length);
        tasks.AddRange(cards.Select(GetMtgDataAsync));

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public Task<HearthstoneCardData?> GetHearthstoneCardDataAsync(string name)
    {
        name = name.ToLowerInvariant();
        return cache.GetOrAddCachedDataAsync($"Mewdeko_hearthstone_{name}",
            HearthstoneCardDataFactory,
            name,
            TimeSpan.FromDays(1));
    }

    private async Task<HearthstoneCardData>? HearthstoneCardDataFactory(string name)
    {
        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("x-rapidapi-key", creds.MashapeKey);
        try
        {
            var response = await http.GetStringAsync(
                    $"https://omgvamp-hearthstone-v1.p.rapidapi.com/cards/search/{Uri.EscapeDataString(name)}")
                .ConfigureAwait(false);
            var objs = JsonConvert.DeserializeObject<HearthstoneCardData[]>(response);
            if (objs == null || objs.Length == 0)
                return null;
            var data = Array.Find(objs, x => x.Collectible)
                       ?? Array.Find(objs, x => !string.IsNullOrEmpty(x.PlayerClass))
                       ?? objs.FirstOrDefault();
            if (data == null)
                return null;
            if (!string.IsNullOrWhiteSpace(data.Img))
                data.Img = await google.ShortenUrl(data.Img).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(data.Text))
            {
                var converter = new Converter();
                data.Text = converter.Convert(data.Text);
            }

            return data;
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message);
            return null;
        }
    }

    public Task<OmdbMovie?> GetMovieDataAsync(string name)
    {
        name = name.Trim().ToLowerInvariant();
        return cache.GetOrAddCachedDataAsync($"Mewdeko_movie_{name}",
            GetMovieDataFactory,
            name,
            TimeSpan.FromDays(1));
    }

    private async Task<OmdbMovie?> GetMovieDataFactory(string name)
    {
        using var http = httpFactory.CreateClient();
        var res = await http.GetStringAsync($"https://omdbapi.nadeko.bot/?t={name.Trim().Replace(' ', '+')}&y=&plot=full&r=json").ConfigureAwait(false);
        var movie = JsonConvert.DeserializeObject<OmdbMovie>(res);
        if (movie?.Title == null)
            return null;
        movie.Poster = await google.ShortenUrl(movie.Poster).ConfigureAwait(false);
        return movie;
    }

    public async Task<int> GetSteamAppIdByName(string query)
    {
        var redis = cache.Redis;
        var redisDb = redis.GetDatabase();
        const string steamGameIdsKey = "steam_names_to_appid";
        await redisDb.KeyExistsAsync(steamGameIdsKey).ConfigureAwait(false);

        // if we didn't get steam name to id map already, get it
        //if (!exists)
        //{
        //    using (var http = _httpFactory.CreateClient())
        //    {
        //        // https://api.steampowered.com/ISteamApps/GetAppList/v2/
        //        var gamesStr = await http.GetStringAsync("https://api.steampowered.com/ISteamApps/GetAppList/v2/").ConfigureAwait(false);
        //        var apps = JsonConvert.DeserializeAnonymousType(gamesStr, new { applist = new { apps = new List<SteamGameId>() } }).applist.apps;

        //        //await db.HashSetAsync("steam_game_ids", apps.Select(app => new HashEntry(app.Name.Trim().ToLowerInvariant(), app.AppId)).ToArray()).ConfigureAwait(false);
        //        await db.StringSetAsync("steam_game_ids", gamesStr, TimeSpan.FromHours(24));
        //        //await db.KeyExpireAsync("steam_game_ids", TimeSpan.FromHours(24), CommandFlags.FireAndForget).ConfigureAwait(false);
        //    }
        //}

        var gamesMap = await cache.GetOrAddCachedDataAsync(steamGameIdsKey, async _ =>
        {
            using var http = httpFactory.CreateClient();
            // https://api.steampowered.com/ISteamApps/GetAppList/v2/
            var gamesStr = await http.GetStringAsync("https://api.steampowered.com/ISteamApps/GetAppList/v2/")
                .ConfigureAwait(false);
            var apps = JsonConvert
                .DeserializeAnonymousType(gamesStr, new
                {
                    applist = new
                    {
                        apps = new List<SteamGameId>()
                    }
                })
                .applist.apps;

            return apps
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .GroupBy(x => x.Name)
                .ToDictionary(x => x.Key, x => x.First().AppId);
            //await db.HashSetAsync("steam_game_ids", apps.Select(app => new HashEntry(app.Name.Trim().ToLowerInvariant(), app.AppId)).ToArray()).ConfigureAwait(false);
            //await db.StringSetAsync("steam_game_ids", gamesStr, TimeSpan.FromHours(24));
            //await db.KeyExpireAsync("steam_game_ids", TimeSpan.FromHours(24), CommandFlags.FireAndForget).ConfigureAwait(false);
        }, default(string), TimeSpan.FromHours(24)).ConfigureAwait(false);

        if (!gamesMap.Any())
            return -1;

        query = query.Trim();

        var keyList = gamesMap.Keys.ToList();

        var key = keyList.Find(x => x.Equals(query, StringComparison.OrdinalIgnoreCase));

        if (key == default)
        {
            key = keyList.Find(x => x.StartsWith(query, StringComparison.OrdinalIgnoreCase));
            if (key == default)
                return -1;
        }

        return gamesMap[key];

        //// try finding the game id
        //var val = db.HashGet(STEAM_GAME_IDS_KEY, query);
        //if (val == default)
        //    return -1; // not found

        //var appid = (int)val;
        //return appid;

        // now that we have appid, get the game info with that appid
        //var gameData = await _cache.GetOrAddCachedDataAsync($"steam_game:{appid}", SteamGameDataFactory, appid, TimeSpan.FromHours(12))
        //    .ConfigureAwait(false);

        //return gameData;
    }

    public async Task<GoogleSearchResultData?> GoogleSearchAsync(string query)
    {
        query = WebUtility.UrlEncode(query)?.Replace(' ', '+');

        var fullQueryLink = $"https://www.google.ca/search?q={query}&safe=on&lr=lang_eng&hl=en&ie=utf-8&oe=utf-8";

        using var msg = new HttpRequestMessage(HttpMethod.Get, fullQueryLink);
        msg.Headers.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.77 Safari/537.36");
        msg.Headers.Add("Cookie", "CONSENT=YES+shp.gws-20210601-0-RC2.en+FX+423;");

        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        var sw = Stopwatch.StartNew();
        using var response = await http.SendAsync(msg).ConfigureAwait(false);
        var content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        sw.Stop();
        Log.Information("Took {Miliseconds}ms to parse results", sw.ElapsedMilliseconds);

        using var document = await GoogleParser.ParseDocumentAsync(content).ConfigureAwait(false);
        var elems = document.QuerySelectorAll("div.g > div > div");

        var resultsElem = document.QuerySelectorAll("#resultStats").FirstOrDefault();
        var totalResults = resultsElem?.TextContent;
        //var time = resultsElem.Children.FirstOrDefault()?.TextContent
        //^ this doesn't work for some reason, <nobr> is completely missing in parsed collection
        if (!elems.Any())
            return default;

        var results = elems.Select(elem =>
            {
                var children = elem.Children.ToList();
                if (children.Count < 2)
                    return null;

                var href = (children[0].QuerySelector("a") as IHtmlAnchorElement)?.Href;
                var name = children[0].QuerySelector("h3")?.TextContent;

                if (href == null || name == null)
                    return null;

                var txt = children[1].TextContent;

                if (string.IsNullOrWhiteSpace(txt))
                    return null;

                return new GoogleSearchResult(name, href, txt);
            })
            .Where(x => x != null)
            .ToList();

        return new GoogleSearchResultData(
            results.AsReadOnly(),
            fullQueryLink,
            totalResults);
    }

    public async Task<GoogleSearchResultData?> DuckDuckGoSearchAsync(string query)
    {
        query = WebUtility.UrlEncode(query)?.Replace(' ', '+');

        const string fullQueryLink = "https://html.duckduckgo.com/html";

        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.77 Safari/537.36");

        using var formData = new MultipartFormDataContent
        {
            {
                new StringContent(query), "q"
            }
        };
        using var response = await http.PostAsync(fullQueryLink, formData).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        using var document = await GoogleParser.ParseDocumentAsync(content).ConfigureAwait(false);
        var searchResults = document.QuerySelector(".results");
        var elems = searchResults.QuerySelectorAll(".result");

        if (!elems.Any())
            return default;

        var results = elems.Select(elem =>
            {
                if (elem.QuerySelector(".result__a") is IHtmlAnchorElement anchor)
                {
                    var href = anchor.Href;
                    var name = anchor.TextContent;

                    if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(name))
                        return null;

                    var txt = elem.QuerySelector(".result__snippet")?.TextContent;

                    if (string.IsNullOrWhiteSpace(txt))
                        return null;

                    return new GoogleSearchResult(name, href, txt);
                }

                return null;
            })
            .Where(x => x != null)
            .ToList();

        return new GoogleSearchResultData(
            results.AsReadOnly(),
            fullQueryLink,
            "0");
    }
}

public record RedditCache
{
    public IGuild Guild { get; set; }
    public string Url { get; set; }
}

//private async Task<SteamGameData> SteamGameDataFactory(int appid)
//{
//    using (var http = _httpFactory.CreateClient())
//    {
//        //  https://store.steampowered.com/api/appdetails?appids=
//        var responseStr = await http.GetStringAsync($"https://store.steampowered.com/api/appdetails?appids={appid}").ConfigureAwait(false);
//        var data = JsonConvert.DeserializeObject<Dictionary<int, SteamGameData.Container>>(responseStr);
//        if (!data.ContainsKey(appid) || !data[appid].Success)
//            return null; // for some reason we can't get the game with valid appid. SHould never happen

//        return data[appid].Data;
//    }
//}

public class GoogleSearchResultData
{
    public GoogleSearchResultData(IReadOnlyList<GoogleSearchResult> results, string fullQueryLink,
        string totalResults)
    {
        Results = results;
        FullQueryLink = fullQueryLink;
        TotalResults = totalResults;
    }

    public IReadOnlyList<GoogleSearchResult> Results { get; }
    public string FullQueryLink { get; }
    public string TotalResults { get; }
}

public class SteamGameId
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("appid")]
    public int AppId { get; set; }
}

public class SteamGameData
{
    public string ShortDescription { get; set; }

    public class Container
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("data")]
        public SteamGameData Data { get; set; }
    }
}

public enum TimeErrors
{
    InvalidInput,
    ApiKeyMissing,
    NotFound,
    Unknown
}

public class ShipCache
{
    public ulong User1 { get; set; }
    public ulong User2 { get; set; }
    public int Score { get; set; }
}