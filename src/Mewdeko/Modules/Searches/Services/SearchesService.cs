using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using GTranslate.Translators;
using LinqToDB.Async;
using MartineApiNet;
using MartineApiNet.Enums;
using MartineApiNet.Models.Images;
using Mewdeko.Modules.Searches.Common;
using Mewdeko.Services.Strings;
using Newtonsoft.Json.Linq;
using Refit;
using SkiaSharp;

namespace Mewdeko.Modules.Searches.Services;

/// <summary>
///     Service for handling search-related commands.
/// </summary>
public class SearchesService : INService, IUnloadableService
{
    /// <summary>
    ///     Represents the type of Image search.
    /// </summary>
    public enum ImageTag
    {
        /// <summary>
        ///     Represents a search for food images.
        /// </summary>
        Food,

        /// <summary>
        ///     Represents a search for dog images.
        /// </summary>
        Dogs,

        /// <summary>
        ///     Represents a search for cat images.
        /// </summary>
        Cats,

        /// <summary>
        ///     Represents a search for bird images.
        /// </summary>
        Birds
    }

    // Cached JsonSerializerOptions for performance
    private static readonly JsonSerializerOptions CachedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
    private readonly IDataConnectionFactory dbFactory;
    private readonly IGoogleApiService google;
    private readonly IHttpClientFactory httpFactory;

    private readonly ConcurrentDictionary<ulong, SearchImageCacher> imageCacher = new();
    private readonly IImageCache imgs;
    private readonly ILogger<SearchesService> logger;
    private readonly MartineApi martineApi;
    private readonly List<string> nsfwreddits;
    private readonly MewdekoRandom rng;
    private readonly GeneratedBotStrings strings;
    private readonly List<string?> yomamaJokes;

