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
using NsfwSpyNS;
using Refit;
using Serilog;
using SkiaSharp;

namespace Mewdeko.Modules.Searches;

public partial class Searches(IBotCredentials creds, IGoogleApiService google, IHttpClientFactory factory,
        IMemoryCache cache,
        GuildTimezoneService tzSvc,
        InteractiveService serv,
        MartineApi martineApi, ToneTagService toneTagService,
        BotConfigService config, INsfwSpy nsfwSpy)
    : MewdekoModuleBase<SearchesService>
{
    private static readonly ConcurrentDictionary<string, string> CachedShortenedLinks = new();

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
            await ctx.Channel.SendErrorAsync("Seems like that subreddit wasn't found, please try something else!")
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


    [Cmd, Aliases]
    public Task RandomCat() => InternalRandomImage(SearchesService.ImageTag.Cats);


    [Cmd, Aliases]
    public Task RandomDog() => InternalRandomImage(SearchesService.ImageTag.Dogs);


    [Cmd, Aliases]
    public Task RandomFood() => InternalRandomImage(SearchesService.ImageTag.Food);


    [Cmd, Aliases]
    public Task RandomBird() => InternalRandomImage(SearchesService.ImageTag.Birds);


    private Task InternalRandomImage(SearchesService.ImageTag tag)
    {
        var url = Service.GetRandomImageUrl(tag);
        return ctx.Channel.EmbedAsync(new EmbedBuilder()
            .WithOkColor()
            .WithImageUrl(url.ToString()));
    }


    [Cmd, Aliases, Ratelimit(20)]
    public async Task Image([Remainder] string query)
    {
        using var gscraper = new GoogleScraper();
        using var dscraper = new DuckDuckGoScraper();
        var search = await gscraper.GetImagesAsync(query, SafeSearchLevel.Strict).ConfigureAwait(false);
        search = search.Take(10);
        if (!search.Any())
        {
            var search2 = await dscraper.GetImagesAsync(query, SafeSearchLevel.Strict).ConfigureAwait(false);
            search2 = search2.Take(10);
            if (!search2.Any())
            {
                await ctx.Channel.SendErrorAsync("Unable to find that or the image is nsfw!").ConfigureAwait(false);
            }
            else
            {
                var images = search2.ToHashSet();
                var tasks = images.Select(ClassifyAndFilterImage).ToList();

                await Task.WhenAll(tasks);

                async Task ClassifyAndFilterImage(DuckDuckGoImageResult i)
                {
                    try
                    {
                        var isNsfw = await nsfwSpy.ClassifyImageAsync(new Uri(i.Url));
                        if (isNsfw.IsNsfw)
                        {
                            lock (images)
                            {
                                images.Remove(i);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // ignored because 403s
                    }
                }

                var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
                    .WithFooter(
                        PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(images.Count)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
                await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                    .ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    var result = images.Skip(page).FirstOrDefault();
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
            var images = search.ToHashSet();
            var tasks = images.Select(ClassifyAndFilterImage).ToList();

            await Task.WhenAll(tasks);

            async Task ClassifyAndFilterImage(GoogleImageResult i)
            {
                try
                {
                    var isNsfw = await nsfwSpy.ClassifyImageAsync(new Uri(i.Url));
                    if (isNsfw.IsNsfw)
                    {
                        lock (images)
                        {
                            images.Remove(i);
                        }
                    }
                }
                catch (Exception e)
                {
                    // ignored because 403s
                }
            }

            var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(images.Count)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var result = images.Skip(page).FirstOrDefault();
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
                        "Neither google nor duckduckgo returned a result! Please search something else!")
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


    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Revav([Remainder] IGuildUser? usr = null)
    {
        usr ??= (IGuildUser)ctx.User;

        var av = usr.RealAvatarUrl();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

        await Revimg(av.ToString());
    }


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


    [Cmd, Aliases]
    public Task Safebooru([Remainder] string? tag = null) =>
        InternalDapiCommand(ctx.Message, tag, DapiSearchType.Safebooru);


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
        if (!string.IsNullOrWhiteSpace(query))
            return true;

        await ErrorLocalizedAsync("specify_search_params").ConfigureAwait(false);
        return false;
    }

    public class ShortenData
    {
        [JsonProperty("result_url")]
        public string ResultUrl { get; set; }
    }


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

//     private byte[] GenerateFakeTweet(string username, byte[] profileImageBytes, string tweetText)
// {
//     int width = 600;  // Width of the tweet image
//     int height = 200; // Starting height, will adjust based on text length
//
//     using var profileImage = SKBitmap.Decode(profileImageBytes);
//     var resizedProfileImage = profileImage.Resize(new SKImageInfo(32, 32), SKFilterQuality.High); // Resize to 32x32
//
//     // Measure tweet text height
//     using var textPaint = new SKPaint
//     {
//         Color = SKColors.White,
//         TextSize = 20,
//         IsAntialias = true,
//     };
//     var textBounds = new SKRect();
//     textPaint.MeasureText(tweetText, ref textBounds);
//     int textHeight = (int)textBounds.Height;
//
//     // Compute the position for the timestamp based on text height
//     int timestampPosition = 110 + textHeight;  // Adjusted position for timestamp
//
//     // Adjust the overall height based on the timestamp position
//     height = timestampPosition + 20;
//
//     using var bitmap = new SKBitmap(width, height);
//     using var canvas = new SKCanvas(bitmap);
//
//     // Draw background (Dark Mode Color)
//     canvas.DrawColor(new SKColor(32, 35, 39));  // Dark mode background color
//
//     // Save canvas state before clipping
//     canvas.Save();
//
//     // Clip canvas to circle for profile image
//     var profileImageRect = new SKRect(10, 20, 42, 52);  // Adjusted for smaller size and position
//     var circularPath = new SKPath();
//     circularPath.AddOval(profileImageRect);
//     canvas.ClipPath(circularPath);
//
//     // Draw profile image
//     canvas.DrawBitmap(resizedProfileImage, 10, 20);
//
//     // Restore canvas state to before clipping
//     canvas.Restore();
//
//     // Draw username (Bold)
//     using var usernamePaint = new SKPaint
//     {
//         Color = SKColors.White,
//         TextSize = 24,
//         IsAntialias = true,
//         Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
//     };
//     canvas.DrawText(username, 60, 40, usernamePaint);
//
//     // Draw handle (Twitter ID)
//     var handle = "@" + username.ToLower();
//     using var handlePaint = new SKPaint
//     {
//         Color = SKColors.Gray,
//         TextSize = 18,
//         IsAntialias = true,
//     };
//     canvas.DrawText(handle, 60, 65, handlePaint);
//
//     // Draw tweet text
//     canvas.DrawText(tweetText, 60, 90, textPaint);
//
//     // Draw timestamp
//     var timestamp = DateTime.Now.ToString("h:mm tt · MMM d, yyyy");
//     using var timestampPaint = new SKPaint
//     {
//         Color = SKColors.Gray,
//         TextSize = 16,
//         IsAntialias = true,
//     };
//     canvas.DrawText(timestamp, 60, timestampPosition, timestampPaint);
//
//     // Convert the bitmap to byte array
//     using var image = SKImage.FromBitmap(bitmap);
//     using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
//     return data.ToArray();
// }
}