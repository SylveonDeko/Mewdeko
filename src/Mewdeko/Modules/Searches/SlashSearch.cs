using System.Net;
using System.Net.Http;
using System.Text.Json;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using GScraper;
using GScraper.DuckDuckGo;
using GScraper.Google;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Searches.Common;
using Mewdeko.Modules.Searches.Services;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using PokeApiNet;
using SkiaSharp;

namespace Mewdeko.Modules.Searches;

/// <summary>
///     Slash commands for searching and retrieving various types of information such as web results, images,
///     definitions, weather, time, movies, games and more.
/// </summary>
/// <param name="google">The Google API service.</param>
/// <param name="factory">The HTTP client factory.</param>
/// <param name="cache">The memory cache service.</param>
/// <param name="interactivity">The interactive service used for pagination.</param>
/// <param name="logger">The logger instance for structured logging.</param>
[Group("search", "Search the web, images, wikis, weather, games and more")]
public partial class SlashSearch(
    IGoogleApiService google,
    IHttpClientFactory factory,
    IMemoryCache cache,
    InteractiveService interactivity,
    ILogger<SlashSearch> logger)
    : MewdekoSlashModuleBase<SearchesService>
{
    private static readonly ConcurrentDictionary<string, string> CachedShortenedLinks = new();
    private readonly PokeApiClient pokeClient = new();

    /// <summary>
    ///     Performs a general search using the Google or DuckDuckGo search engines and displays the results.
    /// </summary>
    /// <param name="query">The search query.</param>
    [SlashCommand("google", "Search the web with Google or DuckDuckGo")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Google([Summary("query", "What to search for")] string query)
    {
        query = query.Trim();
        if (!await ValidateQuery(query).ConfigureAwait(false))
            return;

        await DeferAsync().ConfigureAwait(false);

        var data = await Service.GoogleSearchAsync(query).ConfigureAwait(false);
        if (!data.TotalResults.Any())
        {
            data = await Service.DuckDuckGoSearchAsync(query).ConfigureAwait(false);
            if (data is null)
            {
                await ErrorAsync(Strings.SearchNoResults(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }
        }

        var desc = data.Results.Take(5).Select(res =>
            $"""
             [{res.Title}]({res.Link})
             {res.Text.TrimTo(400 - res.Title.Length - res.Link.Length)}
             """);

        var descStr = string.Join("\n\n", desc);

        var embed = new EmbedBuilder()
            .WithAuthor(eab => eab.WithName($"{Strings.SearchFor(ctx.Guild.Id)} {query.TrimTo(50)}")
                .WithUrl(data.FullQueryLink)
                .WithIconUrl("https://i.imgur.com/G46fm8J.png"))
            .WithTitle(ctx.User.ToString())
            .WithFooter(efb => efb.WithText(data.TotalResults))
            .WithDescription(descStr)
            .WithOkColor();

        await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Performs an image search using Google and DuckDuckGo, then filters out NSFW results.
    /// </summary>
    /// <param name="query">The search query for the image.</param>
    [SlashCommand("image", "Search for images, filtered for safety")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [InteractionRatelimit(20)]
    public async Task Image([Summary("query", "What to search for")] string query)
    {
        await DeferAsync().ConfigureAwait(false);

        IEnumerable<IImageResult> images = null;
        string sourceName = null;
        string sourceIconUrl = null;

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

        if (images == null)
        {
            await ErrorAsync(Strings.ImageNoResults(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var imagesList = images.ToList();

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
                logger.LogWarning(ex, "Error processing image {Url}", image.Url);
            }
        }).ToList();

        await Task.WhenAll(tasks);

        if (filteredImages.Count == 0)
        {
            await ErrorAsync(Strings.ImageNoSafeImages(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(filteredImages.Count - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);
        return;

        Task<PageBuilder> PageFactory(int page)
        {
            var result = filteredImages.ElementAt(page);

            return Task.FromResult(new PageBuilder()
                .WithOkColor()
                .WithDescription(result.Title)
                .WithImageUrl(result.Url)
                .WithAuthor(Strings.ImageResultSource(ctx.Guild.Id, sourceName), sourceIconUrl));
        }
    }

    /// <summary>
    ///     Searches for and displays Wikipedia information based on the provided query.
    /// </summary>
    /// <param name="query">The search term for Wikipedia.</param>
    [SlashCommand("wiki", "Find a Wikipedia page")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Wiki([Summary("query", "The page to look for")] string query)
    {
        query = query.Trim();

        if (!await ValidateQuery(query).ConfigureAwait(false))
            return;

        await DeferAsync().ConfigureAwait(false);

        using var http = factory.CreateClient();
        var result = await http
            .GetStreamAsync(
                $"https://en.wikipedia.org//w/api.php?action=query&format=json&prop=info&redirects=1&formatversion=2&inprop=url&titles={Uri.EscapeDataString(query)}")
            .ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<WikipediaApiModel>(result);
        if (data.Query.Pages[0].Missing || string.IsNullOrWhiteSpace(data.Query.Pages[0].FullUrl))
            await ReplyErrorAsync(Strings.WikiPageNotFound(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ctx.Interaction.FollowupAsync(data.Query.Pages[0].FullUrl).ConfigureAwait(false);
    }

    /// <summary>
    ///     Fetches and displays detailed information about a specific topic from a wikia.
    /// </summary>
    /// <param name="target">The target wikia site.</param>
    /// <param name="query">The search term for the wikia.</param>
    [SlashCommand("wikia", "Search a fandom wikia")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Wikia([Summary("wikia", "The fandom wikia subdomain, e.g. starwars")] string target,
        [Summary("query", "What to search for")]
        string query)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(query))
        {
            await ReplyErrorAsync(Strings.WikiaInputError(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
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
                await ReplyErrorAsync(Strings.WikiaError(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var url = Uri.EscapeDataString($"https://{target}.fandom.com/wiki/{title}");
            var response = $"""
                            `{Strings.Title(ctx.Guild.Id)}` {title.SanitizeMentions()}
                            `{Strings.Url(ctx.Guild.Id)}:` {url}
                            """;
            await ctx.Interaction.FollowupAsync(response).ConfigureAwait(false);
        }
        catch
        {
            await ReplyErrorAsync(Strings.WikiaError(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Searches the Urban Dictionary and displays definitions for a given term.
    /// </summary>
    /// <param name="query">The term to search for on Urban Dictionary.</param>
    [SlashCommand("urban", "Look up a term on Urban Dictionary")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task UrbanDict([Summary("term", "The term to look up")] string query)
    {
        if (!await ValidateQuery(query).ConfigureAwait(false))
            return;

        await DeferAsync().ConfigureAwait(false);
        using var http = factory.CreateClient();
        var res = await http
            .GetStreamAsync($"https://api.urbandictionary.com/v0/define?term={Uri.EscapeDataString(query)}")
            .ConfigureAwait(false);
        try
        {
            var items = await JsonSerializer.DeserializeAsync<UrbanResponse>(res);
            if (items.List is { Length: > 0 })
            {
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(items.List.Length - 1)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                        TimeSpan.FromMinutes(60))
                    .ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    var item = items.List[page];
                    return new PageBuilder().WithOkColor()
                        .WithUrl(item.Permalink)
                        .WithAuthor(eab => eab.WithIconUrl("https://i.imgur.com/nwERwQE.jpg").WithName(item.Word))
                        .WithDescription(item.Definition);
                }
            }
            else
            {
                await ReplyErrorAsync(Strings.UdError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
        catch
        {
            await ReplyErrorAsync(Strings.UdError(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Retrieves and displays a definition from the Pearson dictionary.
    /// </summary>
    /// <param name="word">The word to define.</param>
    [SlashCommand("define", "Get the dictionary definition of a word")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Define([Summary("word", "The word to define")] string word)
    {
        if (!await ValidateQuery(word).ConfigureAwait(false))
            return;

        await DeferAsync().ConfigureAwait(false);

        using var http = factory.CreateClient();
        try
        {
            var res = await cache.GetOrCreateAsync($"define_{word}", e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
                return http.GetStreamAsync(
                    $"https://api.pearson.com/v2/dictionaries/entries?headword={WebUtility.UrlEncode(word)}");
            }).ConfigureAwait(false);

            var data = await JsonSerializer.DeserializeAsync<DefineModel>(res);

            var datas = data.Results
                .Where(x => x.Senses is not null && x.Senses.Count > 0 && x.Senses[0].Definition is not null)
                .Select(x => (Sense: x.Senses[0], x.PartOfSpeech));

            if (!datas.Any())
            {
                logger.LogWarning("Definition not found: {Word}", word);
                await ReplyErrorAsync(Strings.DefineUnknown(ctx.Guild.Id)).ConfigureAwait(false);
                return;
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

            logger.LogInformation("Sending {Count} definitions for: {Word}", col.Count, word);

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(col.Count - 1)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var tuple = col.Skip(page).First();
                var embed = new PageBuilder()
                    .WithDescription(ctx.User.Mention)
                    .AddField(Strings.Word(ctx.Guild.Id), tuple.Word, true)
                    .AddField(Strings.Class(ctx.Guild.Id), tuple.WordType, true)
                    .AddField(Strings.Definition(ctx.Guild.Id), tuple.Definition)
                    .WithOkColor();

                if (!string.IsNullOrWhiteSpace(tuple.Example))
                    embed.AddField(efb => efb.WithName(Strings.Example(ctx.Guild.Id)).WithValue(tuple.Example));

                return embed;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving definition data for: {Word}", word);
            await ReplyErrorAsync(Strings.DefineUnknown(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Searches for YouTube videos based on a provided query and displays the results.
    /// </summary>
    /// <param name="query">The search query to find YouTube videos.</param>
    [SlashCommand("youtube", "Search for YouTube videos")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Youtube([Summary("query", "What to search for")] string query)
    {
        if (!await ValidateQuery(query).ConfigureAwait(false))
            return;

        await DeferAsync().ConfigureAwait(false);

        var result = await google.GetVideoLinksByKeywordAsync(query).ConfigureAwait(false);
        if (!result.Any())
        {
            await ReplyErrorAsync(Strings.NoResults(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithMaxPageIndex(result.Length - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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
    ///     Fetches and displays information about a movie from IMDb based on the provided query.
    /// </summary>
    /// <param name="query">The movie title to search for on IMDb.</param>
    [SlashCommand("movie", "Look up a movie")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Movie([Summary("title", "The movie title")] string query)
    {
        if (!await ValidateQuery(query).ConfigureAwait(false))
            return;

        await DeferAsync().ConfigureAwait(false);

        var movie = await Service.GetMovieDataAsync(query).ConfigureAwait(false);
        if (movie == null)
        {
            await ReplyErrorAsync(Strings.ImdbFail(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor()
            .WithTitle(movie.Title)
            .WithUrl(movie.Url)
            .WithDescription(movie.Plot)
            .AddField(efb => efb.WithName("Year").WithValue(movie.Year).WithIsInline(true))
            .WithImageUrl(movie.ImageUrl).Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Searches for a game on Steam by name and displays its store information.
    /// </summary>
    /// <param name="query">The name of the game to search for on Steam.</param>
    [SlashCommand("steam", "Look up a game on Steam")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Steam([Summary("game", "The game name")] string query)
    {
        if (!await ValidateQuery(query).ConfigureAwait(false))
            return;

        await DeferAsync().ConfigureAwait(false);

        var gameInfo = await Service.GetSteamGameInfoByName(query).ConfigureAwait(false);
        if (gameInfo == null)
        {
            await ReplyErrorAsync(Strings.NotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(page => CreateSteamPage(page, gameInfo))
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(gameInfo.Screenshots?.Count ?? 0)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(10))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Shortens a provided URL using the goolnk.com API.
    /// </summary>
    /// <param name="query">The URL to be shortened.</param>
    [SlashCommand("shorten", "Shorten a url")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Shorten([Summary("url", "The url to shorten")] string query)
    {
        if (!await ValidateQuery(query).ConfigureAwait(false))
            return;

        await DeferAsync().ConfigureAwait(false);

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
                var content = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var data = await JsonSerializer.DeserializeAsync<Searches.ShortenData>(content);

                if (!string.IsNullOrWhiteSpace(data?.ResultUrl))
                {
                    CachedShortenedLinks.TryAdd(query, data.ResultUrl);
                }
                else
                {
                    await ErrorAsync(Strings.ErrorOccured(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                shortLink = data.ResultUrl;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error shortening a link: {Message}", ex.Message);
                await ErrorAsync(Strings.ErrorOccured(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }
        }

        await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder()
                .WithColor(Mewdeko.OkColor)
                .AddField(efb => efb.WithName(Strings.OriginalUrl(ctx.Guild.Id))
                    .WithValue($"<{query}>"))
                .AddField(efb => efb.WithName(Strings.ShortUrl(ctx.Guild.Id))
                    .WithValue($"<{shortLink}>")).Build())
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Performs a reverse image search using an attached image or an image link.
    /// </summary>
    /// <param name="attachment">The image to reverse search.</param>
    /// <param name="imageLink">The direct URL of the image to search for.</param>
    [SlashCommand("reverse-image", "Reverse search an image on Google, TinEye and Yandex")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Revimg([Summary("image", "The image to search")] IAttachment? attachment = null,
        [Summary("url", "A direct image url to search")]
        string? imageLink = null)
    {
        var link = attachment?.Url ?? imageLink?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(link))
        {
            await ReplyErrorAsync(Strings.YouNeedToAttachFileOrUrl(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await SendReverseImageLinks(link).ConfigureAwait(false);
    }

    /// <summary>
    ///     Performs a reverse image search using a user's avatar.
    /// </summary>
    /// <param name="usr">The user whose avatar to reverse search. Defaults to the caller.</param>
    [SlashCommand("reverse-avatar", "Reverse search a user's avatar")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public Task Revav([Summary("user", "The user whose avatar to search")] IGuildUser? usr = null)
    {
        usr ??= (IGuildUser)ctx.User;

        var av = usr.RealAvatarUrl();

        return SendReverseImageLinks(av.ToString());
    }

    /// <summary>
    ///     Searches for and displays an image based on the provided tags from Safebooru.
    /// </summary>
    /// <param name="tag">The tags to search for on Safebooru.</param>
    [SlashCommand("safebooru", "Get a safe for work image from Safebooru")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Safebooru([Summary("tags", "Space separated tags to search")] string? tag = null)
    {
        await DeferAsync().ConfigureAwait(false);

        tag = tag?.Trim() ?? "";

        var imgObj = await Service.DapiSearch(tag, DapiSearchType.Safebooru, ctx.Guild?.Id).ConfigureAwait(false);

        if (imgObj == null)
        {
            await ErrorAsync($"{ctx.User.Mention} {Strings.NoResults(ctx.Guild.Id)}").ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor()
                .WithDescription($"{ctx.User.Mention} [{tag ?? Strings.Url(ctx.Guild.Id)}]({imgObj.FileUrl})")
                .WithImageUrl(imgObj.FileUrl)
                .WithFooter(efb => efb.WithText(DapiSearchType.Safebooru.ToString())).Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Displays a color swatch based on the provided hexadecimal color codes.
    /// </summary>
    /// <param name="colors">Space separated hex color codes to display.</param>
    [SlashCommand("color", "Display swatches for the given hex colors")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Color([Summary("colors", "Space separated hex codes, e.g. #FF0000 00FF00")] string colors)
    {
        var parts = colors.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            await ReplyErrorAsync(Strings.ColorInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var parsed = new List<SKColor>();
        foreach (var part in parts)
        {
            try
            {
                parsed.Add(SKColor.Parse(part.Replace("#", "", StringComparison.InvariantCulture)));
            }
            catch
            {
                await ReplyErrorAsync(Strings.ColorInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }
        }

        await DeferAsync().ConfigureAwait(false);

        var colorObjects = parsed.Take(10).ToArray();

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
        await ctx.Interaction.FollowupWithFileAsync(stream, "colors.png").ConfigureAwait(false);
    }

    /// <summary>
    ///     Searches for and displays Bible verses based on the book, chapter, and verse provided.
    /// </summary>
    /// <param name="book">The book of the Bible.</param>
    /// <param name="chapterAndVerse">The chapter and verse in the format "Chapter:Verse".</param>
    [SlashCommand("bible", "Look up a Bible verse")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Bible([Summary("book", "The book, e.g. John")] string book,
        [Summary("verse", "Chapter and verse, e.g. 3:16")]
        string chapterAndVerse)
    {
        await DeferAsync().ConfigureAwait(false);

        var obj = new BibleVerses();
        try
        {
            using var http = factory.CreateClient();
            var res = await http
                .GetStreamAsync($"https://bible-api.com/{book} {chapterAndVerse}").ConfigureAwait(false);

            obj = await JsonSerializer.DeserializeAsync<BibleVerses>(res);
        }
        catch
        {
            obj = new BibleVerses();
        }

        if (obj.Error != null || obj.Verses is null || !obj.Verses.Any())
        {
            await ErrorAsync(obj.Error ?? Strings.NoVerseFound(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            var v = obj.Verses[0];
            await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.BibleVerseTitle(ctx.Guild.Id, v.BookName, v.Chapter, v.Verse))
                .WithDescription(v.Text).Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Displays interactive weather information for a specified location.
    /// </summary>
    /// <param name="query">The location query to search for weather information.</param>
    [SlashCommand("weather", "Get the weather for a location")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Weather([Summary("location", "The city or place to look up")] string query)
    {
        if (!await ValidateQuery(query).ConfigureAwait(false))
            return;

        await DeferAsync().ConfigureAwait(false);

        var data = await Service.GetWeatherDataAsync(query).ConfigureAwait(false);
        if (data is null)
        {
            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Format.Bold(Strings.CityNotFound(ctx.Guild.Id)));
            await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
            return;
        }

        var component = BuildWeatherComponent(data);
        await ctx.Interaction.FollowupAsync(components: component.Build(), flags: MessageFlags.ComponentsV2)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays the current time in a specified location.
    /// </summary>
    /// <param name="query">The location query to search for the current time.</param>
    [SlashCommand("time", "Get the current time in a location")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Time([Summary("location", "The city or place to look up")] string query)
    {
        if (!await ValidateQuery(query).ConfigureAwait(false))
            return;

        await DeferAsync().ConfigureAwait(false);

        var (candidates, err) = await Service.GetTimeDataWithCandidatesAsync(query).ConfigureAwait(false);
        if (err is not null)
        {
            var errorMsg = err switch
            {
                TimeErrors.InvalidInput => Strings.InvalidInput(ctx.Guild.Id),
                TimeErrors.NotFound => Strings.TimezoneNotFound(ctx.Guild.Id),
                _ => Strings.ErrorOccured(ctx.Guild.Id)
            };

            await ReplyErrorAsync(errorMsg).ConfigureAwait(false);
            return;
        }

        if (candidates == null || candidates.Count == 0)
        {
            await ReplyErrorAsync(Strings.TimezoneNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (candidates.Count == 1)
        {
            var data = candidates[0];
            var timeString = data.Time.ToString("h:mm:ss tt");
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.TimeNew(ctx.Guild.Id))
                .WithDescription(Format.Code(timeString))
                .AddField(Strings.Location(ctx.Guild.Id), string.Join('\n', data.Address.Split(", ")), true)
                .AddField(Strings.Timezone(ctx.Guild.Id), data.TimeZoneName, true);

            await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await ShowTimezoneSelectMenu(candidates, query).ConfigureAwait(false);
    }

    /// <summary>
    ///     Retrieves information about a Pokemon.
    /// </summary>
    /// <param name="name">The name of the Pokemon. Include the word shiny to show the shiny sprite.</param>
    [SlashCommand("pokemon", "Look up a Pokemon")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Pokemon([Summary("name", "The Pokemon name, add shiny for the shiny sprite")] string name)
    {
        await DeferAsync().ConfigureAwait(false);

        var isShiny = false;
        Pokemon? poke;

        if (name.ToLower().Contains("shiny"))
        {
            isShiny = true;
            name = name.ToLower().Replace("shiny", "");
        }

        try
        {
            poke = await pokeClient.GetResourceAsync<Pokemon>(name.Replace(" ", ""))
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            await ReplyErrorAsync(Strings.PokemonNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var abilities = new List<Ability>();
        if (poke.Abilities.Any())
        {
            foreach (var i in poke.Abilities)
            {
                var ability = await pokeClient.GetResourceAsync<Ability>(i.Ability.Name).ConfigureAwait(false);
                abilities.Add(ability);
            }
        }

        var stats = poke.Stats.Select(s => $"{s.Stat.Name}: {s.BaseStat}").ToList();

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(poke.Moves.Count / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        Task<PageBuilder> PageFactory(int page)
        {
            var pb = new PageBuilder();
            pb.WithTitle(isShiny
                ? $"Shiny {char.ToUpper(poke.Name[0]) + poke.Name[1..]}"
                : char.ToUpper(poke.Name[0]) + poke.Name[1..]);
            pb.WithOkColor();
            pb.AddField("Abilities",
                string.Join("\n", abilities.Select(x => $"{char.ToUpper(x.Name[0]) + x.Name[1..]}")));

            if (poke.Forms.Any())
            {
                pb.AddField("Forms",
                    string.Join("\n", poke.Forms.Select(x => $"{char.ToUpper(x.Name[0]) + x.Name[1..]}")));
            }

            if (stats.Any())
            {
                pb.AddField("Base Stats", string.Join("\n", stats));
            }

            if (poke.Types.Any())
            {
                pb.AddField("Types",
                    string.Join("\n",
                        poke.Types.Select(x => $"{char.ToUpper(x.Type.Name[0]) + x.Type.Name[1..]}")));
            }

            if (poke.HeldItems.Any())
            {
                pb.AddField("Held Items",
                    string.Join("\n",
                        poke.HeldItems.Select(x => $"{char.ToUpper(x.Item.Name[0]) + x.Item.Name[1..]}")));
            }

            if (poke.GameIndicies.Any())
            {
                pb.AddField("Game Indices",
                    string.Join("\n",
                        poke.GameIndicies.Skip(10 * 1).Take(10).Select(x =>
                            $"{char.ToUpper(x.Version.Name[0]) + x.Version.Name[1..]}")));
            }

            if (poke.Moves.Any())
            {
                pb.AddField("Moves",
                    string.Join("\n",
                        poke.Moves.Skip(10 * page).Take(10)
                            .Select(x => $"{char.ToUpper(x.Move.Name[0]) + x.Move.Name[1..]}")));
            }

            pb.WithImageUrl(isShiny
                ? $"http://img.dscord.co/shiny/{poke.Id}-0-.png"
                : $"http://img.dscord.co/images/{poke.Id}-0-.png");

            return Task.FromResult(pb);
        }
    }

    private async Task SendReverseImageLinks(string imageLink)
    {
        var googleLink = $"https://images.google.com/searchbyimage?image_url={imageLink}";
        var tineyeLink = $"https://www.tineye.com/search?url={imageLink}";
        var yandexLink = $"https://yandex.com/images/search?url={imageLink}&rpt=imageview";

        var response = $"Google: [Link]({googleLink})\nTinEye: [Link]({tineyeLink})\nYandex: [Link]({yandexLink})";

        await ConfirmAsync(response).ConfigureAwait(false);
    }

    private async Task ShowTimezoneSelectMenu(
        List<(string Address, DateTime Time, string TimeZoneName, string TimezoneId)> candidates, string originalQuery)
    {
        var options = new List<SelectMenuOptionBuilder>();

        for (var i = 0; i < Math.Min(candidates.Count, 25); i++)
        {
            var candidate = candidates[i];
            var timeString = candidate.Time.ToString("h:mm tt");
            var description = $"{timeString} | {candidate.Address}";

            options.Add(new SelectMenuOptionBuilder()
                .WithLabel(candidate.TimeZoneName.Length > 100
                    ? candidate.TimeZoneName[..97] + Strings.Ellipsis(ctx.Guild.Id)
                    : candidate.TimeZoneName)
                .WithDescription(description.Length > 100
                    ? description[..97] + Strings.Ellipsis(ctx.Guild.Id)
                    : description)
                .WithValue($"timezone_select:{candidate.TimezoneId}"));
        }

        var selectMenu = new SelectMenuBuilder()
            .WithCustomId($"timezone_select_menu:{originalQuery}")
            .WithPlaceholder(Strings.TimezoneSelectPlaceholder(ctx.Guild.Id, candidates.Count, originalQuery))
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithOptions(options);

        var component = new ComponentBuilder()
            .WithSelectMenu(selectMenu);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.MultipleTimezonesFound(ctx.Guild.Id))
            .WithDescription(Strings.TimezoneDisambiguationDescription(ctx.Guild.Id, candidates.Count, originalQuery));

        await ctx.Interaction.FollowupAsync(embed: embed.Build(), components: component.Build())
            .ConfigureAwait(false);
    }

    private ComponentBuilderV2 BuildWeatherComponent(OpenMeteoWeatherResponse weatherData)
    {
        var current = weatherData.Current;
        var emoji = WeatherCodeInterpreter.GetEmoji(current.WeatherCode);
        var condition = WeatherCodeInterpreter.GetDescription(current.WeatherCode);

        var builder = new ComponentBuilderV2();

        var locationDisplay = current.Country == current.LocationName
            ? current.LocationName
            : $"{current.LocationName}, {current.Country}";

        builder.WithContainer([
            new TextDisplayBuilder($"# {emoji} Weather for {locationDisplay}"),
            new TextDisplayBuilder($"-# {condition} | Updated at {DateTime.Parse(current.Time):HH:mm}")
        ], Mewdeko.OkColor);

        var weatherOptions = new List<SelectMenuOptionBuilder>
        {
            new(Strings.CurrentConditions(ctx.Guild.Id), "current:0",
                Strings.LiveWeatherData(ctx.Guild.Id), isDefault: true)
        };

        for (var i = 0; i < Math.Min(weatherData.Daily.Time.Count, 7); i++)
        {
            var date = DateTime.Parse(weatherData.Daily.Time[i]);
            var label = i == 0 ? $"{Strings.Today(ctx.Guild.Id)} ({date:MMM dd})" :
                i == 1 ? $"{Strings.Tomorrow(ctx.Guild.Id)} ({date:MMM dd})" : date.ToString("ddd, MMM dd");
            var tempMax = weatherData.Daily.TemperatureMax[i];
            var tempMin = weatherData.Daily.TemperatureMin[i];

            weatherOptions.Add(new SelectMenuOptionBuilder()
                .WithLabel(label)
                .WithDescription(
                    $"{Strings.High(ctx.Guild.Id)}: {tempMax:F1}°C, {Strings.Low(ctx.Guild.Id)}: {tempMin:F1}°C")
                .WithValue($"daily:{i}"));
        }

        weatherOptions.Add(new SelectMenuOptionBuilder(Strings.HourlyForecast(ctx.Guild.Id), "hourly:0",
            Strings.DetailedHourlyConditions(ctx.Guild.Id)));

        builder.WithActionRow([
            new SelectMenuBuilder($"weather_main_select:{current.LocationName}", weatherOptions,
                Strings.SelectForecastPeriod(ctx.Guild.Id))
        ]);

        BuildCurrentWeatherContainer(builder, current);

        BuildWeeklySummaryContainer(builder, weatherData);

        builder.WithSeparator()
            .WithTextDisplay($"-# {Strings.WeatherAttribution(ctx.Guild.Id)}");

        return builder;
    }

    private void BuildCurrentWeatherContainer(ComponentBuilderV2 builder, CurrentWeather current)
    {
        var f = (double c) => c * 1.8 + 32;

        var primaryConditions = new List<TextDisplayBuilder>
        {
            new($"**{current.Temperature:F1}°C** ({f(current.Temperature):F1}°F)"),
            new(
                $"{Strings.FeelsLike(ctx.Guild.Id)}: {current.ApparentTemperature:F1}°C ({f(current.ApparentTemperature):F1}°F)"),
            new($"{Strings.Humidity(ctx.Guild.Id)}: {current.RelativeHumidity}%")
        };

        if (current.DewPoint.HasValue)
            primaryConditions.Add(
                new TextDisplayBuilder($"{Strings.Dewpoint(ctx.Guild.Id)}: {current.DewPoint:F1}°C"));

        builder.WithContainer(primaryConditions.ToArray(), new Color(52, 152, 219));

        var windComponents = new List<TextDisplayBuilder>
        {
            new($"{Strings.WindSpeed(ctx.Guild.Id)}: {current.WindSpeed:F1} km/h ({current.WindDirection}°)")
        };

        if (current.WindGusts.HasValue)
            windComponents.Add(
                new TextDisplayBuilder($"{Strings.WindGusts(ctx.Guild.Id)}: {current.WindGusts:F1} km/h"));

        windComponents.Add(
            new TextDisplayBuilder($"{Strings.Pressure(ctx.Guild.Id)}: {current.SurfacePressure:F1} hPa"));
        windComponents.Add(new TextDisplayBuilder($"{Strings.CloudCover(ctx.Guild.Id)}: {current.CloudCover}%"));

        if (current.Visibility.HasValue)
            windComponents.Add(
                new TextDisplayBuilder($"{Strings.Visibility(ctx.Guild.Id)}: {current.Visibility / 1000:F1} km"));

        builder.WithContainer(windComponents.ToArray(), new Color(155, 89, 182));

        var precipComponents = new List<TextDisplayBuilder>();

        if (current.Precipitation is > 0)
            precipComponents.Add(new TextDisplayBuilder($"Precipitation: {current.Precipitation:F1} mm"));

        if (current.Rain is > 0)
            precipComponents.Add(new TextDisplayBuilder($"Rain: {current.Rain:F1} mm"));

        if (current.Snowfall is > 0)
            precipComponents.Add(
                new TextDisplayBuilder($"{Strings.Snowfall(ctx.Guild.Id)}: {current.Snowfall:F1} cm"));

        if (current.Cape.HasValue)
            precipComponents.Add(new TextDisplayBuilder($"{Strings.Cape(ctx.Guild.Id)}: {current.Cape:F0} J/kg"));

        if (current.FreezingLevelHeight.HasValue)
            precipComponents.Add(
                new TextDisplayBuilder(
                    $"{Strings.FreezingLevel(ctx.Guild.Id)}: {current.FreezingLevelHeight / 1000:F1} km"));

        if (precipComponents.Count > 0)
            builder.WithContainer(precipComponents.ToArray(), new Color(52, 73, 94));

        var solarComponents = new List<TextDisplayBuilder>();

        if (current.UvIndex.HasValue)
            solarComponents.Add(new TextDisplayBuilder($"{Strings.UvIndex(ctx.Guild.Id)}: {current.UvIndex:F1}"));

        if (current.SolarRadiation.HasValue)
            solarComponents.Add(new TextDisplayBuilder($"Solar Radiation: {current.SolarRadiation:F0} W/m²"));

        if (current.Evapotranspiration.HasValue)
            solarComponents.Add(
                new TextDisplayBuilder(
                    $"{Strings.Evapotranspiration(ctx.Guild.Id)}: {current.Evapotranspiration:F2} mm"));

        if (solarComponents.Count > 0)
            builder.WithContainer(solarComponents.ToArray(), new Color(241, 196, 15));

        var soilComponents = new List<TextDisplayBuilder>();

        if (current.SoilTemperature0cm.HasValue)
            soilComponents.Add(
                new TextDisplayBuilder(
                    $"{Strings.SoilTemperature(ctx.Guild.Id)}: {current.SoilTemperature0cm:F1}°C"));

        if (current.SoilMoisture0to1cm.HasValue)
            soilComponents.Add(
                new TextDisplayBuilder(
                    $"{Strings.SoilMoisture(ctx.Guild.Id)}: {current.SoilMoisture0to1cm * 100:F1}%"));

        if (soilComponents.Count > 0)
            builder.WithContainer(soilComponents.ToArray(), new Color(139, 69, 19));
    }

    private void BuildWeeklySummaryContainer(ComponentBuilderV2 builder, OpenMeteoWeatherResponse weatherData)
    {
        var weeklyComponents = new List<TextDisplayBuilder>();

        for (var i = 0; i < Math.Min(weatherData.Daily.Time.Count, 7); i++)
        {
            var date = DateTime.Parse(weatherData.Daily.Time[i]);
            var tempMax = weatherData.Daily.TemperatureMax[i];
            var tempMin = weatherData.Daily.TemperatureMin[i];
            var precipitation = weatherData.Daily.PrecipitationSum[i];
            var uv = weatherData.Daily.UvIndexMax[i];

            var dayLabel = i == 0 ? Strings.Today(ctx.Guild.Id) :
                i == 1 ? Strings.Tomorrow(ctx.Guild.Id) : date.ToString("ddd");
            var precipText = precipitation > 0 ? $" | {precipitation:F1}mm" : "";
            var uvText = uv > 5 ? $" | UV: {uv:F1}" : "";

            weeklyComponents.Add(
                new TextDisplayBuilder($"`{dayLabel,-9}` {tempMax:F1}°/{tempMin:F1}°C{precipText}{uvText}"));
        }

        builder.WithContainer([
            new TextDisplayBuilder("## 7-Day Outlook"),
            .. weeklyComponents
        ], new Color(46, 204, 113));
    }

    private async Task<PageBuilder> CreateSteamPage(int page, SteamGameInfo gameInfo)
    {
        await Task.CompletedTask;

        var priceText = gameInfo.Price == null
            ? "N/A"
            : gameInfo.Price.Final == gameInfo.Price.Initial
                ? $"${gameInfo.Price.Final / 100.0m:F2}"
                : $"~~${gameInfo.Price.Initial / 100.0m:F2}~~ ${gameInfo.Price.Final / 100.0m:F2}";

        var pageBuilder = new PageBuilder()
            .WithOkColor()
            .WithTitle(gameInfo.Name)
            .WithUrl($"https://store.steampowered.com/app/{gameInfo.SteamAppid}")
            .WithDescription(gameInfo.ShortDescription)
            .AddField("Price", priceText, true)
            .AddField("Platforms", GetPlatforms(gameInfo), true)
            .AddField("Release Date", gameInfo.ReleaseDate?.Date ?? "N/A", true)
            .AddField("Developer", string.Join(", ", gameInfo.Developers), true)
            .AddField("Categories", string.Join(", ", gameInfo.Categories?.Take(3).Select(c => c.Description)),
                true);

        if (gameInfo.Metacritic?.Score > 0)
        {
            pageBuilder.AddField("Metacritic", $"{gameInfo.Metacritic.Score}/100", true);
        }

        if (gameInfo.Screenshots != null && gameInfo.Screenshots.Count > page)
        {
            pageBuilder.WithImageUrl(gameInfo.Screenshots[page].PathFull);
        }
        else
        {
            pageBuilder.WithImageUrl(gameInfo.HeaderImage);
        }

        if (gameInfo.Recommendations?.Total > 0)
        {
            pageBuilder.WithFooter(Strings.GameRecommendations(ctx.Guild.Id,
                gameInfo.Recommendations.Total.ToString("N0")));
        }

        return pageBuilder;
    }

    private static string GetPlatforms(SteamGameInfo gameInfo)
    {
        var platforms = new List<string>();
        if (gameInfo.Platforms["windows"]) platforms.Add("Windows");
        if (gameInfo.Platforms["mac"]) platforms.Add("macOS");
        if (gameInfo.Platforms["linux"]) platforms.Add("Linux");

        return platforms.Any() ? string.Join(", ", platforms) : "N/A";
    }

    private async Task<bool> ValidateQuery(string? query)
    {
        if (!string.IsNullOrWhiteSpace(query))
            return true;

        await ErrorAsync(Strings.SpecifySearchParams(ctx.Guild.Id)).ConfigureAwait(false);
        return false;
    }
}