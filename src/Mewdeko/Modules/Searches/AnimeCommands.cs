#nullable enable
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;
using Anilist4Net;
using Anilist4Net.Enums;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using JikanDotNet;
using MartineApiNet;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Services.Settings;
using NekosBestApiNet;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    [Group]
    public class AnimeCommands : MewdekoSubmodule<SearchesService>
    {
        public readonly NekosBestApi NekosBestApi;
        private readonly MartineApi martineApi;
        private readonly InteractiveService interactivity;
        private readonly HttpClient httpClient;
        private readonly BotConfigService config;

        public AnimeCommands(InteractiveService service, MartineApi martineApi, NekosBestApi nekosBestApi,
            HttpClient httpClient,
            BotConfigService config)
        {
            interactivity = service;
            this.martineApi = martineApi;
            NekosBestApi = nekosBestApi;
            this.httpClient = httpClient;
            this.config = config;
        }

        [Cmd, Aliases]
        public async Task Ship(IUser user, IUser user2)
        {
            var random = new Random().Next(0, 101);
            var getShip = await Service.GetShip(user.Id, user2.Id);
            if (getShip is not null)
                random = getShip.Score;
            else
                await Service.SetShip(user.Id, user2.Id, random);
            var shipRequest = await martineApi.ImageGenerationApi.GenerateShipImage(random, user.RealAvatarUrl().AbsoluteUri, user2.RealAvatarUrl().AbsoluteUri)
                .ConfigureAwait(false);
            var bytes = await shipRequest.ReadAsByteArrayAsync().ConfigureAwait(false);
            var ms = new MemoryStream(bytes);
            await using var _ = ms.ConfigureAwait(false);
            var color = new Color();
            var response = string.Empty;
            switch (random)
            {
                case < 30:
                    response = "No chance, just none. Don't even think about it.";
                    color = Discord.Color.Red;
                    break;
                case <= 50 and >= 31:
                    response = "You may have a chance but don't try too hard.";
                    color = Discord.Color.Teal;
                    break;
                case 69:
                    response = "Go 69 that mfer";
                    color = Discord.Color.DarkRed;
                    break;
                case <= 70 and >= 60:
                    response = "I mean, go for it, I guess, looks like you would do good";
                    color = Discord.Color.Magenta;
                    break;
                case <= 100 and >= 71:
                    response =
                        "Horoscopes conclude that today will be a good day.. And that you two will get a room together soon";
                    color = Discord.Color.Red;
                    break;
            }

            await ctx.Channel.SendFileAsync(ms, "ship.png",
                    embed: new EmbedBuilder().WithColor(color).WithDescription($"You are {random}% compatible. {response}").WithImageUrl("attachment://ship.png").Build())
                .ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Ship(IUser user)
            => await Ship(ctx.User, user);

        [Cmd, Aliases]
        public async Task RandomNeko()
        {
            var req = await NekosBestApi.CategoryApi.Neko().ConfigureAwait(false);
            var em = new EmbedBuilder
            {
                Description = $"nya~ [Source]({req.Results.FirstOrDefault().SourceUrl})", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task RandomKitsune()
        {
            var req = await NekosBestApi.CategoryApi.Kitsune().ConfigureAwait(false);
            var em = new EmbedBuilder
            {
                Description = $"What does the fox say? [Source]({req.Results.FirstOrDefault().SourceUrl})", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task RandomWaifu()
        {
            var req = await NekosBestApi.CategoryApi.Waifu().ConfigureAwait(false);
            var em = new EmbedBuilder
            {
                Description = $"Ara Ara~ [Source]({req.Results.FirstOrDefault().SourceUrl})", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        [Priority(0)]
        public async Task Mal([Remainder] string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            var fullQueryLink = "https://myanimelist.net/profile/" + name;

            var malConfig = Configuration.Default.WithDefaultLoader();
            using var document = await BrowsingContext.New(malConfig).OpenAsync(fullQueryLink).ConfigureAwait(false);
            var imageElem = document.QuerySelector(
                "body > div#myanimelist > div.wrapper > div#contentWrapper > div#content > div.content-container > div.container-left > div.user-profile > div.user-image > img");
            var imageUrl = ((IHtmlImageElement)imageElem).Source ?? "https://icecream.me/uploads/870b03f36b59cc16ebfe314ef2dde781.png";

            var stats = document
                .QuerySelectorAll(
                    "body > div#myanimelist > div.wrapper > div#contentWrapper > div#content > div.content-container > div.container-right > div#statistics > div.user-statistics-stats > div.stats > div.clearfix > ul.stats-status > li > span")
                .Select(x => x.InnerHtml).ToList();

            var favorites = document.QuerySelectorAll("div.user-favorites > div.di-tc");

            var favAnime = GetText("anime_no_fav");
            if (favorites.Length > 0 && favorites[0].QuerySelector("p") == null)
            {
                favAnime = string.Join("\n", favorites[0].QuerySelectorAll("ul > li > div.di-tc.va-t > a")
                    .Shuffle()
                    .Take(3)
                    .Select(x =>
                    {
                        var elem = (IHtmlAnchorElement)x;
                        return $"[{elem.InnerHtml}]({elem.Href})";
                    }));
            }

            var info = document.QuerySelectorAll("ul.user-status:nth-child(3) > li.clearfix")
                .Select(x => Tuple.Create(x.Children[0].InnerHtml, x.Children[1].InnerHtml))
                .ToList();

            var daysAndMean = document.QuerySelectorAll("div.anime:nth-child(1) > div:nth-child(2) > div")
                .Select(x => x.TextContent.Split(':').Select(y => y.Trim()).ToArray())
                .ToArray();

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(GetText("mal_profile", name))
                .AddField(efb => efb.WithName("💚 " + GetText("watching")).WithValue(stats[0]).WithIsInline(true))
                .AddField(efb => efb.WithName("💙 " + GetText("completed")).WithValue(stats[1]).WithIsInline(true));
            if (info.Count < 3)
                embed.AddField(efb => efb.WithName("💛 " + GetText("on_hold")).WithValue(stats[2]).WithIsInline(true));
            embed
                .AddField(efb => efb.WithName("💔 " + GetText("dropped")).WithValue(stats[3]).WithIsInline(true))
                .AddField(efb => efb.WithName("⚪ " + GetText("plan_to_watch")).WithValue(stats[4]).WithIsInline(true))
                .AddField(efb => efb.WithName("🕐 " + daysAndMean[0][0]).WithValue(daysAndMean[0][1]).WithIsInline(true))
                .AddField(efb => efb.WithName("📊 " + daysAndMean[1][0]).WithValue(daysAndMean[1][1]).WithIsInline(true))
                .AddField(efb => efb.WithName(MalInfoToEmoji(info[0].Item1) + " " + info[0].Item1).WithValue(info[0].Item2.TrimTo(20)).WithIsInline(true))
                .AddField(efb => efb.WithName(MalInfoToEmoji(info[1].Item1) + " " + info[1].Item1).WithValue(info[1].Item2.TrimTo(20)).WithIsInline(true));
            if (info.Count > 2)
                embed.AddField(efb => efb.WithName(MalInfoToEmoji(info[2].Item1) + " " + info[2].Item1).WithValue(info[2].Item2.TrimTo(20)).WithIsInline(true));

            embed
                .WithDescription($@"
** https://myanimelist.net/animelist/{name} **

**{GetText("top_3_fav_anime")}**
{favAnime}"
                )
                .WithUrl(fullQueryLink)
                .WithImageUrl(imageUrl);

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private static string MalInfoToEmoji(string info)
        {
            info = info.Trim().ToLowerInvariant();
            return info switch
            {
                "gender" => "🚁",
                "location" => "🗺",
                "last online" => "👥",
                "birthday" => "📆",
                _ => "❔"
            };
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
        public Task Mal(IGuildUser usr)
            => Mal(usr.Username);

        [Cmd, Aliases]
        public async Task FindAnime(string? e = null)
        {
            var t = string.Empty;
            if (e != null) t = e;
            if (e is null)
            {
                try
                {
                    t = ctx.Message.Attachments.FirstOrDefault()?.Url;
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync("You need to attach a file or use a url with this!").ConfigureAwait(false);
                    return;
                }
            }

            var c2 = new Client();
            var response = await httpClient.PostAsync(
                $"https://api.trace.moe/search?url={t}", null).ConfigureAwait(false);
            var responseContent = response.Content;
            using var reader = new StreamReader(await responseContent.ReadAsStreamAsync().ConfigureAwait(false));
            var er = await reader.ReadToEndAsync().ConfigureAwait(false);
            var stuff = JsonConvert.DeserializeObject<MoeResponse>(er,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
            if (!string.IsNullOrWhiteSpace(stuff.Error))
            {
                await ctx.Channel.SendErrorAsync($"There was an issue with the findanime command:\n{stuff.Error}").ConfigureAwait(false);
                return;
            }

            var ert = stuff.Result.FirstOrDefault();
            if (ert?.Filename is null)
            {
                await ctx.Channel.SendErrorAsync(
                    "No results found. Please try a different image, or avoid cropping the current one.").ConfigureAwait(false);
            }

            var image = await c2.GetMediaById(ert.Anilist).ConfigureAwait(false);
            var eb = new EmbedBuilder
            {
                ImageUrl = image?.CoverImageLarge, Color = Mewdeko.OkColor
            };
            var te = image?.SeasonInt.ToString()?[2..] is "" ? image.SeasonInt.ToString()?[1..] : image?.SeasonInt.ToString()?[2..];
            var entitle = image?.EnglishTitle;
            if (image?.EnglishTitle == null) entitle = "None";
            eb.AddField("English Title", entitle);
            eb.AddField("Japanese Title", image?.NativeTitle);
            eb.AddField("Romanji Title", image?.RomajiTitle);
            eb.AddField("Air Start Date", image?.AiringStartDate);
            eb.AddField("Air End Date", image?.AiringEndDate);
            eb.AddField("Season Number", te);
            if (ert.Episode is not 0) eb.AddField("Episode", ert.Episode);
            eb.AddField("AniList Link", image?.SiteUrl);
            eb.AddField("MAL Link", $"https://myanimelist.net/anime/{image?.IdMal}");
            eb.AddField("Score", image?.MeanScore);
            eb.AddField("Description", image?.DescriptionMd.TrimTo(1024).StripHtml());
            _ = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task CharInfo([Remainder] string chara)
        {
            var anilist = new Client();
            var te = await anilist.GetCharacterBySearch(chara).ConfigureAwait(false);
            var desc = string.Empty;
            if (te.DescriptionMd is null) desc = "None";
            if (te.DescriptionMd != null) desc = te.DescriptionMd;
            if (te.DescriptionMd is { Length: > 1024 }) desc = te.DescriptionMd.TrimTo(1024);
            var altnames = string.IsNullOrEmpty(te.AlternativeNames.FirstOrDefault()) ? "None" : string.Join(",", te.AlternativeNames);
            var eb = new EmbedBuilder();
            eb.AddField(" Full Name", te.FullName);
            eb.AddField("Alternative Names", altnames);
            eb.AddField("Native Name", te.NativeName);
            eb.AddField("Description/Backstory", desc);
            eb.ImageUrl = te.ImageLarge;
            eb.Color = Mewdeko.OkColor;
            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Anime([Remainder] string query)
        {
            var c2 = new Client();
            Media? result;

            try
            {
                result = await c2.GetMediaBySearch(query, MediaTypes.ANIME).ConfigureAwait(false);
            }
            catch (NullReferenceException)
            {
                await ctx.Channel.SendErrorAsync(
                    "The anime you searched for wasn't found! Please try a different query!").ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder
            {
                ImageUrl = result?.CoverImageLarge
            };
            var te = result?.SeasonInt.ToString()?[2..] is ""
                ? result.SeasonInt.ToString()?[1..]
                : result?.SeasonInt.ToString()?[2..];
            if (result?.DescriptionMd != null) eb.AddField("Description", result.DescriptionMd.TrimTo(1024), true);
            if (result!.Genres.Length > 0) eb.AddField("Genres", string.Join("\n", result.Genres), true);
            if (result.CountryOfOrigin is not null) eb.AddField("Country of Origin", result.CountryOfOrigin, true);
            eb.AddField("Episodes", result.Episodes, true);
            if (result.SeasonInt is not null) eb.AddField("Seasons", te, true);
            eb.AddField("Air Start Date", result.AiringStartDate, true);
            eb.AddField("Air End Date", result.AiringEndDate, true);
            eb.AddField("Average Score", result.AverageScore, true);
            eb.AddField("Mean Score", result.MeanScore, true);
            eb.AddField("Is this Nsfw?", result.IsAdult, true);
            eb.Title = $"{result.EnglishTitle}";
            eb.Color = Mewdeko.OkColor;
            eb.WithUrl(result.SiteUrl);
            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Manga([Remainder] string query)
        {
            var msg = await ctx.Channel.SendConfirmAsync(
                $"{config.Data.LoadingEmote} Getting results for {query}...").ConfigureAwait(false);
            IJikan jikan = new Jikan();
            var result = await jikan.SearchMangaAsync(query).ConfigureAwait(false);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(result.Data.Count - 1)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await msg.DeleteAsync().ConfigureAwait(false);
            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                var data = result.Data.Skip(page).FirstOrDefault();
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder()
                    .WithTitle(Format.Bold($"{data.Titles.First()}"))
                    .AddField("First Publish Date", data.Published)
                    .AddField("Volumes", data.Volumes)
                    .AddField("Is Still Active", data.Publishing)
                    .AddField("Score", data.Score)
                    .AddField("Url", data.Url)
                    .WithDescription(data.Background)
                    .WithImageUrl(data.Images.WebP.MaximumImageUrl!).WithColor(Mewdeko.OkColor);
            }
        }
    }
}
