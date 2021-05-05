using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Memory;
using NadekoBot.Common;
using NadekoBot.Common.Attributes;
using NadekoBot.Common.Replacements;
using NadekoBot.Core.Modules.Searches.Common;
using NadekoBot.Core.Services;
using NadekoBot.Extensions;
using NadekoBot.Modules.Searches.Common;
using NadekoBot.Modules.Searches.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NadekoBot.Modules.Administration.Services;
using Configuration = AngleSharp.Configuration;

namespace NadekoBot.Modules.Searches
{
    public partial class Searches : NadekoTopLevelModule<SearchesService>
    {
        private readonly IBotCredentials _creds;
        private readonly IGoogleApiService _google;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IMemoryCache _cache;
        private static readonly NadekoRandom _rng = new NadekoRandom();
        private readonly GuildTimezoneService _tzSvc;

        public Searches(IBotCredentials creds, IGoogleApiService google, IHttpClientFactory factory, IMemoryCache cache,
            GuildTimezoneService tzSvc)
        {
            _creds = creds;
            _google = google;
            _httpFactory = factory;
            _cache = cache;
            _tzSvc = tzSvc;
        }

        //for anonymasen :^)
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Rip([Leftover] IGuildUser usr)
        {
            var av = usr.RealAvatarUrl(128);
            if (av == null)
                return;
            using (var picStream = await _service.GetRipPictureAsync(usr.Nickname ?? usr.Username, av).ConfigureAwait(false))
            {
                await ctx.Channel.SendFileAsync(
                    picStream,
                    "rip.png",
                    $"Rip {Format.Bold(usr.ToString())} \n\t- " +
                        Format.Italics(ctx.User.ToString()))
                    .ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageMessages)]
        [Priority(1)]
        public async Task Say(ITextChannel channel, [Leftover] string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var rep = new ReplacementBuilder()
                        .WithDefault(ctx.User, channel, (SocketGuild)ctx.Guild, (DiscordSocketClient)ctx.Client)
                        .Build();

            if (CREmbed.TryParse(message, out var embedData))
            {
                rep.Replace(embedData);
                try
                {
                    await channel.EmbedAsync(embedData, sanitizeAll: !((IGuildUser)Context.User).GuildPermissions.MentionEveryone).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.Warn(ex);
                }
            }
            else
            {
                var msg = rep.Replace(message);
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    await channel.SendConfirmAsync(msg).ConfigureAwait(false);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageMessages)]
        [Priority(0)]
        public Task Say([Leftover] string message) =>
            Say((ITextChannel)ctx.Channel, message);

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Weather([Leftover] string query)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            var embed = new EmbedBuilder();
            var data = await _service.GetWeatherDataAsync(query).ConfigureAwait(false);

            if (data == null)
            {
                embed.WithDescription(GetText("city_not_found"))
                    .WithErrorColor();
            }
            else
            {
                Func<double, double> f = StandardConversions.CelsiusToFahrenheit;
                
                var tz = Context.Guild is null
                    ? TimeZoneInfo.Utc
                    : _tzSvc.GetTimeZoneOrUtc(Context.Guild.Id);
                var sunrise = data.Sys.Sunrise.ToUnixTimestamp();
                var sunset = data.Sys.Sunset.ToUnixTimestamp();
                sunrise = sunrise.ToOffset(tz.GetUtcOffset(sunrise));
                sunset = sunset.ToOffset(tz.GetUtcOffset(sunset));
                var timezone = $"UTC{sunrise:zzz}";

                embed.AddField(fb => fb.WithName("🌍 " + Format.Bold(GetText("location"))).WithValue($"[{data.Name + ", " + data.Sys.Country}](https://openweathermap.org/city/{data.Id})").WithIsInline(true))
                    .AddField(fb => fb.WithName("📏 " + Format.Bold(GetText("latlong"))).WithValue($"{data.Coord.Lat}, {data.Coord.Lon}").WithIsInline(true))
                    .AddField(fb => fb.WithName("☁ " + Format.Bold(GetText("condition"))).WithValue(string.Join(", ", data.Weather.Select(w => w.Main))).WithIsInline(true))
                    .AddField(fb => fb.WithName("😓 " + Format.Bold(GetText("humidity"))).WithValue($"{data.Main.Humidity}%").WithIsInline(true))
                    .AddField(fb => fb.WithName("💨 " + Format.Bold(GetText("wind_speed"))).WithValue(data.Wind.Speed + " m/s").WithIsInline(true))
                    .AddField(fb => fb.WithName("🌡 " + Format.Bold(GetText("temperature"))).WithValue($"{data.Main.Temp:F1}°C / {f(data.Main.Temp):F1}°F").WithIsInline(true))
                    .AddField(fb => fb.WithName("🔆 " + Format.Bold(GetText("min_max"))).WithValue($"{data.Main.TempMin:F1}°C - {data.Main.TempMax:F1}°C\n{f(data.Main.TempMin):F1}°F - {f(data.Main.TempMax):F1}°F").WithIsInline(true))
                    .AddField(fb => fb.WithName("🌄 " + Format.Bold(GetText("sunrise"))).WithValue($"{sunrise:HH:mm} {timezone}").WithIsInline(true))
                    .AddField(fb => fb.WithName("🌇 " + Format.Bold(GetText("sunset"))).WithValue($"{sunset:HH:mm} {timezone}").WithIsInline(true))
                    .WithOkColor()
                    .WithFooter(efb => efb.WithText("Powered by openweathermap.org").WithIconUrl($"http://openweathermap.org/img/w/{data.Weather[0].Icon}.png"));
            }
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Time([Leftover] string query)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

