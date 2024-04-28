using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using GTranslate.Translators;
using Html2Markdown;
using Mewdeko.Modules.Searches.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SkiaSharp;

namespace Mewdeko.Modules.Searches.Services;

/// <summary>
/// Service for handling search-related commands.
/// </summary>
public class SearchesService : INService, IUnloadableService
{
    /// <summary>
    /// Represents the type of Image search.
    /// </summary>
    public enum ImageTag
    {
        /// <summary>
        /// Represents a search for food images.
        /// </summary>
        Food,

        /// <summary>
        /// Represents a search for dog images.
        /// </summary>
        Dogs,

        /// <summary>
        /// Represents a search for cat images.
        /// </summary>
        Cats,

        /// <summary>
        /// Represents a search for bird images.
        /// </summary>
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

    private readonly IDataCache cache;
    private readonly IBotCredentials creds;
    private readonly IGoogleApiService google;
    private readonly GuildSettingsService gss;
    private readonly IHttpClientFactory httpFactory;

    private readonly ConcurrentDictionary<ulong, SearchImageCacher> imageCacher = new();
    private readonly IImageCache imgs;
    private readonly List<string> nsfwreddits;
    private readonly MewdekoRandom rng;
    private readonly List<string?> yomamaJokes;

    private readonly object yomamaLock = new();
    private int yomamaJokeIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchesService"/> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="google">The Google API service.</param>
    /// <param name="cache">The data cache.</param>
    /// <param name="factory">The HTTP client factory.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="gss">The guild setting service.</param>
    public SearchesService(DiscordSocketClient client, IGoogleApiService google, IDataCache cache,
        IHttpClientFactory factory,
        IBotCredentials creds, GuildSettingsService gss)
    {
        httpFactory = factory;
        this.google = google;
        imgs = cache.LocalImages;
        this.cache = cache;
        this.creds = creds;
        this.gss = gss;
        rng = new MewdekoRandom();

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
            Log.Warning("data/wowjokes.json is missing. WOW Jokes are not loaded");

        if (File.Exists("data/magicitems.json"))
            MagicItems = JsonConvert.DeserializeObject<List<MagicItem>>(File.ReadAllText("data/magicitems.json"));
        else
            Log.Warning("data/magicitems.json is missing. Magic items are not loaded");

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

    /// <summary>
    /// Gets the collection of channels where auto translation is enabled.
    /// </summary>
    public ConcurrentDictionary<ulong, bool> TranslatedChannels { get; } = new();

    // (userId, channelId)
    /// <summary>
    /// Gets the collection of user languages.
    /// </summary>
    public ConcurrentDictionary<(ulong UserId, ulong ChannelId), string> UserLanguages { get; } = new();

    /// <summary>
    /// Gets the collection of WOW jokes.
    /// </summary>
    public List<WoWJoke> WowJokes { get; } = new();

    /// <summary>
    /// Gets the collection of magic items.
    /// </summary>
    public List<MagicItem> MagicItems { get; } = new();

    /// <summary>
    /// Gets the collection of auto hentai timers.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoHentaiTimers { get; } = new();

    /// <summary>
    /// Gets the collection of auto boob timers.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoBoobTimers { get; } = new();

    /// <summary>
    /// Gets the collection of auto butt timers.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoButtTimers { get; } = new();

    /// <summary>
    /// Unloads the service, clearing timers and caches.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method should be called when the service is being unloaded to clean up resources.
    /// </remarks>
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

    /// <summary>
    /// Sets the relationship score between two users.
    /// </summary>
    /// <param name="user1">The ID of the first user.</param>
    /// <param name="user2">The ID of the second user.</param>
    /// <param name="score">The score indicating the relationship strength.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method sets the relationship score between two users, typically used in a dating context.
    /// </remarks>
    public Task SetShip(ulong user1, ulong user2, int score)
        => cache.SetShip(user1, user2, score);

    /// <summary>
    /// Retrieves the relationship score between two users.
    /// </summary>
    /// <param name="user1">The ID of the first user.</param>
    /// <param name="user2">The ID of the second user.</param>
    /// <returns>A task representing the asynchronous operation, returning the relationship score.</returns>
    /// <remarks>
    /// This method retrieves the relationship score between two users, typically used in a dating context.
    /// </remarks>
    public Task<ShipCache?> GetShip(ulong user1, ulong user2)
        => cache.GetShip(user1, user2);

    /// <summary>
    /// Generates a "rest in peace" image with the provided text and avatar URL.
    /// </summary>
    /// <param name="text">The text to display on the image.</param>
    /// <param name="imgUrl">The URL of the avatar image.</param>
    /// <returns>A stream containing the generated image.</returns>
    /// <remarks>
    /// This method generates an image with the provided text and an avatar image, typically used in memorial contexts.
    /// </remarks>
    public async Task<Stream> GetRipPictureAsync(string text, Uri imgUrl)
    {
        var data = await cache.GetOrAddCachedDataAsync($"Mewdeko_rip_{text}_{imgUrl}",
            GetRipPictureFactory,
            (text, imgUrl),
            TimeSpan.FromDays(1)).ConfigureAwait(false);

        return data.ToStream();
    }

    private async Task<byte[]> GetRipPictureFactory((string text, Uri avatarUrl) arg)
    {
        var (text, avatarUrl) = arg;

        var bg = SKBitmap.Decode(imgs.Rip.ToArray());
        var (succ, data) = await cache.TryGetImageDataAsync(avatarUrl);
        if (!succ)
        {
            using var http = httpFactory.CreateClient();
            data = await http.GetByteArrayAsync(avatarUrl).ConfigureAwait(false);

            using (var avatarImg = SKBitmap.Decode(data))
            {
                var resizedAvatarImg = avatarImg.Resize(new SKImageInfo(85, 85), SKFilterQuality.High);
                var roundedAvatarImg = ApplyRoundedCorners(resizedAvatarImg, 42);

                data = SKImage.FromBitmap(roundedAvatarImg).Encode().ToArray();
                DrawAvatar(bg, roundedAvatarImg);
            }

            await cache.SetImageDataAsync(avatarUrl, data).ConfigureAwait(false);
        }
        else
        {
            using var avatarImg = SKBitmap.Decode(data);
            DrawAvatar(bg, avatarImg);
        }

        var textPaint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright),
            TextSize = 14,
            IsAntialias = true,
            Color = SKColors.Black
        };

