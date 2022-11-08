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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Color = SixLabors.ImageSharp.Color;

namespace Mewdeko.Modules.Searches;

public partial class Searches : MewdekoModuleBase<SearchesService>
{
    private static readonly ConcurrentDictionary<string, string> CachedShortenedLinks = new();
    private readonly IMemoryCache cache;
    private readonly IBotCredentials creds;
    private readonly IGoogleApiService google;
    private readonly IHttpClientFactory httpFactory;
    private readonly GuildTimezoneService tzSvc;
    private readonly InteractiveService interactivity;
    private readonly MartineApi martineApi;
    private readonly ToneTagService toneTagService;
    private readonly BotConfigService config;

    public Searches(IBotCredentials creds, IGoogleApiService google, IHttpClientFactory factory, IMemoryCache cache,
        GuildTimezoneService tzSvc,
        InteractiveService serv,
        MartineApi martineApi, ToneTagService toneTagService,
        BotConfigService config)
    {
        interactivity = serv;
        this.martineApi = martineApi;
        this.creds = creds;
        this.google = google;
        httpFactory = factory;
        this.cache = cache;
        this.tzSvc = tzSvc;
        this.toneTagService = toneTagService;
        this.config = config;
    }

    [Cmd, Aliases]
    public async Task Meme()
    {
        var msg = await ctx.Channel.SendConfirmAsync($"{config.Data.LoadingEmote} Fetching random meme...").ConfigureAwait(false);
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

    [Cmd, Aliases]
    public async Task RandomReddit(string subreddit)
    {
        var msg = await ctx.Channel.SendConfirmAsync("Checking if the subreddit is nsfw...").ConfigureAwait(false);
        if (Service.NsfwCheck(subreddit))
        {
            var emt = new EmbedBuilder
            {
                Description = "This subreddit is nsfw!",
                Color = Mewdeko.ErrorColor
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
            await ctx.Channel.SendErrorAsync("Seems like that subreddit wasn't found, please try something else!").ConfigureAwait(false);
            Log.Error($"Seems that Meme fetching has failed. Here's the error:\nCode: {ex.StatusCode}\nContent: {(ex.HasContent ? ex.Content : "No Content.")}");
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

    // done in 3.0
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
                            $"[{$"{data.Name}, {data.Sys.Country}"}](https://openweathermap.org/city/{data.Id})")
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

    // done in 3.0
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

    // done in 3.0
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

        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithDescription(result[page].Snippet.Description.TrimTo(2048))
                                    .WithAuthor(new EmbedAuthorBuilder().WithName($"YouTube Search for {query.TrimTo(40)}")
                                                                        .WithIconUrl("https://cdn.mewdeko.tech/YouTube.png"))
                                    .WithTitle(result[page].Snippet.Title)
                                    .WithUrl($"https://www.youtube.com/watch?v={result[page].Id.VideoId}")
                                    .WithImageUrl(result[page].Snippet.Thumbnails.High.Url)
                                    .WithColor(new Discord.Color(255, 0, 0));
        }

    }

    // done in 3.0
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

    // done in 3.0
    [Cmd, Aliases]
    public Task RandomCat() => InternalRandomImage(SearchesService.ImageTag.Cats);

    // done in 3.0
    [Cmd, Aliases]
    public Task RandomDog() => InternalRandomImage(SearchesService.ImageTag.Dogs);

    // done in 3.0
    [Cmd, Aliases]
    public Task RandomFood() => InternalRandomImage(SearchesService.ImageTag.Food);

    // done in 3.0
    [Cmd, Aliases]
    public Task RandomBird() => InternalRandomImage(SearchesService.ImageTag.Birds);

    // done in 3.0
    private Task InternalRandomImage(SearchesService.ImageTag tag)
    {
        var url = Service.GetRandomImageUrl(tag);
        return ctx.Channel.EmbedAsync(new EmbedBuilder()
            .WithOkColor()
            .WithImageUrl(url.ToString()));
    }

    // done in 3.0
    [Cmd, Aliases]
    public async Task Image([Remainder] string query)
    {
        using var gscraper = new GoogleScraper();
        using var dscraper = new DuckDuckGoScraper();
        var search = await gscraper.GetImagesAsync(query, SafeSearchLevel.Strict).ConfigureAwait(false);
        var googleImageResults = search as GoogleImageResult[] ?? search.ToArray();
        if (googleImageResults.Length == 0)
        {
            var search2 = await dscraper.GetImagesAsync(query, SafeSearchLevel.Strict).ConfigureAwait(false);
            var duckDuckGoImageResults = search2 as DuckDuckGoImageResult[] ?? search2.ToArray();
            if (duckDuckGoImageResults.Length == 0)
            {
                await ctx.Channel.SendErrorAsync("Unable to find that or the image is nsfw!").ConfigureAwait(false);
            }
            else
            {
                var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
                                                          .WithFooter(
                                                              PaginatorFooter.PageNumber | PaginatorFooter.Users)
                                                          .WithMaxPageIndex(duckDuckGoImageResults.Length)
                                                          .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
                await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    var result = duckDuckGoImageResults.Skip(page).FirstOrDefault();
                    return new PageBuilder().WithOkColor().WithDescription(result!.Title)
                                                            .WithImageUrl(result.Url)
                                                            .WithAuthor(name: "DuckDuckGo Image Result",
                                                                iconUrl:
                                                                "https://media.discordapp.net/attachments/915770282579484693/941382938547863572/5847f32fcef1014c0b5e4877.png%22");
                }
            }
        }
        else
        {
            var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
                                                      .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                                                      .WithMaxPageIndex(googleImageResults.Length).WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                                                      .Build();
            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var result = googleImageResults.Skip(page).FirstOrDefault();
                return new PageBuilder().WithOkColor().WithDescription(result.Title)
                                                        .WithImageUrl(result.Url)
                                                        .WithAuthor(name: "Google Image Result",
                                                            iconUrl:
                                                            "https://media.discordapp.net/attachments/915770282579484693/941383056609144832/superG_v3.max-200x200.png%22");
            }
        }
    }

    [Cmd, Aliases]
    public async Task Lmgtfy([Remainder] string? ffs = null)
    {
        if (!await ValidateQuery(ctx.Channel, ffs).ConfigureAwait(false))
            return;

        await ctx.Channel.SendConfirmAsync(
                     $"<{await google.ShortenUrl($"https://lmgtfy.com/?q={Uri.EscapeDataString(ffs)}").ConfigureAwait(false)}>")
            .ConfigureAwait(false);
    }

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
                using var http = httpFactory.CreateClient();
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://goolnk.com/api/v1/shorten");
                req.Content = new MultipartFormDataContent
                {
                    {new StringContent(query), "url"}
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

    // done in 3.0
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
                    "Neither google nor duckduckgo returned a result! Please search something else!").ConfigureAwait(false);
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

    // done in 3.0
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

    // done in 3.0
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

    // done in 3.0
    [Cmd, Aliases]
    public async Task UrbanDict([Remainder] string? query = null)
    {
        if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
            return;

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
        using var http = httpFactory.CreateClient();
        var res = await http
            .GetStringAsync($"https://api.urbandictionary.com/v0/define?term={Uri.EscapeDataString(query)}")
            .ConfigureAwait(false);
        try
        {
            var items = JsonConvert.DeserializeObject<UrbanResponse>(res)?.List;
            if (items != null && items.Length > 0)
            {
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(items.Length - 1)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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

    // done in 3.0
    [Cmd, Aliases]
    public async Task Define([Remainder] string word)
    {
        if (!await ValidateQuery(ctx.Channel, word).ConfigureAwait(false))
            return;

        using var http = httpFactory.CreateClient();
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

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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

    // done in 3.0
    [Cmd, Aliases]
    public async Task Catfact()
    {
        using var http = httpFactory.CreateClient();
        var response = await http.GetStringAsync("https://catfact.ninja/fact").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response))
            return;

        var fact = JObject.Parse(response)["fact"].ToString();
        await ctx.Channel.SendConfirmAsync($"🐈{GetText("catfact")}", fact).ConfigureAwait(false);
    }

    //done in 3.0
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Revav([Remainder] IGuildUser? usr = null)
    {
        if (usr == null)
            usr = (IGuildUser)ctx.User;

        var av = usr.RealAvatarUrl();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

        await ctx.Channel.SendConfirmAsync($"https://images.google.com/searchbyimage?image_url={av}")
                 .ConfigureAwait(false);
    }

    //done in 3.0
    [Cmd, Aliases]
    public async Task Revimg([Remainder] string? imageLink = null)
    {
        imageLink = imageLink?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(imageLink))
            return;
        await ctx.Channel.SendConfirmAsync($"https://images.google.com/searchbyimage?image_url={imageLink}")
            .ConfigureAwait(false);
    }

    [Cmd, Aliases]
    public Task Safebooru([Remainder] string? tag = null) => InternalDapiCommand(ctx.Message, tag, DapiSearchType.Safebooru);

    // done in 3.0
    [Cmd, Aliases]
    public async Task Wiki([Remainder] string? query = null)
    {
        query = query?.Trim();

        if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
            return;

        using var http = httpFactory.CreateClient();
        var result = await http
            .GetStringAsync(
                $"https://en.wikipedia.org//w/api.php?action=query&format=json&prop=info&redirects=1&formatversion=2&inprop=url&titles={Uri.EscapeDataString(query)}").ConfigureAwait(false);
        var data = JsonConvert.DeserializeObject<WikipediaApiModel>(result);
        if (data.Query.Pages[0].Missing || string.IsNullOrWhiteSpace(data.Query.Pages[0].FullUrl))
            await ReplyErrorLocalizedAsync("wiki_page_not_found").ConfigureAwait(false);
        else
            await ctx.Channel.SendMessageAsync(data.Query.Pages[0].FullUrl).ConfigureAwait(false);
    }

    [Cmd, Aliases]
    public async Task Color(params Color[] colors)
    {
        if (colors.Length == 0)
            return;

        var colorObjects = colors.Take(10)
            .ToArray();

        using var img = new Image<Rgba32>(colorObjects.Length * 50, 50);
        for (var i = 0; i < colorObjects.Length; i++)
        {
            var x = i * 50;
            img.Mutate(m => m.FillPolygon(colorObjects[i], new PointF(x, 0), new PointF(x + 50, 0),
                new PointF(x + 50, 50), new PointF(x, 50)));
        }

        var ms = img.ToStream();
        await using var _ = ms.ConfigureAwait(false);
        await ctx.Channel.SendFileAsync(ms, "colors.png").ConfigureAwait(false);
    }

    // done in 3.0
    [Cmd, Aliases]
    public async Task Wikia(string target, [Remainder] string query)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(query))
        {
            await ReplyErrorLocalizedAsync("wikia_input_error").ConfigureAwait(false);
            return;
        }

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        try
        {
            var res = await http.GetStringAsync(
                $"https://{Uri.EscapeDataString(target)}.fandom.com/api.php?action=query&format=json&list=search&srsearch={Uri.EscapeDataString(query)}&srlimit=1").ConfigureAwait(false);
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

    // done in 3.0
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Bible(string book, string chapterAndVerse)
    {
        var obj = new BibleVerses();
        try
        {
            using var http = httpFactory.CreateClient();
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
            await ctx.Channel.SendErrorAsync(obj.Error ?? "No verse found.").ConfigureAwait(false);
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

    [Cmd, Aliases]
    public async Task ResolveToneTags([Remainder] string tag)
    {
        var embed = toneTagService.GetEmbed(toneTagService.ParseTags(tag), ctx.Guild);
        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }

    public async Task InternalDapiCommand(IUserMessage umsg, string? tag, DapiSearchType type)
    {
        var channel = umsg.Channel;

        tag = tag?.Trim() ?? "";

        var imgObj = await Service.DapiSearch(tag, type, ctx.Guild?.Id).ConfigureAwait(false);

        if (imgObj == null)
        {
            await channel.SendErrorAsync($"{umsg.Author.Mention} {GetText("no_results")}").ConfigureAwait(false);
        }
        else
        {
            await channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithDescription($"{umsg.Author.Mention} [{tag ?? "url"}]({imgObj.FileUrl})")
                        .WithImageUrl(imgObj.FileUrl)
                        .WithFooter(efb => efb.WithText(type.ToString()))).ConfigureAwait(false);
        }
    }

    public async Task<bool> ValidateQuery(IMessageChannel ch, string query)
    {
        if (!string.IsNullOrWhiteSpace(query)) return true;

        await ErrorLocalizedAsync("specify_search_params").ConfigureAwait(false);
        return false;
    }

    public class ShortenData
    {
        [JsonProperty("result_url")] public string ResultUrl { get; set; }
    }
}