            var (data, err) = await _service.GetTimeDataAsync(query).ConfigureAwait(false);
            if (!(err is null))
            {
                string errorKey;
                switch (err)
                {
                    case TimeErrors.ApiKeyMissing:
                        errorKey = "api_key_missing";
                        break;
                    case TimeErrors.InvalidInput:
                        errorKey = "invalid_input";
                        break;
                    case TimeErrors.NotFound:
                        errorKey = "not_found";
                        break;
                    default:
                        errorKey = "error_occured";
                        break;
                }
                await ReplyErrorLocalizedAsync(errorKey).ConfigureAwait(false);
                return;
            }
            else if (string.IsNullOrWhiteSpace(data.TimeZoneName))
            {
                await ReplyErrorLocalizedAsync("timezone_db_api_key").ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(GetText("time_new"))
                .WithDescription(Format.Code(data.Time.ToString()))
                .AddField(GetText("location"), string.Join('\n', data.Address.Split(", ")), inline: true)
                .AddField(GetText("timezone"), data.TimeZoneName, inline: true);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Youtube([Leftover] string query = null)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            var result = (await _google.GetVideoLinksByKeywordAsync(query, 1).ConfigureAwait(false)).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(result))
            {
                await ReplyErrorLocalizedAsync("no_results").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.SendMessageAsync(result).ConfigureAwait(false);
        }

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Movie([Leftover] string query = null)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