        using var canvas = new SKCanvas(bg);
        canvas.DrawText(text, new SKPoint(25, 225), textPaint);

        //flowa
        using var flowers = SKBitmap.Decode(imgs.RipOverlay.ToArray());
        DrawImage(bg, flowers, new SKPoint(0, 0));

        return SKImage.FromBitmap(bg).Encode().ToArray();
    }

// Helper method to draw rounded corners
    private static SKBitmap ApplyRoundedCorners(SKBitmap input, float radius)
    {
        var output = new SKBitmap(input.Width, input.Height, input.AlphaType == SKAlphaType.Opaque);

        using var paint = new SKPaint
        {
            IsAntialias = true
        };
        using var clipPath = new SKPath();

        var rect = new SKRect(0, 0, input.Width, input.Height);
        clipPath.AddRoundRect(rect, radius, radius);

        using var canvas = new SKCanvas(output);
        canvas.ClipPath(clipPath);
        canvas.DrawBitmap(input, 0, 0, paint);

        return output;
    }


// Helper method to draw an image on a canvas
    private static void DrawAvatar(SKBitmap bg, SKBitmap avatar)
    {
        using var canvas = new SKCanvas(bg);
        canvas.DrawBitmap(avatar, new SKPoint(0, 0));
    }

// Helper method to draw an image on a canvas
    private static void DrawImage(SKBitmap bg, SKBitmap image, SKPoint location)
    {
        using var canvas = new SKCanvas(bg);
        canvas.DrawBitmap(image, location);
    }