    private readonly object yomamaLock = new();
    private int yomamaJokeIndex;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SearchesService" /> class.
    /// </summary>
    /// <param name="google">The Google API service.</param>
    /// <param name="cache">The data cache.</param>
    /// <param name="factory">The HTTP client factory.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="handler">Async discord event handler because stoopid</param>
    /// <param name="martineApi">Reddit!</param>
    /// <param name="dbFactory">The db context provider</param>
    /// <param name="strings">The localized strings service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public SearchesService(IGoogleApiService google, IDataCache cache,
        IHttpClientFactory factory,
        IBotCredentials creds, EventHandler handler, MartineApi martineApi, IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings, ILogger<SearchesService> logger)
    {
        httpFactory = factory;
        this.google = google;
        imgs = cache.LocalImages;
        this.cache = cache;
        this.creds = creds;
        this.martineApi = martineApi;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.logger = logger;
        rng = new MewdekoRandom();

        //translate commands
        handler.Subscribe("MessageReceived", "SearchesService", async (SocketMessage msg) =>
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
                        strings.SearchResultUser(((IGuildChannel)umsg.Channel).GuildId, umsg.Author.Mention,
                            text.Replace("<@ ", "<@", StringComparison.InvariantCulture)
                                .Replace("<@! ", "<@!", StringComparison.InvariantCulture)))
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        });

        //joke commands
        if (File.Exists("data/wowjokes.json"))
            WowJokes = JsonSerializer.Deserialize<List<WoWJoke>>(File.ReadAllText("data/wowjokes.json"));
        else
            logger.LogWarning("data/wowjokes.json is missing. WOW Jokes are not loaded");

        if (File.Exists("data/magicitems.json"))
            MagicItems = JsonSerializer.Deserialize<List<MagicItem>>(File.ReadAllText("data/magicitems.json"));
        else
            logger.LogWarning("data/magicitems.json is missing. Magic items are not loaded");

        if (File.Exists("data/yomama.txt"))
        {
            yomamaJokes = File.ReadAllLines("data/yomama.txt")
                .SecureShuffle()
                .ToList();
        }

        if (File.Exists("data/ultimatelist.txt"))
        {
            nsfwreddits = File.ReadAllLines("data/ultimatelist.txt")
                .ToList();
        }
    }

    /// <summary>
    ///     Gets the collection of channels where auto translation is enabled.
    /// </summary>
    public ConcurrentDictionary<ulong, bool> TranslatedChannels { get; } = new();

    // (userId, channelId)
    /// <summary>
    ///     Gets the collection of user languages.
    /// </summary>
    public ConcurrentDictionary<(ulong UserId, ulong ChannelId), string> UserLanguages { get; } = new();

    /// <summary>
    ///     Gets the collection of WOW jokes.
    /// </summary>
    public List<WoWJoke> WowJokes { get; } = [];

    /// <summary>
    ///     Gets the collection of magic items.
    /// </summary>
    public List<MagicItem> MagicItems { get; } = [];

    /// <summary>
    ///     Gets the collection of auto hentai timers.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoHentaiTimers { get; } = new();

    /// <summary>
    ///     Gets the collection of auto boob timers.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoBoobTimers { get; } = new();

    /// <summary>
    ///     Gets the collection of auto butt timers.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoButtTimers { get; } = new();

    /// <summary>
    ///     Unloads the service, clearing timers and caches.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    ///     This method should be called when the service is being unloaded to clean up resources.
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
    ///     Sets the relationship score between two users.
    /// </summary>
    /// <param name="user1">The ID of the first user.</param>
    /// <param name="user2">The ID of the second user.</param>
    /// <param name="score">The score indicating the relationship strength.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    ///     This method sets the relationship score between two users, typically used in a dating context.
    /// </remarks>
    public Task SetShip(ulong user1, ulong user2, int score)
    {
        return cache.SetShip(user1, user2, score);
    }

    /// <summary>
    ///     Retrieves the relationship score between two users.
    /// </summary>
    /// <param name="user1">The ID of the first user.</param>
    /// <param name="user2">The ID of the second user.</param>
    /// <returns>A task representing the asynchronous operation, returning the relationship score.</returns>
    /// <remarks>
    ///     This method retrieves the relationship score between two users, typically used in a dating context.
    /// </remarks>
    public Task<ShipCache?> GetShip(ulong user1, ulong user2)
    {
        return cache.GetShip(user1, user2);
    }

    /// <summary>
    ///     Generates a "rest in peace" image with the provided text and avatar URL.
    /// </summary>
    /// <param name="text">The text to display on the image.</param>
    /// <param name="imgUrl">The URL of the avatar image.</param>
    /// <returns>A stream containing the generated image.</returns>
    /// <remarks>
    ///     This method generates an image with the provided text and an avatar image, typically used in memorial contexts.
    /// </remarks>
    public async Task<Stream?> GetRipPictureAsync(string text, Uri imgUrl)
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
                var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear);
                var resizedAvatarImg = avatarImg.Resize(new SKImageInfo(85, 85), samplingOptions);
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

        // Create a font first
        var textFont = new SKFont
        {
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright),
            Size = 14
        };

        var textPaint = new SKPaint
        {
            IsAntialias = true, Color = SKColors.Black
        };

        using var canvas = new SKCanvas(bg);

        canvas.DrawText(text, 25, 225, textFont, textPaint);

        //flowa
        using var flowers = SKBitmap.Decode(imgs.RipOverlay.ToArray());
        DrawImage(bg, flowers, new SKPoint(0, 0));

        return SKImage.FromBitmap(bg).Encode().ToArray();
    }