            var movie = await _service.GetMovieDataAsync(query).ConfigureAwait(false);
            if (movie == null)
            {
                await ReplyErrorLocalizedAsync("imdb_fail").ConfigureAwait(false);
                return;
            }
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithTitle(movie.Title)
                .WithUrl($"http://www.imdb.com/title/{movie.ImdbId}/")
                .WithDescription(movie.Plot.TrimTo(1000))
                .AddField(efb => efb.WithName("Rating").WithValue(movie.ImdbRating).WithIsInline(true))
                .AddField(efb => efb.WithName("Genre").WithValue(movie.Genre).WithIsInline(true))
                .AddField(efb => efb.WithName("Year").WithValue(movie.Year).WithIsInline(true))
                .WithImageUrl(movie.Poster)).ConfigureAwait(false);
        }

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public Task RandomCat() => InternalRandomImage(SearchesService.ImageTag.Cats);

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public Task RandomDog() => InternalRandomImage(SearchesService.ImageTag.Dogs);

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public Task RandomFood() => InternalRandomImage(SearchesService.ImageTag.Food);

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public Task RandomBird() => InternalRandomImage(SearchesService.ImageTag.Birds);

        // done in 3.0
        private Task InternalRandomImage(SearchesService.ImageTag tag)
        {
            var url = _service.GetRandomImageUrl(tag);
            return ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithImageUrl(url));
        }

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Image([Leftover] string query = null)
        {
            var oterms = query?.Trim();
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;
            query = WebUtility.UrlEncode(oterms).Replace(' ', '+');
            try
            {
                var res = await _google.GetImageAsync(oterms).ConfigureAwait(false);
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithAuthor(eab => eab.WithName(GetText("image_search_for") + " " + oterms.TrimTo(50))
                        .WithUrl("https://www.google.rs/search?q=" + query + "&source=lnms&tbm=isch")
                        .WithIconUrl("http://i.imgur.com/G46fm8J.png"))
                    .WithDescription(res.Link)
                    .WithImageUrl(res.Link)
                    .WithTitle(ctx.User.ToString());
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            catch
            {
                _log.Warn("Falling back to Imgur");

                var fullQueryLink = $"http://imgur.com/search?q={ query }";
                var config = Configuration.Default.WithDefaultLoader();
                using (var document = await BrowsingContext.New(config).OpenAsync(fullQueryLink).ConfigureAwait(false))
                {
                    var elems = document.QuerySelectorAll("a.image-list-link").ToList();

                    if (!elems.Any())
                        return;

                    var img = (elems.ElementAtOrDefault(new NadekoRandom().Next(0, elems.Count))?.Children?.FirstOrDefault() as IHtmlImageElement);

                    if (img?.Source == null)
                        return;

                    var source = img.Source.Replace("b.", ".", StringComparison.InvariantCulture);

                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(eab => eab.WithName(GetText("image_search_for") + " " + oterms.TrimTo(50))
                            .WithUrl(fullQueryLink)
                            .WithIconUrl("http://s.imgur.com/images/logo-1200-630.jpg?"))
                        .WithDescription(source)
                        .WithImageUrl(source)
                        .WithTitle(ctx.User.ToString());
                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Lmgtfy([Leftover] string ffs = null)
        {
            if (!await ValidateQuery(ctx.Channel, ffs).ConfigureAwait(false))
                return;

            await ctx.Channel.SendConfirmAsync("<" + await _google.ShortenUrl($"http://lmgtfy.com/?q={ Uri.EscapeUriString(ffs) }").ConfigureAwait(false) + ">")
                           .ConfigureAwait(false);
        }

        public class ShortenData
        {
            [JsonProperty("result_url")]
            public string ResultUrl { get; set; }
        }

        private static readonly ConcurrentDictionary<string, string> cachedShortenedLinks = new ConcurrentDictionary<string, string>();

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Shorten([Leftover] string query)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            query = query.Trim();
            if (!cachedShortenedLinks.TryGetValue(query, out var shortLink))
            {
                try
                {
                    using (var _http = _httpFactory.CreateClient())
                    using (var req = new HttpRequestMessage(HttpMethod.Post, "https://goolnk.com/api/v1/shorten"))
                    {
                        var formData = new MultipartFormDataContent
                    {
                        { new StringContent(query), "url" }
                    };
                        req.Content = formData;

                        using (var res = await _http.SendAsync(req).ConfigureAwait(false))
                        {
                            var content = await res.Content.ReadAsStringAsync();
                            var data = JsonConvert.DeserializeObject<ShortenData>(content);

                            if (!string.IsNullOrWhiteSpace(data?.ResultUrl))
                                cachedShortenedLinks.TryAdd(query, data.ResultUrl);
                            else
                                return;

                            shortLink = data.ResultUrl;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error shortening a link: {Message}", ex.Message);
                    return;
                }
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithColor(NadekoBot.OkColor)
                .AddField(efb => efb.WithName(GetText("original_url"))
                                    .WithValue($"<{query}>"))
                .AddField(efb => efb.WithName(GetText("short_url"))
                                    .WithValue($"<{shortLink}>")))
                .ConfigureAwait(false);
        }

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Google([Leftover] string query = null)
        {
            var oterms = query?.Trim();
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            query = WebUtility.UrlEncode(oterms).Replace(' ', '+');

            var fullQueryLink = $"https://www.google.ca/search?q={ query }&safe=on&lr=lang_eng&hl=en&ie=utf-8&oe=utf-8";

            using (var msg = new HttpRequestMessage(HttpMethod.Get, fullQueryLink))
            {
                msg.Headers.AddFakeHeaders();
                var parser = new HtmlParser();
                var test = "";
                using (var http = _httpFactory.CreateClient())
                using (var response = await http.SendAsync(msg).ConfigureAwait(false))
                using (var document = await parser.ParseDocumentAsync(test = await response.Content.ReadAsStringAsync().ConfigureAwait(false)).ConfigureAwait(false))
                {
                    var elems = document.QuerySelectorAll("div.g");

                    var resultsElem = document.QuerySelectorAll("#resultStats").FirstOrDefault();
                    var totalResults = resultsElem?.TextContent;
                    //var time = resultsElem.Children.FirstOrDefault()?.TextContent
                    //^ this doesn't work for some reason, <nobr> is completely missing in parsed collection
                    if (!elems.Any())
                        return;

                    var results = elems.Select<IElement, GoogleSearchResult?>(elem =>
                    {
                        var aTag = elem.QuerySelector("a") as IHtmlAnchorElement; // <h3> -> <a>
                        var href = aTag?.Href;
                        var name = aTag?.QuerySelector("h3")?.TextContent;
                        if (href == null || name == null)
                            return null;

                        var txt = elem.QuerySelectorAll(".st").FirstOrDefault()?.TextContent;

                        if (txt == null)
                            return null;

                        return new GoogleSearchResult(name, href, txt);
                    }).Where(x => x != null).Take(5);

                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(eab => eab.WithName(GetText("search_for") + " " + oterms.TrimTo(50))
                            .WithUrl(fullQueryLink)
                            .WithIconUrl("http://i.imgur.com/G46fm8J.png"))
                        .WithTitle(ctx.User.ToString())
                        .WithFooter(efb => efb.WithText(totalResults));

                    var desc = await Task.WhenAll(results.Select(async res =>
                            $"[{Format.Bold(res?.Title)}]({(await _google.ShortenUrl(res?.Link).ConfigureAwait(false))})\n{res?.Text?.TrimTo(400 - res.Value.Title.Length - res.Value.Link.Length)}\n\n"))
                        .ConfigureAwait(false);
                    var descStr = string.Concat(desc);
                    await ctx.Channel.EmbedAsync(embed.WithDescription(descStr)).ConfigureAwait(false);
                }
            }
        }

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task MagicTheGathering([Leftover] string search)
        {
            if (!await ValidateQuery(ctx.Channel, search))
                return;

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var card = await _service.GetMtgCardAsync(search).ConfigureAwait(false);

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
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Hearthstone([Leftover] string name)
        {
            var arg = name;
            if (!await ValidateQuery(ctx.Channel, name).ConfigureAwait(false))
                return;

            if (string.IsNullOrWhiteSpace(_creds.MashapeKey))
            {
                await ReplyErrorLocalizedAsync("mashape_api_missing").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var card = await _service.GetHearthstoneCardDataAsync(name).ConfigureAwait(false);

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
        [NadekoCommand, Usage, Description, Aliases]
        public async Task UrbanDict([Leftover] string query = null)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            using (var http = _httpFactory.CreateClient())
            {
                var res = await http.GetStringAsync($"http://api.urbandictionary.com/v0/define?term={Uri.EscapeUriString(query)}").ConfigureAwait(false);
                try
                {
                    var items = JsonConvert.DeserializeObject<UrbanResponse>(res).List;
                    if (items.Any())
                    {

                        await ctx.SendPaginatedConfirmAsync(0, (p) =>
                        {
                            var item = items[p];
                            return new EmbedBuilder().WithOkColor()
                                         .WithUrl(item.Permalink)
                                         .WithAuthor(eab => eab.WithIconUrl("http://i.imgur.com/nwERwQE.jpg").WithName(item.Word))
                                         .WithDescription(item.Definition);
                        }, items.Length, 1).ConfigureAwait(false);
                        return;
                    }
                }
                catch
                {
                }
            }
            await ReplyErrorLocalizedAsync("ud_error").ConfigureAwait(false);

        }

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Define([Leftover] string word)
        {
            if (!await ValidateQuery(ctx.Channel, word).ConfigureAwait(false))
                return;

            using (var _http = _httpFactory.CreateClient())
            {
                string res;
                try
                {
                    res = await _cache.GetOrCreateAsync($"define_{word}", e =>
                     {
                         e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
                         return _http.GetStringAsync("https://api.pearson.com/v2/dictionaries/entries?headword=" + WebUtility.UrlEncode(word));
                     }).ConfigureAwait(false);

                    var data = JsonConvert.DeserializeObject<DefineModel>(res);

                    var datas = data.Results
                        .Where(x => !(x.Senses is null) && x.Senses.Count > 0 && !(x.Senses[0].Definition is null))
                        .Select(x => (Sense: x.Senses[0], x.PartOfSpeech));

                    if (!datas.Any())
                    {
                        _log.Warn("Definition not found: {Word}", word);
                        await ReplyErrorLocalizedAsync("define_unknown").ConfigureAwait(false);
                    }


                    var col = datas.Select(data => (
                        Definition: data.Sense.Definition is string
                            ? data.Sense.Definition.ToString()
                            : ((JArray)JToken.Parse(data.Sense.Definition.ToString())).First.ToString(),
                        Example: data.Sense.Examples is null || data.Sense.Examples.Count == 0
                            ? string.Empty
                            : data.Sense.Examples[0].Text,
                        Word: word,
                        WordType: string.IsNullOrWhiteSpace(data.PartOfSpeech) ? "-" : data.PartOfSpeech
                    )).ToList();

                    _log.Info($"Sending {col.Count} definition for: {word}");

                    await ctx.SendPaginatedConfirmAsync(0, page =>
                    {
                        var data = col.Skip(page).First();
                        var embed = new EmbedBuilder()
                            .WithDescription(ctx.User.Mention)
                            .AddField(GetText("word"), data.Word, inline: true)
                            .AddField(GetText("class"), data.WordType, inline: true)
                            .AddField(GetText("definition"), data.Definition)
                            .WithOkColor();

                        if (!string.IsNullOrWhiteSpace(data.Example))
                            embed.AddField(efb => efb.WithName(GetText("example")).WithValue(data.Example));

                        return embed;
                    }, col.Count, 1);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error retrieving definition data for: {Word}", word);
                }
            }
        }

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Catfact()
        {
            using (var http = _httpFactory.CreateClient())
            {
                var response = await http.GetStringAsync("https://catfact.ninja/fact").ConfigureAwait(false);
                if (response == null)
                    return;

                var fact = JObject.Parse(response)["fact"].ToString();
                await ctx.Channel.SendConfirmAsync("🐈" + GetText("catfact"), fact).ConfigureAwait(false);
            }
        }

        //done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Revav([Leftover] IGuildUser usr = null)
        {
            if (usr == null)
                usr = (IGuildUser)ctx.User;

            var av = usr.RealAvatarUrl();
            if (av == null)
                return;

            await ctx.Channel.SendConfirmAsync($"https://images.google.com/searchbyimage?image_url={av}").ConfigureAwait(false);
        }

        //done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Revimg([Leftover] string imageLink = null)
        {
            imageLink = imageLink?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(imageLink))
                return;
            await ctx.Channel.SendConfirmAsync($"https://images.google.com/searchbyimage?image_url={imageLink}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public Task Safebooru([Leftover] string tag = null)
            => InternalDapiCommand(ctx.Message, tag, DapiSearchType.Safebooru);

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Wiki([Leftover] string query = null)
        {
            query = query?.Trim();

            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            using (var http = _httpFactory.CreateClient())
            {
                var result = await http.GetStringAsync("https://en.wikipedia.org//w/api.php?action=query&format=json&prop=info&redirects=1&formatversion=2&inprop=url&titles=" + Uri.EscapeDataString(query)).ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<WikipediaApiModel>(result);
                if (data.Query.Pages[0].Missing || string.IsNullOrWhiteSpace(data.Query.Pages[0].FullUrl))
                    await ReplyErrorLocalizedAsync("wiki_page_not_found").ConfigureAwait(false);
                else
                    await ctx.Channel.SendMessageAsync(data.Query.Pages[0].FullUrl).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Color(params SixLabors.ImageSharp.Color[] colors)
        {
            if (!colors.Any())
                return;

            var colorObjects = colors.Take(10)
                .ToArray();

            using (var img = new Image<Rgba32>(colorObjects.Length * 50, 50))
            {
                for (int i = 0; i < colorObjects.Length; i++)
                {
                    var x = i * 50;
                    img.Mutate(m => m.FillPolygon(colorObjects[i], new PointF[] {
                        new PointF(x, 0),
                        new PointF(x + 50, 0),
                        new PointF(x + 50, 50),
                        new PointF(x, 50)
                    }));
                }
                using (var ms = img.ToStream())
                {
                    await ctx.Channel.SendFileAsync(ms, $"colors.png").ConfigureAwait(false);
                }
            }
        }

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Avatar([Leftover] IGuildUser usr = null)
        {
            if (usr == null)
                usr = (IGuildUser)ctx.User;

            var avatarUrl = usr.RealAvatarUrl(2048);

            if (avatarUrl == null)
            {
                await ReplyErrorLocalizedAsync("avatar_none", usr.ToString()).ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .AddField(efb => efb.WithName("Username").WithValue(usr.ToString()).WithIsInline(false))
                .AddField(efb => efb.WithName("Avatar Url").WithValue(avatarUrl).WithIsInline(false))
                .WithThumbnailUrl(avatarUrl.ToString()), ctx.User.Mention).ConfigureAwait(false);
        }
        
        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Wikia(string target, [Leftover] string query)
        {
            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(query))
            {
                await ReplyErrorLocalizedAsync("wikia_input_error").ConfigureAwait(false);
                return;
            }
            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            using (var http = _httpFactory.CreateClient())
            {
                http.DefaultRequestHeaders.Clear();
                try
                {
                    var res = await http.GetStringAsync($"https://{Uri.EscapeUriString(target)}.fandom.com/api.php" +
                                                        $"?action=query" +
                                                        $"&format=json" +
                                                        $"&list=search" +
                                                        $"&srsearch={Uri.EscapeUriString(query)}" +
                                                        $"&srlimit=1").ConfigureAwait(false);
                    var items = JObject.Parse(res);
                    var title = items["query"]?["search"]?.FirstOrDefault()?["title"]?.ToString();
                    
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        await ReplyErrorLocalizedAsync("wikia_error").ConfigureAwait(false);
                        return;
                    }

                    var url = Uri.EscapeUriString($"https://{target}.fandom.com/wiki/{title}");
                    var response = $@"`{GetText("title")}` {title?.SanitizeMentions()}
`{GetText("url")}:` {url}";
                    await ctx.Channel.SendMessageAsync(response).ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalizedAsync("wikia_error").ConfigureAwait(false);
                }
            }
        }

        // done in 3.0
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Bible(string book, string chapterAndVerse)
        {
            var obj = new BibleVerses();
            try
            {
                using (var http = _httpFactory.CreateClient())
                {
                    var res = await http
                        .GetStringAsync("https://bible-api.com/" + book + " " + chapterAndVerse).ConfigureAwait(false);

                    obj = JsonConvert.DeserializeObject<BibleVerses>(res);
                }
            }
            catch
            {
            }
            if (obj.Error != null || obj.Verses == null || obj.Verses.Length == 0)
                await ctx.Channel.SendErrorAsync(obj.Error ?? "No verse found.").ConfigureAwait(false);
            else
            {
                var v = obj.Verses[0];
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"{v.BookName} {v.Chapter}:{v.Verse}")
                    .WithDescription(v.Text)).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Steam([Leftover] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            var appId = await _service.GetSteamAppIdByName(query).ConfigureAwait(false);
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

        public async Task InternalDapiCommand(IUserMessage umsg, string tag, DapiSearchType type)
        {
            var channel = umsg.Channel;

            tag = tag?.Trim() ?? "";

            var imgObj = await _service.DapiSearch(tag, type, ctx.Guild?.Id).ConfigureAwait(false);

            if (imgObj == null)
                await channel.SendErrorAsync(umsg.Author.Mention + " " + GetText("no_results")).ConfigureAwait(false);
            else
                await channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithDescription($"{umsg.Author.Mention} [{tag ?? "url"}]({imgObj.FileUrl})")
                    .WithImageUrl(imgObj.FileUrl)
                    .WithFooter(efb => efb.WithText(type.ToString()))).ConfigureAwait(false);
        }

        public async Task<bool> ValidateQuery(IMessageChannel ch, string query)
        {
            if (!string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            await ErrorLocalizedAsync("specify_search_params").ConfigureAwait(false);
            return false;
        }
    }
}