    /// <summary>
    /// Fetches weather data for the specified location.
    /// </summary>
    /// <param name="query">The location for which to fetch weather data.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning the weather data for the specified location.
    /// </returns>
    /// <remarks>
    /// This method fetches weather data for the specified location using the OpenWeatherMap API.
    /// </remarks>
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
                    $"https://api.openweathermap.org/data/2.5/weather?q={query}&appid=42cd627dd60debf25a5739e50a217d74&units=metric")
                .ConfigureAwait(false);

            return string.IsNullOrEmpty(data) ? null : JsonConvert.DeserializeObject<WeatherData>(data);
        }
        catch (Exception ex)
        {
            Log.Warning(ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Retrieves time data for the specified location.
    /// </summary>
    /// <param name="arg">The query string specifying the location.</param>
    /// <returns>
    /// A tuple containing the address, time, and timezone name for the specified location,
    /// along with any errors encountered during the operation.
    /// </returns>
    /// <remarks>
    /// This method retrieves time data for the specified location by geocoding the query and
    /// querying the timezone database API.
    /// </remarks>
    public Task<((string Address, DateTime Time, string TimeZoneName), TimeErrors?)> GetTimeDataAsync(string arg) =>
        GetTimeDataFactory(arg);

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

    /// <summary>
    /// Generates a random image URL based on the provided tag.
    /// </summary>
    /// <param name="tag">The tag specifying the category of images.</param>
    /// <returns>A URI representing a randomly selected image.</returns>
    /// <remarks>
    /// This method generates a random image URL based on the provided tag, typically used for displaying images in various contexts.
    /// </remarks>
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

    /// <summary>
    /// Automatically translates the input string from one language to another.
    /// </summary>
    /// <param name="str">The string to translate.</param>
    /// <param name="from">The source language code.</param>
    /// <param name="to">The target language code.</param>
    /// <returns>A task representing the asynchronous operation, returning the translated string.</returns>
    private static async Task<string> AutoTranslate(string str, string from, string to)
    {
        using var translator = new AggregateTranslator();
        var translation = await translator.TranslateAsync(str, to, from).ConfigureAwait(false);
        return translation.Translation == str
            ? (await translator.TransliterateAsync(str, to, from).ConfigureAwait(false)).Transliteration
            : translation.Translation;
    }

    /// <summary>
    /// Translates the input text to the specified languages.
    /// </summary>
    /// <param name="langs">A string representing the target languages separated by comma (e.g., "en,fr,de").</param>
    /// <param name="text">The text to translate. If not provided, the method translates the language of the provided text.</param>
    /// <returns>A task representing the asynchronous operation, returning the translated string.</returns>
    public static async Task<string> Translate(string langs, string? text = null)
    {
        using var translator = new AggregateTranslator();
        var translation = await translator.TranslateAsync(text, langs).ConfigureAwait(false);
        return translation.Translation == text
            ? (await translator.TransliterateAsync(text, langs).ConfigureAwait(false)).Transliteration
            : translation.Translation;
    }

    /// <summary>
    /// Performs a search using the DAPI (Danbooru) API.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <param name="type">The type of search (e.g., Safe, Explicit).</param>
    /// <param name="guild">The ID of the guild where the search is performed.</param>
    /// <param name="isExplicit">A boolean indicating whether the search is explicit or not.</param>
    /// <returns>A task representing the asynchronous operation, returning the search result.</returns>
    public async Task<ImageCacherObject?> DapiSearch(string? tag, DapiSearchType type, ulong? guild,
        bool isExplicit = false)
    {
        tag ??= "";
        if (string.IsNullOrWhiteSpace(tag)
            && (tag.Contains("loli") || tag.Contains("shota")))
        {
            return null;
        }

        var tags = tag
            .Split('+')
            .Select(x => x.ToLowerInvariant().Replace(' ', '_'))
            .ToArray();

        if (guild.HasValue)
        {
            var hashSet = await GetBlacklistedTags(guild.Value);

            var cacher = imageCacher.GetOrAdd(guild.Value, _ => new SearchImageCacher(httpFactory));

            return await cacher.GetImage(tags, isExplicit, type, hashSet);
        }
        else
        {
            var cacher = imageCacher.GetOrAdd(guild ?? 0, _ => new SearchImageCacher(httpFactory));

            return await cacher.GetImage(tags, isExplicit, type);
        }
    }

    /// <summary>
    /// Retrieves the blacklisted tags for the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A HashSet containing the blacklisted tags for the guild.</returns>
    private async Task<HashSet<string>> GetBlacklistedTags(ulong guildId)
    {
        var config = await gss.GetGuildConfig(guildId);
        return config.NsfwBlacklistedTags.Count != 0
            ? [..config.NsfwBlacklistedTags.Select(x => x.Tag)]
            : [];
    }

    /// <summary>
    /// Checks if a given Reddit is marked as NSFW.
    /// </summary>
    /// <param name="reddit">The Reddit to check.</param>
    /// <returns>True if the Reddit is marked as NSFW, otherwise false.</returns>
    public bool NsfwCheck(string reddit) => nsfwreddits.Contains(reddit, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Retrieves a "Yo Mama" joke.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning the "Yo Mama" joke.</returns>
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

    /// <summary>
    /// Retrieves a random joke.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning a tuple containing the setup and punchline of the joke.</returns>
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

    /// <summary>
    /// Retrieves a Chuck Norris joke.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning the Chuck Norris joke.</returns>
    public async Task<string?> GetChuckNorrisJoke()
    {
        using var http = httpFactory.CreateClient();
        var response = await http.GetStringAsync(new Uri("https://api.icndb.com/jokes/random/"))
            .ConfigureAwait(false);
        return $"{JObject.Parse(response)["value"]["joke"]} 😆";
    }

    /// <summary>
    /// Retrieves Magic: The Gathering card data asynchronously.
    /// </summary>
    /// <param name="search">The search query for the card.</param>
    /// <returns>A task representing the asynchronous operation, returning the Magic: The Gathering card data.</returns>
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

    /// <summary>
    /// Retrieves Hearthstone card data asynchronously.
    /// </summary>
    /// <param name="name">The name of the Hearthstone card.</param>
    /// <returns>A task representing the asynchronous operation, returning the Hearthstone card data.</returns>
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

    /// <summary>
    /// Retrieves movie data asynchronously from the OMDB API.
    /// </summary>
    /// <param name="name">The name of the movie.</param>
    /// <returns>A task representing the asynchronous operation, returning the movie data.</returns>
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
        var res = await http
            .GetStringAsync($"https://omdbapi.nadeko.bot/?t={name.Trim().Replace(' ', '+')}&y=&plot=full&r=json")
            .ConfigureAwait(false);
        var movie = JsonConvert.DeserializeObject<OmdbMovie>(res);
        if (movie?.Title == null)
            return null;
        movie.Poster = await google.ShortenUrl(movie.Poster).ConfigureAwait(false);
        return movie;
    }

    /// <summary>
    /// Retrieves the Steam App ID for the specified game name asynchronously.
    /// </summary>
    /// <param name="query">The name of the game to search for.</param>
    /// <returns>A task representing the asynchronous operation, returning the Steam App ID of the game.</returns>
    public async Task<int> GetSteamAppIdByName(string query)
    {
        var redis = cache.Redis;
        var redisDb = redis.GetDatabase();
        const string steamGameIdsKey = "steam_names_to_appid";
        await redisDb.KeyExistsAsync(steamGameIdsKey).ConfigureAwait(false);

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
    }

    /// <summary>
    /// Performs a Google search asynchronously.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <returns>A task representing the asynchronous operation, returning the Google search results.</returns>
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

    /// <summary>
    /// Performs a DuckDuckGo search asynchronously.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <returns>A task representing the asynchronous operation, returning the DuckDuckGo search results.</returns>
    public async Task<GoogleSearchResultData?> DuckDuckGoSearchAsync(string query)
    {
        query = WebUtility.UrlEncode(query)?.Replace(' ', '+');

        const string fullQueryLink = "https://html.duckduckgo.com/html";

        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.77 Safari/537.36");

        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(query), "q");
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

/// <summary>
/// Represents already posted Reddit posts.
/// </summary>
public record RedditCache
{
    /// <summary>
    /// The guild where the post was posted.
    /// </summary>
    public IGuild Guild { get; set; }

    /// <summary>
    /// The url of the post.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// Represents the result data of a Google search operation.
/// </summary>
public class GoogleSearchResultData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleSearchResultData"/> class.
    /// </summary>
    /// <param name="results">The list of search results.</param>
    /// <param name="fullQueryLink">The full query link used for the search.</param>
    /// <param name="totalResults">The total number of search results.</param>
    public GoogleSearchResultData(IReadOnlyList<GoogleSearchResult> results, string fullQueryLink, string totalResults)
    {
        Results = results;
        FullQueryLink = fullQueryLink;
        TotalResults = totalResults;
    }

    /// <summary>
    /// Gets the list of search results.
    /// </summary>
    public IReadOnlyList<GoogleSearchResult> Results { get; }

    /// <summary>
    /// Gets the full query link used for the search.
    /// </summary>
    public string FullQueryLink { get; }

    /// <summary>
    /// Gets the total number of search results.
    /// </summary>
    public string TotalResults { get; }
}

/// <summary>
/// Represents a Steam game ID and its associated name.
/// </summary>
public class SteamGameId
{
    /// <summary>
    /// Gets or sets the name of the Steam game.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the Steam App ID of the game.
    /// </summary>
    [JsonProperty("appid")]
    public int AppId { get; set; }
}

/// <summary>
/// Represents data related to a Steam game.
/// </summary>
public class SteamGameData
{
    /// <summary>
    /// Gets or sets the short description of the Steam game.
    /// </summary>
    public string ShortDescription { get; set; }

    /// <summary>
    /// Represents a container for Steam game data.
    /// </summary>
    public class Container
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the Steam game data.
        /// </summary>
        [JsonProperty("data")]
        public SteamGameData Data { get; set; }
    }
}

/// <summary>
/// Enumerates the possible time-related errors.
/// </summary>
public enum TimeErrors
{
    /// <summary>
    /// Invalid input error.
    /// </summary>
    InvalidInput,

    /// <summary>
    /// API key missing error.
    /// </summary>
    ApiKeyMissing,

    /// <summary>
    /// Not found error.
    /// </summary>
    NotFound,

    /// <summary>
    /// Unknown error.
    /// </summary>
    Unknown
}

/// <summary>
/// Represents data related to a ship.
/// </summary>
public class ShipCache
{
    /// <summary>
    /// Gets or sets the first user ID.
    /// </summary>
    public ulong User1 { get; set; }

    /// <summary>
    /// Gets or sets the second user ID.
    /// </summary>
    public ulong User2 { get; set; }

    /// <summary>
    /// Gets or sets the score of the ship.
    /// </summary>
    public int Score { get; set; }
}