// Helper method to draw rounded corners
    private static SKBitmap ApplyRoundedCorners(SKBitmap input, float radius)
    {
        var output = new SKBitmap(input.Width, input.Height, input.AlphaType == SKAlphaType.Opaque);

        using var paint = new SKPaint();
        paint.IsAntialias = true;
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
    /// Gets weather data for the specified location.
    /// </summary>
    /// <param name="query">The location query.</param>
    public async Task<OpenMeteoWeatherResponse?> GetWeatherDataAsync(string query)
    {
        using var http = httpFactory.CreateClient();
        try
        {
            // First, geocode the location
            var geocodeUrl =
                $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&count=1&language=en";
            var geocodeResponse = await http.GetStringAsync(geocodeUrl).ConfigureAwait(false);
            var geocodeData = JsonSerializer.Deserialize<OpenMeteoGeocodingResponse>(geocodeResponse);

            if (geocodeData?.Results == null || geocodeData.Results.Count == 0)
            {
                logger.LogWarning("No location found for query: {Query}", query);
                return null;
            }

            var location = geocodeData.Results[0];

            // Now get weather data
            var weatherUrl =
                $"{creds.OpenMeteoApiUrl}/v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}" +
                "&current=temperature_2m,relative_humidity_2m,dew_point_2m,apparent_temperature,weather_code," +
                "surface_pressure,pressure_msl,cloud_cover,visibility,is_day,wind_speed_10m,wind_direction_10m," +
                "wind_gusts_10m,precipitation,rain,snowfall,uv_index,shortwave_radiation,evapotranspiration," +
                "cape,freezing_level_height,soil_temperature_0cm,soil_moisture_0_to_1cm" +
                "&hourly=temperature_2m,relative_humidity_2m,dew_point_2m,apparent_temperature,precipitation_probability," +
                "weather_code,wind_speed_10m,wind_direction_10m,wind_gusts_10m,pressure_msl,cloud_cover," +
                "visibility,precipitation,rain,snowfall,uv_index,shortwave_radiation,cape,soil_temperature_0cm" +
                "&daily=temperature_2m_max,temperature_2m_min,sunrise,sunset,daylight_duration,sunshine_duration," +
                "uv_index_max,precipitation_sum,precipitation_probability_max,rain_sum,snowfall_sum," +
                "wind_speed_10m_max,wind_gusts_10m_max,wind_direction_10m_dominant,shortwave_radiation_sum," +
                "et0_fao_evapotranspiration" +
                "&forecast_days=7&timezone=auto";

            var weatherResponse = await http.GetStringAsync(weatherUrl).ConfigureAwait(false);
            var weatherData = JsonSerializer.Deserialize<OpenMeteoWeatherResponse>(weatherResponse);

            if (weatherData != null)
            {
                // Build smart location string avoiding duplicates
                var locationParts = new List<string>
                {
                    location.Name
                };

                // Only add Admin1 (state/province) if it's different from the city name
                if (!string.IsNullOrEmpty(location.Admin1) &&
                    !location.Admin1.Equals(location.Name, StringComparison.OrdinalIgnoreCase))
                {
                    locationParts.Add(location.Admin1);
                }

                // Add location info to the response
                weatherData.Current.LocationName = string.Join(", ", locationParts);
                weatherData.Current.Country = location.Country;
                weatherData.Current.CountryCode = location.CountryCode;
            }

            return weatherData;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error fetching weather data for query: {Query}", query);
            return null;
        }
    }

    /// <summary>
    /// Gets time data for the specified location using Open-Meteo.
    /// </summary>
    public async Task<((string Address, DateTime Time, string TimeZoneName), TimeErrors?)> GetTimeDataAsync(
        string query)
    {
        var result = await GetTimeDataWithCandidatesAsync(query);
        if (result.candidates?.Count > 0)
        {
            var best = result.candidates[0];
            return ((best.Address, best.Time, best.TimeZoneName), null);
        }

        return (default, result.error);
    }

    /// <summary>
    ///     Gets time data with multiple timezone candidates for disambiguation.
    /// </summary>
    public async
        Task<(List<(string Address, DateTime Time, string TimeZoneName, string TimezoneId)>? candidates, TimeErrors?
            error)>
        GetTimeDataWithCandidatesAsync(string query)
    {
        query = query.Trim();

        if (string.IsNullOrEmpty(query))
            return (null, TimeErrors.InvalidInput);

        // Check if query is a timezone abbreviation or ID - get multiple candidates
        var timezoneCandidates = GetTimezoneInfoCandidates(query);
        if (timezoneCandidates.Count > 0)
        {
            var candidates = new List<(string Address, DateTime Time, string TimeZoneName, string TimezoneId)>();

            foreach (var tz in timezoneCandidates.Take(10)) // Limit to 10 candidates for select menu
            {
                var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                candidates.Add((
                    Address: $"{tz.DisplayName}",
                    Time: currentTime,
                    TimeZoneName: tz.Id,
                    TimezoneId: tz.Id
                ));
            }

            return (candidates, null);
        }

        using var http = httpFactory.CreateClient();
        try
        {
            // First, geocode the location using Open-Meteo
            var geocodeUrl =
                $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&count=1&language=en";
            var geocodeResponse = await http.GetStringAsync(geocodeUrl).ConfigureAwait(false);
            var geocodeData = JsonSerializer.Deserialize<OpenMeteoGeocodingResponse>(geocodeResponse);

            if (geocodeData?.Results == null || geocodeData.Results.Count == 0)
            {
                logger.LogWarning("Geocoding failed for time query: {Query}", query);
                return (default, TimeErrors.NotFound);
            }

            var location = geocodeData.Results[0];

            // Get basic weather data to obtain timezone information
            var weatherUrl =
                $"{creds.OpenMeteoApiUrl}/v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}&current=temperature_2m&timezone=auto";
            var weatherResponse = await http.GetStringAsync(weatherUrl).ConfigureAwait(false);
            var weatherData = JsonSerializer.Deserialize<OpenMeteoWeatherResponse>(weatherResponse);

            if (weatherData == null)
            {
                logger.LogWarning("Failed to get timezone data for: {Query}", query);
                return (default, TimeErrors.NotFound);
            }

            // Parse the current time from the weather response (it's already in local timezone)
            var currentTime = DateTime.Parse(weatherData.Current.Time);

            // Build address string, avoiding duplicates
            var addressParts = new List<string>
            {
                location.Name
            };

            // Only add Admin1 (state/province) if it's different from the city name
            if (!string.IsNullOrEmpty(location.Admin1) &&
                !location.Admin1.Equals(location.Name, StringComparison.OrdinalIgnoreCase))
            {
                addressParts.Add(location.Admin1);
            }

            if (!string.IsNullOrEmpty(location.Country))
                addressParts.Add(location.Country);

            var address = string.Join(", ", addressParts);

            return ([
                (address, currentTime, weatherData.Timezone ?? weatherData.TimezoneAbbreviation ?? "Unknown",
                    weatherData.Timezone ?? "Unknown")
            ], null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting time data for query: {Query}", query);
            return (null, TimeErrors.NotFound);
        }
    }

    /// <summary>
    /// Gets multiple timezone candidates for disambiguation.
    /// </summary>
    private List<TimeZoneInfo> GetTimezoneInfoCandidates(string query)
    {
        // First check if we can get an exact single match
        var exactMatch = TryGetTimezoneInfo(query);
        if (exactMatch != null)
        {
            return [exactMatch];
        }

        // If no exact match, use the intelligent search to get multiple candidates
        return GetTimezoneSearchCandidates(query);
    }

    /// <summary>
    ///     Attempts to get TimeZoneInfo from common timezone abbreviations or IDs.
    /// </summary>
    private TimeZoneInfo? TryGetTimezoneInfo(string query)
    {
        // Try direct timezone ID lookup first
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(query);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Direct timezone lookup failed for query: {Query}", query);
            // Continue to conversion attempts
        }

        // Try converting IANA to Windows ID
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(query, out var windowsId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to find timezone using converted Windows ID '{WindowsId}' for query: {Query}", windowsId,
                    query);
                // Continue to next attempt
            }
        }

        // Try converting Windows to IANA ID
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(query.ToUpper(), null, out var ianaId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to find timezone using converted IANA ID '{IanaId}' for query: {Query}",
                    ianaId, query);
                // Continue to manual mapping
            }
        }

        return null;
    }

    /// <summary>
    ///     Gets multiple timezone candidates using intelligent search.
    /// </summary>
    private List<TimeZoneInfo> GetTimezoneSearchCandidates(string query)
    {
        var normalizedQuery = query.ToUpperInvariant();
        var candidates = new List<(TimeZoneInfo tz, int priority, int distance)>();

        foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
        {
            try
            {
                // Priority 1: Exact abbreviation match from standard/daylight names
                var standardAbbrev = GetAbbreviation(tz.StandardName);
                var daylightAbbrev = !string.IsNullOrEmpty(tz.DaylightName) ? GetAbbreviation(tz.DaylightName) : "";

                if (string.Equals(standardAbbrev, normalizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    // Give extra priority to canonical timezone IDs (like America/New_York for EST)
                    var priority = IsCanonicalTimezone(tz.Id, normalizedQuery) ? 1 : 2;
                    candidates.Add((tz, priority, 0));
                    continue;
                }

                if (string.Equals(daylightAbbrev, normalizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    // Give extra priority to canonical timezone IDs
                    var priority = IsCanonicalTimezone(tz.Id, normalizedQuery) ? 1 : 2;
                    candidates.Add((tz, priority, 0));
                    continue;
                }

                // Priority 3: Direct timezone ID match
                if (string.Equals(tz.Id, query, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add((tz, 3, 0));
                    continue;
                }

                // Priority 4: Word matches in standard/daylight names
                if (tz.StandardName.Split(' ').Any(word =>
                        string.Equals(word, normalizedQuery, StringComparison.OrdinalIgnoreCase)))
                {
                    candidates.Add((tz, 4, 0));
                    continue;
                }

                if (!string.IsNullOrEmpty(tz.DaylightName) &&
                    tz.DaylightName.Split(' ').Any(word =>
                        string.Equals(word, normalizedQuery, StringComparison.OrdinalIgnoreCase)))
                {
                    candidates.Add((tz, 4, 0));
                    continue;
                }

                // Priority 5: Fuzzy abbreviation match using Levenshtein distance
                var standardDistance = standardAbbrev.LevenshteinDistance(normalizedQuery);
                var daylightDistance = !string.IsNullOrEmpty(daylightAbbrev)
                    ? daylightAbbrev.LevenshteinDistance(normalizedQuery)
                    : int.MaxValue;

                var minDistance = Math.Min(standardDistance, daylightDistance);
                if (minDistance <= 2 && normalizedQuery.Length >= 3) // Allow small typos for 3+ char queries
                {
                    candidates.Add((tz, 5, minDistance));
                    continue;
                }

                // Priority 6: Display name contains query
                if (tz.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    var displayDistance = tz.DisplayName.ToUpperInvariant().LevenshteinDistance(normalizedQuery);
                    candidates.Add((tz, 6, displayDistance));
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error checking timezone {TimezoneId} for query: {Query}", tz.Id, query);
            }
        }

        if (candidates.Count == 0)
        {
            logger.LogWarning("No timezone found for query: {Query}", query);
            return [];
        }

        // Return multiple candidates ordered by priority and distance
        var sortedCandidates = candidates
            .OrderBy(c => c.priority)
            .ThenBy(c => c.distance)
            .Take(10) // Limit for select menu
            .Select(c => c.tz)
            .ToList();

        logger.LogDebug("Found {Count} timezone candidates for query: {Query}", sortedCandidates.Count, query);

        return sortedCandidates;
    }

    /// <summary>
    ///     Determines if a timezone ID is the canonical/primary timezone for a given abbreviation.
    /// </summary>
    private static bool IsCanonicalTimezone(string timezoneId, string abbreviation)
    {
        return abbreviation.ToUpperInvariant() switch
        {
            "EST" or "EDT" or "EASTERN" => timezoneId == "America/New_York",
            "CST" or "CDT" or "CENTRAL" => timezoneId == "America/Chicago",
            "MST" or "MDT" or "MOUNTAIN" => timezoneId == "America/Denver",
            "PST" or "PDT" or "PACIFIC" => timezoneId == "America/Los_Angeles",
            "GMT" or "UTC" => timezoneId == "UTC",
            "JST" => timezoneId == "Asia/Tokyo",
            "CET" or "CEST" => timezoneId == "Europe/Paris",
            "BST" => timezoneId == "Europe/London",
            "IST" => timezoneId == "Asia/Kolkata",
            "AEST" or "AEDT" => timezoneId == "Australia/Sydney",
            _ => false
        };
    }

    /// <summary>
    ///     Generates an abbreviation from a timezone name.
    /// </summary>
    private static string GetAbbreviation(string timezoneName)
    {
        if (string.IsNullOrEmpty(timezoneName))
            return string.Empty;

        // Handle common timezone name patterns
        var words = timezoneName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        switch (words.Length)
        {
            // For "Eastern Standard Time" -> "EST", "Pacific Daylight Time" -> "PDT", etc.
            case >= 3 when
                (words[1].Equals("Standard", StringComparison.OrdinalIgnoreCase) ||
                 words[1].Equals("Daylight", StringComparison.OrdinalIgnoreCase)) &&
                words[2].Equals("Time", StringComparison.OrdinalIgnoreCase):
            {
                var first = words[0][0];
                var second = words[1][0];
                var third = words[2][0];
                return $"{first}{second}{third}".ToUpperInvariant();
            }
            // For shorter names, take first letter of each word
            case >= 2:
            {
                var result = "";
                foreach (var word in words.Take(3)) // Max 3 letters
                {
                    if (word.Length > 0)
                        result += char.ToUpperInvariant(word[0]);
                }

                return result;
            }
            default:
                // Single word - return as is if short, or first 3 letters if long
                return timezoneName.Length <= 4
                    ? timezoneName.ToUpperInvariant()
                    : timezoneName[..3].ToUpperInvariant();
        }
    }


    /// <summary>
    ///     Gets a random image from a specified category using the Martine API.
    /// </summary>
    /// <param name="tag">The category of image to fetch.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the image data.</returns>
    /// <exception cref="ApiException">Thrown when the API request fails.</exception>
    /// <remarks>
    ///     This method fetches random images using the Martine API, selecting from multiple themed subreddits per category.
    /// </remarks>
    public async Task<RedditPost> GetRandomImageAsync(ImageTag tag)
    {
        var subreddit = tag switch
        {
            ImageTag.Food => rng.Next() switch
            {
                var n when n % 5 == 0 => "FoodPorn",
                var n when n % 5 == 1 => "food",
                var n when n % 5 == 2 => "cooking",
                var n when n % 5 == 3 => "recipes",
                _ => "culinary"
            },
            ImageTag.Dogs => rng.Next() switch
            {
                var n when n % 6 == 0 => "dogpictures",
                var n when n % 6 == 1 => "rarepuppers",
                var n when n % 6 == 2 => "puppies",
                var n when n % 6 == 3 => "dogs",
                var n when n % 6 == 4 => "dogswithjobs",
                _ => "WhatsWrongWithYourDog"
            },
            ImageTag.Cats => rng.Next() switch
            {
                var n when n % 7 == 0 => "cats",
                var n when n % 7 == 1 => "CatPictures",
                var n when n % 7 == 2 => "catpics",
                var n when n % 7 == 3 => "SupermodelCats",
                var n when n % 7 == 4 => "CatsStandingUp",
                var n when n % 7 == 5 => "CatsInSinks",
                _ => "TheCatTrapIsWorking"
            },
            ImageTag.Birds => rng.Next() switch
            {
                var n when n % 5 == 0 => "birdpics",
                var n when n % 5 == 1 => "parrots",
                var n when n % 5 == 2 => "birding",
                var n when n % 5 == 3 => "whatsthisbird",
                _ => "Birbs"
            },
            _ => throw new ArgumentException($"Unsupported image tag: {tag}", nameof(tag))
        };

        try
        {
            return await martineApi.RedditApi.GetRandomFromSubreddit(subreddit, Toptype.month).ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            logger.LogError("Failed to fetch image from Martine API for tag {Tag} (subreddit: r/{Subreddit}): {Error}",
                tag,
                subreddit,
                ex.HasContent ? ex.Content : "No Content");
            throw;
        }
    }

    /// <summary>
    ///     Automatically translates the input string from one language to another.
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
    ///     Translates the input text to the specified languages.
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
    ///     Performs a search using the DAPI (Danbooru) API.
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
    ///     Retrieves the blacklisted tags for the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A HashSet containing the blacklisted tags for the guild.</returns>
    private async Task<HashSet<string>> GetBlacklistedTags(ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query with GuildId filter
        var tags = await dbContext.NsfwBlacklistedTags
            .Where(t => t.GuildId == guildId)
            .Select(x => x.Tag)
            .ToListAsync();

        return tags.Count != 0 ? tags.ToHashSet() : [];
    }

    /// <summary>
    ///     Checks if a given Reddit is marked as NSFW.
    /// </summary>
    /// <param name="reddit">The Reddit to check.</param>
    /// <returns>True if the Reddit is marked as NSFW, otherwise false.</returns>
    public bool NsfwCheck(string reddit)
    {
        return nsfwreddits.Contains(reddit, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Retrieves a "Yo Mama" joke.
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
                yomamaJokes.AddRange(newList.SecureShuffle());
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
    ///     Retrieves a random joke.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation, returning a tuple containing the setup and punchline of the
    ///     joke.
    /// </returns>
    public async Task<(string? Setup, string Punchline)> GetRandomJoke()
    {
        using var http = httpFactory.CreateClient();
        var res = await http.GetStringAsync("https://official-joke-api.appspot.com/random_joke").ConfigureAwait(false);

        var resObj = JsonSerializer.Deserialize<JokeResponse>(res, CachedJsonOptions);
        return (resObj?.Setup, resObj?.Punchline ?? string.Empty);
    }

    /// <summary>
    ///     Retrieves a Chuck Norris joke.
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
    ///     Retrieves movie data asynchronously from Wikipedia.
    /// </summary>
    /// <param name="name">The name of the movie.</param>
    /// <returns>A task representing the asynchronous operation, returning the movie data.</returns>
    public Task<WikiMovie?> GetMovieDataAsync(string name)
    {
        name = name.Trim().ToLowerInvariant();
        return cache.GetOrAddCachedDataAsync($"Mewdeko_movie_{name}",
            GetMovieDataFactory,
            name,
            TimeSpan.FromDays(1));
    }

    private async Task<WikiMovie?> GetMovieDataFactory(string name)
    {
        using var http = httpFactory.CreateClient();

        // First search for the movie
        var searchUrl =
            $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(name)}%20film&format=json&prop=info&inprop=url";
        var searchResponse = await http.GetStringAsync(searchUrl).ConfigureAwait(false);
        var searchResult = JsonSerializer.Deserialize<WikiSearchResponse>(searchResponse);

        if (searchResult?.Query?.Search == null || searchResult.Query.Search.Count == 0)
            return null;

        // Get the full page data
        var pageId = searchResult.Query.Search[0].PageId;
        var contentUrl =
            $"https://en.wikipedia.org/w/api.php?action=query&prop=extracts|pageimages|info&pithumbsize=500&inprop=url&explaintext=1&pageids={pageId}&format=json";
        var contentResponse = await http.GetStringAsync(contentUrl).ConfigureAwait(false);
        var contentResult = JsonSerializer.Deserialize<WikiContentResponse>(contentResponse);

        if (!contentResult?.Query?.Pages?.ContainsKey(pageId.ToString()) ?? true)
            return null;

        var page = contentResult.Query.Pages[pageId.ToString()];

        // Parse the year from the text
        var yearMatch = Regex.Match(page.Extract, @"(?:released|premiered)[^\d]*(\d{4})");
        var year = yearMatch.Success ? yearMatch.Groups[1].Value : "N/A";

        return new WikiMovie
        {
            Title = page.Title.Replace("(film)", "").Trim(),
            Year = year,
            Plot = GetFirstParagraph(page.Extract),
            Url = page.FullUrl,
            ImageUrl = page.Thumbnail?.Source
        };
    }

    private string GetFirstParagraph(string extract)
    {
        var firstParagraph = extract.Split("\n\n").FirstOrDefault() ?? "";
        return firstParagraph.Length > 1000 ? firstParagraph[..1000] + "..." : firstParagraph;
    }

    /// <summary>
    ///     Retrieves detailed information about a Steam game by its name.
    /// </summary>
    /// <param name="query">The name of the game to search for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the SteamGameInfo if found;
    ///     otherwise, null.
    /// </returns>
    public async Task<SteamGameInfo?> GetSteamGameInfoByName(string query)
    {
        query = query.Trim().ToLowerInvariant();
        var searchCacheKey = $"steam_game_search_{query}";

        // First try to get the AppId from search
        var searchResult = await cache.GetOrAddCachedDataAsync(
            searchCacheKey,
            async _ =>
            {
                using var http = httpFactory.CreateClient();
                var response =
                    await http.GetAsync(
                        $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(query)}&l=en&cc=US");

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<StoreSearchResponse>(content, CachedJsonOptions);

                if (result?.Items == null || !result.Items.Any())
                    return null;

                // Return the most relevant match
                return result.Items[0];
            },
            default(string),
            TimeSpan.FromHours(1)
        );

        if (searchResult == null)
            return null;

        // Then get detailed info
        return await cache.GetOrAddCachedDataAsync(
            $"steam_game_details_{searchResult.Id}",
            async _ =>
            {
                using var http = httpFactory.CreateClient();
                var detailsStr =
                    await http.GetStringAsync(
                        $"https://store.steampowered.com/api/appdetails?appids={searchResult.Id}");

                var response =
                    JsonSerializer.Deserialize<Dictionary<string, AppDetailsResponse>>(detailsStr, CachedJsonOptions);

                if (response?.TryGetValue(searchResult.Id.ToString(), out var gameDetails) == true &&
                    gameDetails.Success)
                {
                    var data = gameDetails.Data;
                    data.Price = searchResult.Price; // Include the price from search result as it's more reliable
                    data.Metascore = searchResult.Metascore; // Include metascore from search as it's already parsed
                    return data;
                }

                return null;
            },
            default(string),
            TimeSpan.FromHours(6)
        );
    }


    /// <summary>
    ///     Performs a Google search asynchronously.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <returns>A task representing the asynchronous operation, returning the Google search results.</returns>
    public async Task<GoogleSearchResultData?> GoogleSearchAsync(string query)
    {
        try
        {
            var encodedQuery = WebUtility.UrlEncode(query ?? "");
            query = encodedQuery?.Replace(' ', '+') ?? "";

            var fullQueryLink = $"https://www.google.com/search?q={query}&ie=utf-8&oe=utf-8&num=10&gbv=1";

            using var msg = new HttpRequestMessage(HttpMethod.Get, fullQueryLink);
            msg.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; bot)");
            msg.Headers.Add("Accept", "text/html");
            msg.Headers.Add("Accept-Language", "en");

            using var http = httpFactory.CreateClient();
            http.DefaultRequestHeaders.Clear();
            using var response = await http.SendAsync(msg).ConfigureAwait(false);

            var htmlContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var content = new MemoryStream(Encoding.UTF8.GetBytes(htmlContent));
            using var document = await GoogleParser.ParseDocumentAsync(content).ConfigureAwait(false);

            if (document.Title?.Contains("robot") == true ||
                document.Body?.TextContent?.Contains("captcha") == true ||
                document.Body?.TextContent?.Contains("unusual traffic") == true ||
                document.Body?.TextContent?.Contains("enablejs") == true ||
                htmlContent.Contains("enablejs") ||
                htmlContent.Contains("noscript") ||
                (document.Title == "Google Search" && htmlContent.Length < 10000))
            {
                return await DuckDuckGoSearchAsync(query).ConfigureAwait(false);
            }

            var elemsList = document.QuerySelectorAll("div.g").ToList();
            IEnumerable<IElement> elems = elemsList;

            if (!elems.Any())
                elems = document.QuerySelectorAll(".g");

            if (!elems.Any())
                elems = document.QuerySelectorAll("[data-hveid]");

            if (!elems.Any())
                elems = document.QuerySelectorAll(".tF2Cxc");

            if (!elems.Any())
            {
                var allDivs = document.QuerySelectorAll("div");
                elems = allDivs.Where(d =>
                    d.QuerySelector("a") != null &&
                    d.QuerySelector("h3") != null);
            }

            var resultsElem = document.QuerySelector("#resultStats")
                              ?? document.QuerySelector("#result-stats")
                              ?? document.QuerySelector(".LHJvCe")
                              ?? document.QuerySelectorAll("div")
                                  .FirstOrDefault(x => x.TextContent?.Contains("results") == true);
            var totalResults = resultsElem?.TextContent;

            if (!elems.Any())
                return default;

            var results = elems.Select(elem =>
                {
                    // Try multiple approaches to find the link and title
                    var linkElem = elem.QuerySelector("a") as IHtmlAnchorElement;
                    var href = linkElem?.Href;

                    // Try different selectors for title
                    var name = elem.QuerySelector("h3")?.TextContent
                               ?? elem.QuerySelector(".LC20lb")?.TextContent
                               ?? elem.QuerySelector("[role='heading']")?.TextContent
                               ?? linkElem?.TextContent;

                    if (href == null || string.IsNullOrWhiteSpace(name))
                        return null;

                    // Try to find description text - look for common description selectors
                    var txt = elem.QuerySelector(".VwiC3b")?.TextContent // Current description class
                              ?? elem.QuerySelector(".s")?.TextContent // Older description class
                              ?? elem.QuerySelector(".st")?.TextContent // Alternative description class
                              ?? elem.QuerySelectorAll("div").Skip(1).FirstOrDefault()?.TextContent; // Fallback

                    if (string.IsNullOrWhiteSpace(txt))
                        txt = "No description available";

                    return new GoogleSearchResult(name, href, txt);
                })
                .Where(x => x != null)
                .Take(10) // Limit results to prevent too many
                .ToList();

            return new GoogleSearchResultData(
                results.AsReadOnly(),
                fullQueryLink,
                totalResults);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    ///     Performs a DuckDuckGo search asynchronously.
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
///     Represents already posted Reddit posts.
/// </summary>
public record RedditCache
{
    /// <summary>
    ///     The guild where the post was posted.
    /// </summary>
    public IGuild Guild { get; set; }

    /// <summary>
    ///     The url of the post.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
///     Represents the result data of a Google search operation.
/// </summary>
public class GoogleSearchResultData
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GoogleSearchResultData" /> class.
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
    ///     Gets the list of search results.
    /// </summary>
    public IReadOnlyList<GoogleSearchResult> Results { get; }

    /// <summary>
    ///     Gets the full query link used for the search.
    /// </summary>
    public string FullQueryLink { get; }

    /// <summary>
    ///     Gets the total number of search results.
    /// </summary>
    public string TotalResults { get; }
}

/// <summary>
///     Enumerates the possible time-related errors.
/// </summary>
public enum TimeErrors
{
    /// <summary>
    ///     Invalid input error.
    /// </summary>
    InvalidInput,

    /// <summary>
    ///     API key missing error.
    /// </summary>
    ApiKeyMissing,

    /// <summary>
    ///     Not found error.
    /// </summary>
    NotFound,

    /// <summary>
    ///     Unknown error.
    /// </summary>
    Unknown
}

/// <summary>
///     Represents data related to a ship.
/// </summary>
public class ShipCache
{
    /// <summary>
    ///     Gets or sets the first user ID.
    /// </summary>
    public ulong User1 { get; set; }

    /// <summary>
    ///     Gets or sets the second user ID.
    /// </summary>
    public ulong User2 { get; set; }

    /// <summary>
    ///     Gets or sets the score of the ship.
    /// </summary>
    public int Score { get; set; }
}