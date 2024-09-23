using System.Globalization;
using System.Net;
using System.Net.Http;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using GScraper;
using GScraper.DuckDuckGo;
using GScraper.Google;
using MartineApiNet;
using MartineApiNet.Enums;
using MartineApiNet.Models.Images;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Searches.Common;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Services.Settings;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Refit;
using Serilog;
using SkiaSharp;

namespace Mewdeko.Modules.Searches;

/// <summary>
/// The Searches module provides commands for searching and retrieving various types of information. It includes commands for searching memes, Reddit posts, weather, and more.
/// </summary>
/// <param name="creds">The bot credentials.</param>
/// <param name="google">The Google API service.</param>
/// <param name="factory">The HTTP client factory.</param>
/// <param name="cache">The memory cache service.</param>
/// <param name="tzSvc">The guild timezone service.</param>
/// <param name="serv">The interactive service.</param>
/// <param name="martineApi">The Martine API service.</param>
/// <param name="toneTagService">The ToneTag service.</param>
/// <param name="config">The bot configuration service.</param>
/// <param name="nsfwSpy">The NSFW spy service.</param>
public partial class Searches(
    IBotCredentials creds,
    IGoogleApiService google,
    IHttpClientFactory factory,
    IMemoryCache cache,
    GuildTimezoneService tzSvc,
    InteractiveService serv,
    MartineApi martineApi,
    ToneTagService toneTagService,
    BotConfigService config)
    : MewdekoModuleBase<SearchesService>
{
    private static readonly ConcurrentDictionary<string, string> CachedShortenedLinks = new();

    /// <summary>
    /// Fetches and displays a random meme from Reddit.
    /// </summary>
    /// <remarks>
    /// This command uses the MartineApi to retrieve a random meme from a predefined list of subreddits.
    /// It displays the meme in an embed format, including the title, author, subreddit, and a link to the original post.
    /// </remarks>
    /// <example>
    /// <code>.meme</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Meme()
    {
        var msg = await ctx.Channel.SendConfirmAsync($"{config.Data.LoadingEmote} Fetching random meme...")
            .ConfigureAwait(false);
        var image = await martineApi.RedditApi.GetRandomMeme(Toptype.year).ConfigureAwait(false);

        var button = new ComponentBuilder().WithButton("Another!", $"meme:{ctx.User.Id}");
        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = $"u/{image.Data.Author.Name}"
            },
            Description = $"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})",
            Footer = new EmbedFooterBuilder
            {
                Text =
                    $"{image.Data.Upvotes} Upvotes {image.Data.Downvotes} Downvotes | r/{image.Data.Subreddit.Name} | Powered by MartineApi"
            },
            ImageUrl = image.Data.ImageUrl,
            Color = Mewdeko.OkColor
        };
        await msg.ModifyAsync(x =>
        {
            x.Embed = em.Build();
            x.Components = button.Build();
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches and displays a random post from a specified subreddit.
    /// </summary>
    /// <param name="subreddit">The subreddit from which to fetch a random post.</param>
    /// <remarks>
    /// This command checks if the specified subreddit is marked as NSFW. If it is not, it fetches a random post.
    /// It displays the post in an embed format, including the title, author, subreddit, and a link to the original post.
    /// </remarks>
    /// <example>
    /// <code>.randomreddit sylveon</code>
    /// </example>
    [Cmd, Aliases]
    public async Task RandomReddit(string subreddit)
    {
        var msg = await ctx.Channel.SendConfirmAsync("Checking if the subreddit is nsfw...").ConfigureAwait(false);
        if (Service.NsfwCheck(subreddit))
        {
            var emt = new EmbedBuilder
            {
                Description = "This subreddit is nsfw!", Color = Mewdeko.ErrorColor
            };
            await msg.ModifyAsync(x => x.Embed = emt.Build()).ConfigureAwait(false);
            return;
        }

        var button = new ComponentBuilder().WithButton("Another!", $"randomreddit:{subreddit}.{ctx.User.Id}");
        RedditPost image;
        try
        {
            image = await martineApi.RedditApi.GetRandomFromSubreddit(subreddit).ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            await msg.DeleteAsync().ConfigureAwait(false);
            await ctx.Channel.SendErrorAsync("Seems like that subreddit wasn't found, please try something else!",
                    Config)
                .ConfigureAwait(false);
            Log.Error(
                $"Seems that Meme fetching has failed. Here's the error:\nCode: {ex.StatusCode}\nContent: {(ex.HasContent ? ex.Content : "No Content.")}");
            return;
        }

        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = $"u/{image.Data.Author.Name}"
            },
            Description = $"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})",
            Footer = new EmbedFooterBuilder
            {
                Text = $"{image.Data.Upvotes} Upvotes! | r/{image.Data.Subreddit.Name} Powered by martineAPI"
            },
            ImageUrl = image.Data.ImageUrl,
            Color = Mewdeko.OkColor
        };
        await msg.ModifyAsync(x =>
        {
            x.Embed = em.Build();
            x.Components = button.Build();
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Displays a RIP image with the user's name and avatar.
    /// </summary>
    /// <param name="usr">The user for whom to generate the RIP image.</param>
    /// <remarks>
    /// This command generates a "Rest In Peace" image featuring the specified user's name and avatar.
    /// It then sends this image in the channel where the command was used.
    /// </remarks>
    /// <example>
    /// <code>.rip @username</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Rip([Remainder] IGuildUser usr)
    {
        var av = usr.RealAvatarUrl();
        var picStream =
            await Service.GetRipPictureAsync(usr.Nickname ?? usr.Username, av).ConfigureAwait(false);
        await using var _ = picStream.ConfigureAwait(false);
        await ctx.Channel.SendFileAsync(
                picStream,
                "rip.png", $"Rip {Format.Bold(usr.ToString())} \n\t- {Format.Italics(ctx.User.ToString())}")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches and displays weather information for a given location query.
    /// </summary>
    /// <param name="query">The location query to search for weather information.</param>
    /// <remarks>
    /// This command searches for current weather conditions based on the provided location query.
    /// It displays the weather information in an embed format, including temperature, humidity, wind speed, and more.
    /// </remarks>
    /// <example>
    /// <code>.weather New York</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Weather([Remainder] string query)
    {
        if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
            return;

        var embed = new EmbedBuilder();
        var data = await Service.GetWeatherDataAsync(query).ConfigureAwait(false);

        if (data == null)
        {
            embed.WithDescription(GetText("city_not_found"))
                .WithErrorColor();
        }
        else
        {
            var f = StandardConversions.CelsiusToFahrenheit;

            var tz = Context.Guild is null
                ? TimeZoneInfo.Utc
                : tzSvc.GetTimeZoneOrUtc(Context.Guild.Id);
            var sunrise = data.Sys.Sunrise.ToUnixTimestamp();
            var sunset = data.Sys.Sunset.ToUnixTimestamp();
            sunrise = sunrise.ToOffset(tz.GetUtcOffset(sunrise));
            sunset = sunset.ToOffset(tz.GetUtcOffset(sunset));
            var timezone = $"UTC{sunrise:zzz}";

            embed.AddField(fb =>
                    fb.WithName($"🌍 {Format.Bold(GetText("location"))}")
                        .WithValue(
                            $"[{data.Name}, {data.Sys.Country}](https://openweathermap.org/city/{data.Id})")
                        .WithIsInline(true))
                .AddField(fb =>
                    fb.WithName($"📏 {Format.Bold(GetText("latlong"))}")
                        .WithValue($"{data.Coord.Lat}, {data.Coord.Lon}").WithIsInline(true))
                .AddField(fb =>
                    fb.WithName($"☁ {Format.Bold(GetText("condition"))}")
                        .WithValue(string.Join(", ", data.Weather.Select(w => w.Main))).WithIsInline(true))
                .AddField(fb =>
                    fb.WithName($"😓 {Format.Bold(GetText("humidity"))}").WithValue($"{data.Main.Humidity}%")
                        .WithIsInline(true))
                .AddField(fb =>
                    fb.WithName($"💨 {Format.Bold(GetText("wind_speed"))}").WithValue($"{data.Wind.Speed} m/s")
                        .WithIsInline(true))
                .AddField(fb =>
                    fb.WithName($"🌡 {Format.Bold(GetText("temperature"))}")
                        .WithValue($"{data.Main.Temp:F1}°C / {f(data.Main.Temp):F1}°F").WithIsInline(true))
                .AddField(fb =>
                    fb.WithName($"🔆 {Format.Bold(GetText("min_max"))}")
                        .WithValue(
                            $"{data.Main.TempMin:F1}°C - {data.Main.TempMax:F1}°C\n{f(data.Main.TempMin):F1}°F - {f(data.Main.TempMax):F1}°F")
                        .WithIsInline(true))
                .AddField(fb =>
                    fb.WithName($"🌄 {Format.Bold(GetText("sunrise"))}").WithValue($"{sunrise:HH:mm} {timezone}")
                        .WithIsInline(true))
                .AddField(fb =>
                    fb.WithName($"🌇 {Format.Bold(GetText("sunset"))}").WithValue($"{sunset:HH:mm} {timezone}")
                        .WithIsInline(true))
                .WithOkColor()
                .WithFooter(efb =>
                    efb.WithText("Powered by openweathermap.org")
                        .WithIconUrl($"https://openweathermap.org/img/w/{data.Weather[0].Icon}.png"));
        }

        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }

    /// <summary>
    /// Displays the current time in a specified location.
    /// </summary>
    /// <param name="query">The location query to search for the current time.</param>
    /// <remarks>
    /// This command searches for the current time based on the provided location query.
    /// It displays the time and timezone information in the channel where the command was used.
    /// </remarks>
    /// <example>
    /// <code>.time Tokyo</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Time([Remainder] string query)
    {
        if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
            return;

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

        var (data, err) = await Service.GetTimeDataAsync(query).ConfigureAwait(false);
        if (err is not null)
        {
            var errorKey = err switch
            {
                TimeErrors.ApiKeyMissing => "api_key_missing",
                TimeErrors.InvalidInput => "invalid_input",
                TimeErrors.NotFound => "not_found",
                _ => "error_occured"
            };

            await ReplyErrorLocalizedAsync(errorKey).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(data.TimeZoneName))
        {
            await ReplyErrorLocalizedAsync("timezone_db_api_key").ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(GetText("time_new"))
            .WithDescription(Format.Code(data.Time.ToString(CultureInfo.InvariantCulture)))
            .AddField(GetText("location"), string.Join('\n', data.Address.Split(", ")), true)
            .AddField(GetText("timezone"), data.TimeZoneName, true);

        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Searches for YouTube videos based on a provided query and displays the results.
    /// </summary>
    /// <param name="query">The search query to find YouTube videos.</param>
    /// <remarks>
    /// This command utilizes the Google API to search for YouTube videos matching the provided query.
    /// It presents the search results in a paginated format, allowing users to browse through video titles and links.
    /// </remarks>
    /// <example>
    /// <code>.youtube query</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Youtube([Remainder] string query)
    {
        if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
            return;

        var result = await google.GetVideoLinksByKeywordAsync(query).ConfigureAwait(false);
        if (!result.Any())
        {
            await ReplyErrorLocalizedAsync("no_results").ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithMaxPageIndex(result.Length - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithDescription(result[page].Snippet.Description.TrimTo(2048))
                .WithAuthor(new EmbedAuthorBuilder().WithName($"YouTube Search for {query.TrimTo(40)}")
                    .WithIconUrl("https://cdn.mewdeko.tech/YouTube.png"))
                .WithTitle(result[page].Snippet.Title)
                .WithUrl($"https://www.youtube.com/watch?v={result[page].Id.VideoId}")
                .WithImageUrl(result[page].Snippet.Thumbnails.High.Url)
                .WithColor(new Color(255, 0, 0));
        }
    }

    /// <summary>
    /// Fetches and displays information about a movie from IMDb based on the provided query.
    /// </summary>
    /// <param name="query">The movie title to search for on IMDb.</param>
    /// <remarks>
    /// This command searches IMDb for a movie matching the provided query and displays detailed information,
    /// including the plot, rating, genre, and year of release. The response is shown in an embed format with a link to the IMDb page.
    /// </remarks>
    /// <example>
    /// <code>.movie "The Matrix"</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Movie([Remainder] string? query = null)
    {
        if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
            return;

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

        var movie = await Service.GetMovieDataAsync(query).ConfigureAwait(false);
        if (movie == null)
        {
            await ReplyErrorLocalizedAsync("imdb_fail").ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
            .WithTitle(movie.Title)
            .WithUrl($"httpS://www.imdb.com/title/{movie.ImdbId}/")
            .WithDescription(movie.Plot.TrimTo(1000))
            .AddField(efb => efb.WithName("Rating").WithValue(movie.ImdbRating).WithIsInline(true))
            .AddField(efb => efb.WithName("Genre").WithValue(movie.Genre).WithIsInline(true))
            .AddField(efb => efb.WithName("Year").WithValue(movie.Year).WithIsInline(true))
            .WithImageUrl(movie.Poster)).ConfigureAwait(false);
    }

    /// <summary>
    /// Displays a random cat image.
    /// </summary>
    /// <remarks>
    /// This command fetches a random cat image from an online source and posts it in the channel.
    /// It's a fun command intended to provide users with a random cute cat picture to lighten the mood.
    /// </remarks>
    /// <example>
    /// <code>.randomcat</code>
    /// </example>
    [Cmd, Aliases]
    public Task RandomCat() => InternalRandomImage(SearchesService.ImageTag.Cats);

    /// <summary>
    /// Displays a random dog image.
    /// </summary>
    /// <remarks>
    /// Similar to the RandomCat command, this fetches and displays a random dog image.
    /// It aims to delight users with a surprise dog picture, contributing to a positive and engaging community atmosphere.
    /// </remarks>
    /// <example>
    /// <code>.randomdog</code>
    /// </example>
    [Cmd, Aliases]
    public Task RandomDog() => InternalRandomImage(SearchesService.ImageTag.Dogs);

    /// <summary>
    /// Displays a random food image.
    /// </summary>
    /// <remarks>
    /// Similar to the RandomCat command, this fetches and displays a random food image.
    /// It aims to delight users with a surprise food picture, contributing to a positive and engaging community atmosphere. Maybe even leaving some salivating. Maybe disgusted. Idk.
    /// </remarks>
    /// <example>
    /// <code>.randomfood</code>
    /// </example>
    [Cmd, Aliases]
    public Task RandomFood() => InternalRandomImage(SearchesService.ImageTag.Food);

    /// <summary>
    /// Displays birb.
    /// </summary>
    /// <remarks>
    /// Similar to the RandomCat command, this fetches and displays a random birb.
    /// It aims to delight users with a surprise birb picture, contributing to a positive and engaging community atmosphere.
    /// </remarks>
    /// <example>
    /// <code>.randombird</code>
    /// </example>
    [Cmd, Aliases]
    public Task RandomBird() => InternalRandomImage(SearchesService.ImageTag.Birds);


    private Task<IUserMessage> InternalRandomImage(SearchesService.ImageTag tag)
    {
        var url = Service.GetRandomImageUrl(tag);
        return ctx.Channel.EmbedAsync(new EmbedBuilder()
            .WithOkColor()
            .WithImageUrl(url.ToString()));
    }

    /// <summary>
    /// Performs an image search using Google and DuckDuckGo, then filters out NSFW results.
    /// </summary>
    /// <param name="query">The search query for the image.</param>
    /// <remarks>
    /// This command uses both Google and DuckDuckGo to perform an image search based on the provided query.
    /// It then filters out NSFW results using NsfwSpy and presents the safe images in a paginated embed format.
    /// </remarks>
    /// <example>
    /// <code>.image query</code>
    /// </example>
    [Cmd, Aliases, Ratelimit(20)]
    public async Task Image([Remainder] string query)
    {
        // Send a message indicating that images are being checked
        var checkingMessage = await ctx.Channel.SendConfirmAsync(GetText("image_checking")).ConfigureAwait(false);

        IEnumerable<IImageResult> images = null;
        string sourceName = null;
        string sourceIconUrl = null;

        // Try to get images from Google
        using (var gscraper = new GoogleScraper())
        {
            var search = await gscraper.GetImagesAsync(query, SafeSearchLevel.Strict).ConfigureAwait(false);
            search = search.Take(20);

            if (search.Any())
            {
                images = search;
                sourceName = "Google";
                sourceIconUrl = "https://www.google.com/favicon.ico";
            }
        }

        // If Google didn't return any results, try DuckDuckGo
        if (images == null)
        {
            using var dscraper = new DuckDuckGoScraper();
            var search2 = await dscraper.GetImagesAsync(query, SafeSearchLevel.Strict).ConfigureAwait(false);
            search2 = search2.Take(20);

            if (search2.Any())
            {
                images = search2;
                sourceName = "DuckDuckGo";
                sourceIconUrl = "https://duckduckgo.com/assets/logo_homepage.normal.v108.svg";
            }
        }

        // If no images were found by either scraper
        if (images == null)
        {
            await checkingMessage.DeleteAsync().ConfigureAwait(false);
            await ctx.Channel.SendErrorAsync(GetText("image_no_results"), Config)
                .ConfigureAwait(false);
            return;
        }

        // Now, filter the images using Safe Search Detection
        var imagesList = images.ToList(); // Convert to list for indexing

        var filteredImages = new List<IImageResult>();
        var tasks = imagesList.Select(async image =>
        {
            try
            {
                var safeSearchResult = await google.DetectSafeSearchAsync(image.Url);

                if (google.IsImageSafe(safeSearchResult))
                {
                    lock (filteredImages)
                    {
                        filteredImages.Add(image);
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., logging)
                Console.WriteLine($"Error processing image: {ex.Message}");
            }
        }).ToList();

        await Task.WhenAll(tasks);

        await checkingMessage.DeleteAsync().ConfigureAwait(false);

        if (filteredImages.Count==0)
        {
            await ctx.Channel.SendErrorAsync(GetText("image_no_safe_images"), Config)
                .ConfigureAwait(false);
            return;
        }

        // Proceed with displaying the images using a paginator
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(filteredImages.Count - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);
        return;

        Task<PageBuilder> PageFactory(int page)
        {
            var result = filteredImages.ElementAt(page);

            return Task.FromResult(new PageBuilder()
                .WithOkColor()
                .WithDescription(result.Title)
                .WithImageUrl(result.Url)
                .WithAuthor(
                    name: GetText("image_result_source", sourceName), // e.g., "Image Result from Google"
                    iconUrl: sourceIconUrl));
        }
    }


    /// <summary>
    /// Generates a Let Me Google That For You (LMGTFY) link for the provided query.
    /// </summary>
    /// <param name="ffs">The search query to be used in the LMGTFY link.</param>
    /// <remarks>
    /// This command takes a search query as input and generates a LMGTFY link.
    /// The LMGTFY link is then shortened using the google.ShortenUrl method and sent to the channel.
    /// If the provided query is null or whitespace, the command will return without sending a message.
    /// </remarks>
    /// <example>
    /// <code>.lmgtfy query</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Lmgtfy([Remainder] string? ffs = null)
    {
        if (!await ValidateQuery(ctx.Channel, ffs).ConfigureAwait(false))
            return;

        await ctx.Channel.SendConfirmAsync(
                $"<{await google.ShortenUrl($"https://lmgtfy.com/?q={Uri.EscapeDataString(ffs)}").ConfigureAwait(false)}>")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Shortens a provided URL using the goolnk.com API.
    /// </summary>
    /// <param name="query">The URL to be shortened.</param>
    /// <remarks>
    /// This command submits the specified URL to the goolnk.com API to generate a shortened version.
    /// The shortened URL is then returned and displayed in the channel. This is useful for sharing long URLs in a more concise format.
    /// </remarks>
    /// <example>
    /// <code>.shorten https://example.com/very/long/url</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Shorten([Remainder] string query)
    {
        if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
            return;

        query = query.Trim();
        if (!CachedShortenedLinks.TryGetValue(query, out var shortLink))
        {
            try
            {
                using var http = factory.CreateClient();
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://goolnk.com/api/v1/shorten");
                req.Content = new MultipartFormDataContent
                {
                    {
                        new StringContent(query), "url"
                    }
                };

                using var res = await http.SendAsync(req).ConfigureAwait(false);
                var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<ShortenData>(content);

                if (!string.IsNullOrWhiteSpace(data?.ResultUrl))
                    CachedShortenedLinks.TryAdd(query, data.ResultUrl);
                else
                    return;

                shortLink = data.ResultUrl;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error shortening a link: {Message}", ex.Message);
                return;
            }
        }

        await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithColor(Mewdeko.OkColor)
                .AddField(efb => efb.WithName(GetText("original_url"))
                    .WithValue($"<{query}>"))
                .AddField(efb => efb.WithName(GetText("short_url"))
                    .WithValue($"<{shortLink}>")))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Performs a general search using the Google or DuckDuckGo search engines and displays the results.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <remarks>
    /// This command conducts a web search using the specified query. If Google does not return results, DuckDuckGo is used as a fallback.
    /// Results are displayed in an embed format, providing users with a title, snippet, and link for each result.
    /// </remarks>
    /// <example>
    /// <code>.google search_terms</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Google([Remainder] string? query = null)
    {
        query = query?.Trim();
        if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
            return;

        _ = ctx.Channel.TriggerTypingAsync();

        var data = await Service.GoogleSearchAsync(query).ConfigureAwait(false);
        if (!data.TotalResults.Any())
        {
            data = await Service.DuckDuckGoSearchAsync(query).ConfigureAwait(false);
            if (data is null)
            {
                await ctx.Channel.SendErrorAsync(
                        "Neither google nor duckduckgo returned a result! Please search something else!", Config)
                    .ConfigureAwait(false);
                return;
            }
        }

        var desc = data.Results.Take(5).Select(res =>
            $@"[{res.Title}]({res.Link})
{res.Text.TrimTo(400 - res.Title.Length - res.Link.Length)}");

        var descStr = string.Join("\n\n", desc);

        var embed = new EmbedBuilder()
            .WithAuthor(eab => eab.WithName($"{GetText("search_for")} {query.TrimTo(50)}")
                .WithUrl(data.FullQueryLink)
                .WithIconUrl("https://i.imgur.com/G46fm8J.png"))
            .WithTitle(ctx.User.ToString())
            .WithFooter(efb => efb.WithText(data.TotalResults))
            .WithDescription(descStr)
            .WithOkColor();

        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches and displays information about a Magic: The Gathering card.
    /// </summary>
    /// <param name="search">The name or identifier of the Magic: The Gathering card to search for.</param>
    /// <remarks>
    /// Utilizing an external API, this command retrieves details about a specified Magic: The Gathering card,
    /// including its name, description, mana cost, types, and an image if available.
    /// The information is presented in an embed format.
    /// </remarks>
    /// <example>
    /// <code>.magicthegathering "Black Lotus"</code>
    /// </example>
    [Cmd, Aliases]
    public async Task MagicTheGathering([Remainder] string search)
    {
        if (!await ValidateQuery(ctx.Channel, search).ConfigureAwait(false))
            return;

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
        var card = await Service.GetMtgCardAsync(search).ConfigureAwait(false);

        if (card == null)
        {
            await ReplyErrorLocalizedAsync("card_not_found").ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder().WithOkColor()
            .WithTitle(card.Name)
            .WithDescription(card.Description)
            .WithImageUrl(card.ImageUrl)
            .AddField(efb => efb.WithName(GetText("store_url")).WithValue(card.StoreUrl).WithIsInline(true))
            .AddField(efb => efb.WithName(GetText("cost")).WithValue(card.ManaCost).WithIsInline(true))
            .AddField(efb => efb.WithName(GetText("types")).WithValue(card.Types).WithIsInline(true));

        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }

    /// <summary>
    /// Searches for and displays information about a Hearthstone card.
    /// </summary>
    /// <param name="name">The name of the Hearthstone card to search for.</param>
    /// <remarks>
    /// This command searches for a Hearthstone card by name and displays its image and flavor text, if available.
    /// It requires a valid Mashape API key set in the bot's configuration to access the Hearthstone API.
    /// </remarks>
    /// <example>
    /// <code>.hearthstone "Leeroy Jenkins"</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Hearthstone([Remainder] string name)
    {
        if (!await ValidateQuery(ctx.Channel, name).ConfigureAwait(false))
            return;

        if (string.IsNullOrWhiteSpace(creds.MashapeKey))
        {
            await ReplyErrorLocalizedAsync("mashape_api_missing").ConfigureAwait(false);
            return;
        }

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
        var card = await Service.GetHearthstoneCardDataAsync(name).ConfigureAwait(false);

        if (card == null)
        {
            await ReplyErrorLocalizedAsync("card_not_found").ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder().WithOkColor()
            .WithImageUrl(card.Img);

        if (!string.IsNullOrWhiteSpace(card.Flavor))
            embed.WithDescription(card.Flavor);

        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }

    /// <summary>
    /// Searches the Urban Dictionary and displays definitions for a given term.
    /// </summary>
    /// <param name="query">The term to search for on Urban Dictionary.</param>
    /// <remarks>
    /// This command fetches definitions from Urban Dictionary for the specified term.
    /// Results are presented in a paginated embed format, allowing users to browse through multiple definitions.
    /// </remarks>
    /// <example>
    /// <code>.urbandict "vaporeon copypasta"</code>
    /// </example>
    [Cmd, Aliases]
    public async Task UrbanDict([Remainder] string? query = null)
    {
        if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
            return;

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
        using var http = factory.CreateClient();
        var res = await http
            .GetStringAsync($"https://api.urbandictionary.com/v0/define?term={Uri.EscapeDataString(query)}")
            .ConfigureAwait(false);
        try
        {
            var items = JsonConvert.DeserializeObject<UrbanResponse>(res)?.List;
            if (items is { Length: > 0 })
            {
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(items.Length - 1)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                    .ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    var item = items[page];
                    return new PageBuilder().WithOkColor()
                        .WithUrl(item.Permalink)
                        .WithAuthor(
                            eab => eab.WithIconUrl("https://i.imgur.com/nwERwQE.jpg").WithName(item.Word))
                        .WithDescription(item.Definition);
                }
            }
            else
            {
                await ReplyErrorLocalizedAsync("ud_error").ConfigureAwait(false);
            }
        }
        catch
        {
            await ReplyErrorLocalizedAsync("ud_error").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Retrieves and displays a definition from the Pearson dictionary.
    /// </summary>
    /// <param name="word">The word to define.</param>
    /// <remarks>
    /// This command looks up a given word in the Pearson dictionary and displays its definition, part of speech,
    /// and an example sentence if available. Results are presented in a paginated format to navigate through multiple definitions.
    /// </remarks>
    /// <example>
    /// <code>.define "ubiquitous"</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Define([Remainder] string word)
    {
        if (!await ValidateQuery(ctx.Channel, word).ConfigureAwait(false))
            return;

        using var http = factory.CreateClient();
        try
        {
            var res = await cache.GetOrCreateAsync($"define_{word}", e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
                return http.GetStringAsync(
                    $"https://api.pearson.com/v2/dictionaries/entries?headword={WebUtility.UrlEncode(word)}");
            }).ConfigureAwait(false);

            var data = JsonConvert.DeserializeObject<DefineModel>(res);

            var datas = data.Results
                .Where(x => x.Senses is not null && x.Senses.Count > 0 && x.Senses[0].Definition is not null)
                .Select(x => (Sense: x.Senses[0], x.PartOfSpeech));

            if (!datas.Any())
            {
                Log.Warning("Definition not found: {Word}", word);
                await ReplyErrorLocalizedAsync("define_unknown").ConfigureAwait(false);
            }

            var col = datas.Select(tuple => (
                Definition: tuple.Sense.Definition is string
                    ? tuple.Sense.Definition.ToString()
                    : ((JArray)JToken.Parse(tuple.Sense.Definition.ToString())).First.ToString(),
                Example: tuple.Sense.Examples is null || tuple.Sense.Examples.Count == 0
                    ? string.Empty
                    : tuple.Sense.Examples[0].Text,
                Word: word,
                WordType: string.IsNullOrWhiteSpace(tuple.PartOfSpeech) ? "-" : tuple.PartOfSpeech
            )).ToList();

            Log.Information($"Sending {col.Count} definition for: {word}");

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(col.Count - 1)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var tuple = col.Skip(page).First();
                var embed = new PageBuilder()
                    .WithDescription(ctx.User.Mention)
                    .AddField(GetText("word"), tuple.Word, true)
                    .AddField(GetText("class"), tuple.WordType, true)
                    .AddField(GetText("definition"), tuple.Definition)
                    .WithOkColor();

                if (!string.IsNullOrWhiteSpace(tuple.Example))
                    embed.AddField(efb => efb.WithName(GetText("example")).WithValue(tuple.Example));

                return embed;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error retrieving definition data for: {Word}", word);
        }
    }


    /// <summary>
    /// Fetches and shares a random cat fact.
    /// </summary>
    /// <remarks>
    /// This command accesses a cat fact API to retrieve a random fact about cats.
    /// It's designed to provide fun and interesting information to cat enthusiasts.
    /// </remarks>
    /// <example>
    /// <code>.catfact</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Catfact()
    {
        using var http = factory.CreateClient();
        var response = await http.GetStringAsync("https://catfact.ninja/fact").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response))
            return;

        var fact = JObject.Parse(response)["fact"].ToString();
        await ctx.Channel.SendConfirmAsync($"🐈{GetText("catfact")}", fact).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs a reverse image search using an avatar link.
    /// </summary>
    /// <param name="usr">The user whos avatar to reverse search</param>
    /// <remarks>
    /// This command utilizes Google, TinEye, and Yandex reverse image search engines to find similar images or the source of the given image.
    /// It provides links to the search results on each platform, offering users multiple avenues to explore related or source images.
    /// </remarks>
    /// <example>
    /// <code>.revav @user</code>
    /// </example>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public Task Revav([Remainder] IGuildUser? usr = null)
    {
        usr ??= (IGuildUser)ctx.User;

        var av = usr.RealAvatarUrl();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

        return Revimg(av.ToString());
    }

    /// <summary>
    /// Performs a reverse image search using the provided image link.
    /// </summary>
    /// <param name="imageLink">The direct URL of the image to search for.</param>
    /// <remarks>
    /// This command utilizes Google, TinEye, and Yandex reverse image search engines to find similar images or the source of the given image.
    /// It provides links to the search results on each platform, offering users multiple avenues to explore related or source images.
    /// </remarks>
    /// <example>
    /// <code>.revimg "http://example.com/image.jpg"</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Revimg([Remainder] string? imageLink = null)
    {
        imageLink = imageLink?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(imageLink))
            return;

        // Google reverse image search link
        var googleLink = $"https://images.google.com/searchbyimage?image_url={imageLink}";

        // TinEye reverse image search link
        var tineyeLink = $"https://www.tineye.com/search?url={imageLink}";

        // Yandex reverse image search link
        var yandexLink = $"https://yandex.com/images/search?url={imageLink}&rpt=imageview";

        var response = $"Google: [Link]({googleLink})\nTinEye: [Link]({tineyeLink})\nYandex: [Link]({yandexLink})";

        await ctx.Channel.SendConfirmAsync(response).ConfigureAwait(false);
    }
    //
    // [Cmd, Aliases]
    // public async Task FakeTweet(string tweetText)
    // {
    //     // Gather user information
    //     var username = ctx.User.Username;
    //     var profileImageUrl = ctx.User.GetAvatarUrl();
    //
    //     // Download the user's profile image
    //     var httpClient = new HttpClient();
    //     var profileImageBytes = await httpClient.GetByteArrayAsync(profileImageUrl);
    //
    //     // Generate the fake tweet
    //     var tweetImageBytes = GenerateFakeTweet(username, profileImageBytes, tweetText);
    //
    //     var stream = new MemoryStream(tweetImageBytes);
    //     await ctx.Channel.SendFileAsync(stream, "fake_tweet.jpg");
    // }

    /// <summary>
    /// Searches for and displays an image based on the provided tag from Safebooru.
    /// </summary>
    /// <param name="tag">The tag to search for on Safebooru.</param>
    /// <remarks>
    /// This command uses the Safebooru API to fetch an image related to the specified tag.
    /// It is designed to provide safe-for-work images from a variety of anime and manga sources.
    /// The resulting image is posted in the channel where the command was used.
    /// </remarks>
    /// <example>
    /// <code>.safebooru tag_name</code>
    /// </example>
    [Cmd, Aliases]
    public Task Safebooru([Remainder] string? tag = null) =>
        InternalDapiCommand(ctx.Message, tag, DapiSearchType.Safebooru);

    /// <summary>
    /// Searches for and displays Wikipedia information based on the provided query.
    /// </summary>
    /// <param name="query">The search term for Wikipedia.</param>
    /// <remarks>
    /// This command searches Wikipedia for the specified query and returns the first matching page.
    /// If a page is found, it displays the page title and a link to the full article.
    /// </remarks>
    /// <example>
    /// <code>.wiki "Quantum mechanics" (nobody will ever actually search for this on discord)</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Wiki([Remainder] string? query = null)
    {
        query = query?.Trim();

        if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
            return;

        using var http = factory.CreateClient();
        var result = await http
            .GetStringAsync(
                $"https://en.wikipedia.org//w/api.php?action=query&format=json&prop=info&redirects=1&formatversion=2&inprop=url&titles={Uri.EscapeDataString(query)}")
            .ConfigureAwait(false);
        var data = JsonConvert.DeserializeObject<WikipediaApiModel>(result);
        if (data.Query.Pages[0].Missing || string.IsNullOrWhiteSpace(data.Query.Pages[0].FullUrl))
            await ReplyErrorLocalizedAsync("wiki_page_not_found").ConfigureAwait(false);
        else
            await ctx.Channel.SendMessageAsync(data.Query.Pages[0].FullUrl).ConfigureAwait(false);
    }

    /// <summary>
    /// Displays a color swatch based on the provided hexadecimal color codes.
    /// </summary>
    /// <param name="colors">An array of SKColor objects representing the colors to display.</param>
    /// <remarks>
    /// This command creates an image consisting of color swatches for each provided color code.
    /// It's useful for visualizing colors or sharing color schemes with others.
    /// </remarks>
    /// <example>
    /// <code>.color #FFFFFF #FF0000 #0000FF</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Color(params SKColor[] colors)
    {
        if (colors.Length == 0)
            return;

        var colorObjects = colors.Take(10)
            .ToArray();

        using var img = new SKBitmap(colorObjects.Length * 50, 50, SKColorType.Rgba8888, SKAlphaType.Premul);

        using (var canvas = new SKCanvas(img))
        {
            for (var i = 0; i < colorObjects.Length; i++)
            {
                var x = i * 50;
                var rect = new SKRect(x, 0, x + 50, 50);
                using var paint = new SKPaint
                {
                    Color = colorObjects[i], IsAntialias = true, Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(rect, paint);
            }
        }

        var data = SKImage.FromBitmap(img).Encode(SKEncodedImageFormat.Png, 100);
        var stream = data.AsStream();
        await ctx.Channel.SendFileAsync(stream, "colors.png").ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches and displays detailed information about a specific topic from a wikia.
    /// </summary>
    /// <param name="target">The target wikia site.</param>
    /// <param name="query">The search term for the wikia.</param>
    /// <remarks>
    /// This command searches the specified wikia for information related to the query.
    /// It returns the first relevant result, including the title and a link to the detailed page.
    /// </remarks>
    /// <example>
    /// <code>.wikia "starwars" "Darth Vader"</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Wikia(string target, [Remainder] string query)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(query))
        {
            await ReplyErrorLocalizedAsync("wikia_input_error").ConfigureAwait(false);
            return;
        }

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
        using var http = factory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        try
        {
            var res = await http.GetStringAsync(
                    $"https://{Uri.EscapeDataString(target)}.fandom.com/api.php?action=query&format=json&list=search&srsearch={Uri.EscapeDataString(query)}&srlimit=1")
                .ConfigureAwait(false);
            var items = JObject.Parse(res);
            var title = items["query"]?["search"]?.FirstOrDefault()?["title"]?.ToString();

            if (string.IsNullOrWhiteSpace(title))
            {
                await ReplyErrorLocalizedAsync("wikia_error").ConfigureAwait(false);
                return;
            }

            var url = Uri.EscapeDataString($"https://{target}.fandom.com/wiki/{title}");
            var response = $@"`{GetText("title")}` {title.SanitizeMentions()}
`{GetText("url")}:` {url}";
            await ctx.Channel.SendMessageAsync(response).ConfigureAwait(false);
        }
        catch
        {
            await ReplyErrorLocalizedAsync("wikia_error").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Searches for and displays Bible verses based on the book, chapter, and verse provided.
    /// </summary>
    /// <param name="book">The book of the Bible.</param>
    /// <param name="chapterAndVerse">The chapter and verse in the format "Chapter:Verse".</param>
    /// <remarks>
    /// This command retrieves and displays a specific Bible verse or set of verses.
    /// The response includes the text of the verses along with their book, chapter, and verse reference.
    /// </remarks>
    /// <example>
    /// <code>.bible "John" "3:16"</code>
    /// </example>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Bible(string book, string chapterAndVerse)
    {
        var obj = new BibleVerses();
        try
        {
            using var http = factory.CreateClient();
            var res = await http
                .GetStringAsync($"https://bible-api.com/{book} {chapterAndVerse}").ConfigureAwait(false);

            obj = JsonConvert.DeserializeObject<BibleVerses>(res);
        }
        catch
        {
            // ignored
        }

        if (obj.Error != null || !obj.Verses.Any())
        {
            await ctx.Channel.SendErrorAsync(obj.Error ?? "No verse found.", Config).ConfigureAwait(false);
        }
        else
        {
            var v = obj.Verses[0];
            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"{v.BookName} {v.Chapter}:{v.Verse}")
                .WithDescription(v.Text)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Searches for a game on Steam by name and provides a link to its Steam Store page.
    /// </summary>
    /// <param name="query">The name of the game to search for on Steam.</param>
    /// <remarks>
    /// This command searches for a game on Steam using the provided query. If the game is found, it returns a direct link to the game's page on the Steam Store.
    /// It's useful for quickly sharing Steam Store pages of games within the Discord channel.
    /// </remarks>
    /// <example>
    /// <code>.steam "Half-Life 3"</code>
    /// </example>
    [Cmd, Aliases]
    public async Task Steam([Remainder] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

        var appId = await Service.GetSteamAppIdByName(query).ConfigureAwait(false);
        if (appId == -1)
        {
            await ReplyErrorLocalizedAsync("not_found").ConfigureAwait(false);
            return;
        }

        //var embed = new EmbedBuilder()
        //    .WithOkColor()
        //    .WithDescription(gameData.ShortDescription)
        //    .WithTitle(gameData.Name)
        //    .WithUrl(gameData.Link)
        //    .WithImageUrl(gameData.HeaderImage)
        //    .AddField(efb => efb.WithName(GetText("genres")).WithValue(gameData.TotalEpisodes.ToString()).WithIsInline(true))
        //    .AddField(efb => efb.WithName(GetText("price")).WithValue(gameData.IsFree ? GetText("FREE") : game).WithIsInline(true))
        //    .AddField(efb => efb.WithName(GetText("links")).WithValue(gameData.GetGenresString()).WithIsInline(true))
        //    .WithFooter(efb => efb.WithText(GetText("recommendations", gameData.TotalRecommendations)));
        await ctx.Channel.SendMessageAsync($"https://store.steampowered.com/app/{appId}").ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves tone tags in a message and provides explanations for each identified tag.
    /// </summary>
    /// <param name="tag">The message containing tone tags to be resolved.</param>
    /// <remarks>
    /// Tone tags are short codes used to express the tone of a message. This command parses the message for known tone tags and returns their meanings to help clarify the intended tone of the message.
    /// This is particularly useful in text-based communication where conveying tone can be challenging.
    /// </remarks>
    /// <example>
    /// <code>.resolvetonetags "I'm happy to help! /s"</code>
    /// </example>
    [Cmd, Aliases]
    public async Task ResolveToneTags([Remainder] string tag)
    {
        var embed = toneTagService.GetEmbed(toneTagService.ParseTags(tag), ctx.Guild);
        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }

    private async Task InternalDapiCommand(IMessage umsg, string? tag, DapiSearchType type)
    {
        var channel = umsg.Channel;

        tag = tag?.Trim() ?? "";

        var imgObj = await Service.DapiSearch(tag, type, ctx.Guild?.Id).ConfigureAwait(false);

        if (imgObj == null)
        {
            await channel.SendErrorAsync($"{umsg.Author.Mention} {GetText("no_results")}", Config)
                .ConfigureAwait(false);
        }
        else
        {
            await channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription($"{umsg.Author.Mention} [{tag ?? "url"}]({imgObj.FileUrl})")
                .WithImageUrl(imgObj.FileUrl)
                .WithFooter(efb => efb.WithText(type.ToString()))).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Validates if the given query string is not null or whitespace.
    /// </summary>
    /// <param name="ch">The channel from which the command was invoked.</param>
    /// <param name="query">The query string to validate.</param>
    /// <returns>True if the query is valid, otherwise false.</returns>
    /// <remarks>
    /// This utility method checks if a query string provided in a command is valid. It ensures that commands requiring input do not proceed with empty or whitespace-only queries.
    /// </remarks>
    /// <example>
    /// This method is called internally by commands requiring input validation and does not have a direct command example.
    /// </example>
    public async Task<bool> ValidateQuery(IMessageChannel ch, string query)
    {
        if (!string.IsNullOrWhiteSpace(query))
            return true;

        await ErrorLocalizedAsync("specify_search_params").ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Demonstrates localized string responses in commands for testing purposes.
    /// </summary>
    /// <param name="input">The input string to localize, followed by optional arguments separated by "|".</param>
    /// <remarks>
    /// This command is designed for developers to test and demonstrate the localization of strings within the bot.
    /// It accepts an input string and optional arguments to format the localized message.
    /// </remarks>
    /// <example>
    /// <code>.testlocalize "greeting|world"</code>
    /// </example>
    [Cmd, Aliases]
    [RequireDragon, HelpDisabled]
    public async Task TestLocalize([Remainder] string input)
    {
        var sp = input.Split("|");
        if (sp[0].IsNullOrWhiteSpace())
        {
            await ErrorLocalizedAsync("__loctest_invalid");
            return;
        }

        await ConfirmLocalizedAsync(sp[0], sp.Skip(1).ToArray());
    }

    /// <summary>
    /// Represents the response data from a URL shortening service.
    /// </summary>
    /// <remarks>
    /// This class is designed to capture the shortened URL result from a URL shortening service's API response.
    /// It is utilized in the process of shortening URLs to make them more manageable and shareable.
    /// The `result_url` property in the JSON response maps to the `ResultUrl` property in this class.
    /// </remarks>
    public class ShortenData
    {
        /// <summary>
        /// Gets or sets the shortened URL result from the URL shortening service.
        /// </summary>
        /// <value>
        /// The shortened URL as a string.
        /// </value>
        [JsonProperty("result_url")]
        public string ResultUrl { get; set; }
    }
}