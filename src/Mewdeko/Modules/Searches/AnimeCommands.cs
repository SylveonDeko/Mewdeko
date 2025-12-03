#nullable enable
using System.IO;
using System.Net.Http;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Html.Dom;
using Anilist4Net;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using JikanDotNet;
using MartineApiNet;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Searches.Services;
using NekosBestApiNet;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    /// <summary>
    ///     Group of commands related to anime.
    /// </summary>
    [Group]
    public class AnimeCommands(
        InteractiveService service,
        MartineApi martineApi,
        NekosBestApi nekosBestApi,
        HttpClient httpClient)
        : MewdekoSubmodule<SearchesService>
    {
        /// <summary>
        ///     Sends a ship image based on compatibility between two users.
        /// </summary>
        /// <param name="user">The first user to be compared.</param>
        /// <param name="user2">The second user to be compared.</param>
        /// <remarks>
        ///     This command calculates the compatibility score between two users and sends a ship image
        ///     with a message based on the score.
        /// </remarks>
        /// <example>
        ///     <code>.ship @user1 @user2</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task Ship(IUser user, IUser user2)
        {
            var random = new Random().Next(0, 101);
            var getShip = await Service.GetShip(user.Id, user2.Id);
            if (getShip is not null)
                random = getShip.Score;
            else
                await Service.SetShip(user.Id, user2.Id, random);
            var shipRequest = await martineApi.ImageGenerationApi.GenerateShipImage(random,
                    user.RealAvatarUrl().AbsoluteUri, user2.RealAvatarUrl().AbsoluteUri)
                .ConfigureAwait(false);
            var bytes = await shipRequest.ReadAsByteArrayAsync().ConfigureAwait(false);
            var ms = new MemoryStream(bytes);
            await using var _ = ms.ConfigureAwait(false);
            var color = new Color();
            var response = string.Empty;
            switch (random)
            {
                case < 30:
                    response = Strings.ShipNoChance(ctx.Guild.Id);
                    break;
                case <= 50 and >= 31:
                    response = Strings.ShipMaybeChance(ctx.Guild.Id);
                    break;
                case 69:
                    response = Strings.ShipSixnine(ctx.Guild.Id);
                    break;
                case <= 70 and >= 60:
                    response = Strings.ShipGoodChance(ctx.Guild.Id);
                    break;
                case <= 100 and >= 71:
                    response = Strings.ShipExcellentChance(ctx.Guild.Id);
                    break;
            }

            await ctx.Channel.SendFileAsync(ms, "ship.png",
                    embed: new EmbedBuilder().WithColor(color)
                        .WithDescription(Strings.CompatibilityResult(ctx.Guild.Id, random, response))
                        .WithImageUrl("attachment://ship.png").Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sends a ship image based on compatibility between the current user and another user.
        /// </summary>
        /// <param name="user">The user to be compared with the current user.</param>
        /// <remarks>
        ///     This command calculates the compatibility score between the current user and another user
        ///     and sends a ship image with a message based on the score.
        /// </remarks>
        /// <example>
        ///     <code>.ship @user</code>
        /// </example>
        [Cmd]
        [Aliases]
        public Task Ship(IUser user)
        {
            return Ship(ctx.User, user);
        }

        /// <summary>
        ///     Sends a random neko image.
        /// </summary>
        /// <remarks>
        ///     This command retrieves a random neko image from the API and sends it in the channel.
        /// </remarks>
        /// <example>
        ///     <code>.randomneko</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task RandomNeko()
        {
            var req = await nekosBestApi.CategoryApi.Neko().ConfigureAwait(false);
            var em = new EmbedBuilder
            {
                Description = Strings.NekoSource(ctx.Guild.Id, req.Results.FirstOrDefault()?.SourceUrl),
                ImageUrl = req.Results.FirstOrDefault()?.Url,
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sends a random kitsune image.
        /// </summary>
        /// <remarks>
        ///     This command retrieves a random kitsune image from the API and sends it in the channel.
        /// </remarks>
        /// <example>
        ///     <code>.randomkitsune</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task RandomKitsune()
        {
            var req = await nekosBestApi.CategoryApi.Kitsune().ConfigureAwait(false);
            var em = new EmbedBuilder
            {
                Description = Strings.KitsuneSource(ctx.Guild.Id, req.Results.FirstOrDefault()?.SourceUrl),
                ImageUrl = req.Results.FirstOrDefault()?.Url,
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sends a random waifu image.
        /// </summary>
        /// <remarks>
        ///     This command retrieves a random waifu image from the API and sends it in the channel.
        /// </remarks>
        /// <example>
        ///     <code>.randomwaifu</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task RandomWaifu()
        {
            var req = await nekosBestApi.CategoryApi.Waifu().ConfigureAwait(false);
            var em = new EmbedBuilder
            {
                Description = Strings.WaifuSource(ctx.Guild.Id, req.Results.FirstOrDefault()?.SourceUrl),
                ImageUrl = req.Results.FirstOrDefault()?.Url,
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves and displays information about a MyAnimeList profile.
        /// </summary>
        /// <param name="name">The username of the MyAnimeList profile.</param>
        /// <remarks>
        ///     This command fetches and displays various statistics and information about a MyAnimeList profile,
        ///     including watching, completed, on hold, dropped, and plan to watch anime lists, as well as other details.
        /// </remarks>
        /// <example>
        ///     <code>.mal username</code>
        /// </example>
        [Cmd]
        [Aliases]
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
            var imageUrl = ((IHtmlImageElement?)imageElem)?.Source ??
                           "https://icecream.me/uploads/870b03f36b59cc16ebfe314ef2dde781.png";

            var stats = document
                .QuerySelectorAll(
                    "body > div#myanimelist > div.wrapper > div#contentWrapper > div#content > div.content-container > div.container-right > div#statistics > div.user-statistics-stats > div.stats > div.clearfix > ul.stats-status > li > span")
                .Select(x => x.InnerHtml).ToList();

            var favorites = document.QuerySelectorAll("div.user-favorites > div.di-tc");

            var favAnime = Strings.AnimeNoFav(ctx.Guild.Id);
            if (favorites.Length > 0 && favorites[0].QuerySelector("p") == null)
            {
                favAnime = string.Join("\n", favorites[0].QuerySelectorAll("ul > li > div.di-tc.va-t > a")
                    .SecureShuffle()
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
                .WithTitle(Strings.MalProfile(ctx.Guild.Id, name))
                .AddField(efb =>
                    efb.WithName("💚 " + Strings.Watching(ctx.Guild.Id)).WithValue(stats[0]).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName("💙 " + Strings.Completed(ctx.Guild.Id)).WithValue(stats[1]).WithIsInline(true));
            if (info.Count < 3)
                embed.AddField(efb =>
                    efb.WithName("💛 " + Strings.OnHold(ctx.Guild.Id)).WithValue(stats[2]).WithIsInline(true));
            embed
                .AddField(efb =>
                    efb.WithName("💔 " + Strings.Dropped(ctx.Guild.Id)).WithValue(stats[3]).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName("⚪ " + Strings.PlanToWatch(ctx.Guild.Id)).WithValue(stats[4]).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName("🕐 " + daysAndMean[0][0]).WithValue(daysAndMean[0][1]).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName("📊 " + daysAndMean[1][0]).WithValue(daysAndMean[1][1]).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName(MalInfoToEmoji(info[0].Item1) + " " + info[0].Item1)
                        .WithValue(info[0].Item2.TrimTo(20)).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName(MalInfoToEmoji(info[1].Item1) + " " + info[1].Item1)
                        .WithValue(info[1].Item2.TrimTo(20)).WithIsInline(true));
            if (info.Count > 2)
                embed.AddField(efb =>
                    efb.WithName(MalInfoToEmoji(info[2].Item1) + " " + info[2].Item1)
                        .WithValue(info[2].Item2.TrimTo(20)).WithIsInline(true));

            embed
                .WithDescription($"""

                                  ** https://myanimelist.net/animelist/{name} **

                                  **{Strings.TopThreeFavAnime(ctx.Guild.Id)}**
                                  {favAnime}
                                  """
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

        /// <summary>
        ///     Retrieves and displays information about a MyAnimeList profile for a specified user in the current guild.
        /// </summary>
        /// <param name="usr">The user for whom to retrieve the MyAnimeList profile information.</param>
        /// <remarks>
        ///     This command fetches and displays various statistics and information about the MyAnimeList profile of a specified
        ///     user
        ///     within the current guild, including watching, completed, on hold, dropped, and plan to watch anime lists, as well
        ///     as other details.
        /// </remarks>
        /// <example>
        ///     <code>.mal @username</code>
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public Task Mal(IGuildUser usr)
        {
            return Mal(usr.Username);
        }

        /// <summary>
        ///     Finds anime information based on an image.
        /// </summary>
        /// <param name="e">The image URL or an attached image to use for searching.</param>
        /// <remarks>
        ///     This command finds anime information based on an image using the Trace.moe API and displays relevant details.
        /// </remarks>
        /// <example>
        ///     <code>.findanime image_url</code>
        /// </example>
        [Cmd]
        [Aliases]
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
                    await ctx.Channel.SendErrorAsync(
                            Strings.YouNeedToAttachFileOrUrl(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }
            }

            var c2 = new Client();
            var response = await httpClient.PostAsync(
                $"https://api.trace.moe/search?url={t}", null).ConfigureAwait(false);
            var responseContent = response.Content;
            using var reader = new StreamReader(await responseContent.ReadAsStreamAsync().ConfigureAwait(false));
            var er = await reader.ReadToEndAsync().ConfigureAwait(false);
            var stuff = JsonSerializer.Deserialize<MoeResponse>(er,
                new JsonSerializerOptions
                {
                    RespectNullableAnnotations = true
                });
            if (!string.IsNullOrWhiteSpace(stuff?.Error))
            {
                await ctx.Channel.SendErrorAsync(
                        Strings.FindAnimeError(ctx.Guild.Id, stuff.Error), Config)
                    .ConfigureAwait(false);
                return;
            }

            var ert = stuff?.Result?.FirstOrDefault();
            if (ert?.Filename is null)
            {
                await ctx.Channel.SendErrorAsync(
                        Strings.NoResultsTryDifferent(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var image = await c2.GetMediaById(ert.Anilist).ConfigureAwait(false);
            var eb = new EmbedBuilder
            {
                ImageUrl = image?.CoverImageLarge, Color = Mewdeko.OkColor
            };
            var te = image?.SeasonInt.ToString()?[2..] is ""
                ? image.SeasonInt.ToString()?[1..]
                : image?.SeasonInt.ToString()?[2..];
            var entitle = image?.EnglishTitle;
            if (image?.EnglishTitle == null) entitle = "None";
            eb.AddField(Strings.AnimeEnglishTitle(ctx.Guild.Id), entitle);
            eb.AddField(Strings.AnimeJapaneseTitle(ctx.Guild.Id), image?.NativeTitle);
            eb.AddField(Strings.AnimeRomanjiTitle(ctx.Guild.Id), image?.RomajiTitle);
            eb.AddField(Strings.AnimeAirStartDate(ctx.Guild.Id), image?.AiringStartDate);
            eb.AddField(Strings.AnimeAirEndDate(ctx.Guild.Id), image?.AiringEndDate);
            eb.AddField(Strings.AnimeSeasonNumber(ctx.Guild.Id), te);
            if (ert.Episode is not 0) eb.AddField(Strings.AnimeEpisode(ctx.Guild.Id), ert.Episode);
            eb.AddField(Strings.AnimeAnilistLink(ctx.Guild.Id), image?.SiteUrl);
            eb.AddField(Strings.AnimeMalLink(ctx.Guild.Id), $"https://myanimelist.net/anime/{image?.IdMal}");
            eb.AddField(Strings.AnimeScore(ctx.Guild.Id), image?.MeanScore);
            eb.AddField(Strings.AnimeDescription(ctx.Guild.Id), image?.DescriptionMd.TrimTo(1024).StripHtml());
            _ = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves and displays information about a character.
        /// </summary>
        /// <param name="chara">The name of the character to search for.</param>
        /// <remarks>
        ///     This command retrieves and displays information about a character, including their full name,
        ///     alternative names, native name, description/backstory, and an image.
        /// </remarks>
        /// <example>
        ///     <code>.charinfo character_name</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task CharInfo([Remainder] string chara)
        {
            var anilist = new Client();
            var te = await anilist.GetCharacterBySearch(chara).ConfigureAwait(false);
            var desc = string.Empty;
            if (te.DescriptionMd is null) desc = "None";
            if (te.DescriptionMd != null) desc = te.DescriptionMd;
            if (te.DescriptionMd is { Length: > 1024 }) desc = te.DescriptionMd.TrimTo(1024);
            var altnames = string.IsNullOrEmpty(te.AlternativeNames.FirstOrDefault())
                ? "None"
                : string.Join(",", te.AlternativeNames);
            var eb = new EmbedBuilder();
            eb.AddField(Strings.AnimeFullName(ctx.Guild.Id), te.FullName);
            eb.AddField(Strings.AnimeAlternativeNames(ctx.Guild.Id), altnames);
            eb.AddField(Strings.AnimeNativeName(ctx.Guild.Id), te.NativeName);
            eb.AddField(Strings.AnimeDescriptionBackstory(ctx.Guild.Id), desc);
            eb.ImageUrl = te.ImageLarge;
            eb.Color = Mewdeko.OkColor;
            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Searches for anime and displays information about the search results.
        /// </summary>
        /// <param name="query">The query to search for.</param>
        /// <remarks>
        ///     This command searches for anime based on the provided query and displays relevant information
        ///     about the search results, including titles, genres, episodes, scores, and more.
        /// </remarks>
        /// <example>
        ///     <code>.anime search_query</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task Anime([Remainder] string query)
        {
            var client = new Jikan();
            var result = await client.SearchAnimeAsync(query);
            if (result is null)
            {
                await ctx.Channel.SendErrorAsync(
                        Strings.AnimeNotFound(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            IEnumerable<Anime> newResult = ((ITextChannel)ctx.Channel).IsNsfw
                ? result.Data
                    .Where(x => x.Genres.Any(malUrl => malUrl.Name != "Hentai"))
                : result.Data;

            if (!newResult.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.NoResultsFound(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(result.Data.Count - 1)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await service.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);


            async Task<PageBuilder> PageFactory(int page)
            {
                try
                {
                    await Task.CompletedTask;
                    var data = newResult.Skip(page).FirstOrDefault();
                    return new PageBuilder()
                        .WithTitle(data?.Titles?.FirstOrDefault()?.Title ?? "Unknown")
                        .WithUrl(data?.Url ?? "")
                        .WithDescription(data?.Synopsis ?? Strings.NoDescriptionAvailable(ctx.Guild.Id))
                        .AddField(Strings.AnimeGenres(ctx.Guild.Id),
                            data?.Genres != null ? string.Join(", ", data.Genres) : "Unknown", true)
                        .AddField(Strings.AnimeEpisodes(ctx.Guild.Id),
                            data?.Episodes.HasValue == true ? data.Episodes : "Unknown", true)
                        .AddField(Strings.AnimeScore(ctx.Guild.Id),
                            data?.Score.HasValue == true ? data.Score : "Unknown", true)
                        .AddField(Strings.AnimeStatus(ctx.Guild.Id), data?.Status ?? "Unknown", true)
                        .AddField(Strings.AnimeType(ctx.Guild.Id), data?.Type ?? "Unknown", true)
                        .AddField(Strings.AnimeStartDate(ctx.Guild.Id),
                            data?.Aired?.From.HasValue == true
                                ? TimestampTag.FromDateTime(data.Aired.From.Value)
                                : "Unknown",
                            true)
                        .AddField(Strings.AnimeEndDate(ctx.Guild.Id),
                            data?.Aired?.To.HasValue == true
                                ? TimestampTag.FromDateTime(data.Aired.To.Value)
                                : "Unknown", true)
                        .AddField(Strings.AnimeRating(ctx.Guild.Id), data?.Rating ?? "Unknown", true)
                        .AddField(Strings.AnimeRank(ctx.Guild.Id), data?.Rank.HasValue == true ? data.Rank : "Unknown",
                            true)
                        .AddField(Strings.AnimePopularity(ctx.Guild.Id),
                            data?.Popularity.HasValue == true ? data.Popularity : "Unknown", true)
                        .AddField(Strings.AnimeMembers(ctx.Guild.Id),
                            data?.Members.HasValue == true ? data.Members : "Unknown", true)
                        .AddField(Strings.AnimeFavorites(ctx.Guild.Id),
                            data?.Favorites.HasValue == true ? data.Favorites : "Unknown", true)
                        .AddField(Strings.AnimeSource(ctx.Guild.Id), data?.Source ?? "Unknown", true)
                        .AddField(Strings.AnimeDuration(ctx.Guild.Id), data?.Duration ?? "Unknown", true)
                        .AddField(Strings.AnimeStudios(ctx.Guild.Id),
                            data?.Studios?.Any() == true
                                ? string.Join(", ", data.Studios.Select(x => x.Name))
                                : "Unknown", true)
                        .AddField(Strings.AnimeProducers(ctx.Guild.Id),
                            data?.Producers?.Any() == true
                                ? string.Join(", ", data.Producers.Select(x => x.Name))
                                : "Unknown",
                            true)
                        .WithOkColor()
                        .WithImageUrl(data?.Images?.JPG?.LargeImageUrl ?? "");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        /// <summary>
        ///     Searches for manga and displays information about the search results.
        /// </summary>
        /// <param name="query">The query to search for.</param>
        /// <remarks>
        ///     This command searches for manga based on the provided query and displays relevant information
        ///     about the search results, including titles, publish dates, volumes, scores, and more.
        /// </remarks>
        /// <example>
        ///     <code>.manga search_query</code>
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Manga([Remainder] string query)
        {
            var msg = await ctx.Channel.SendConfirmAsync(
                Strings.SearchingFor(ctx.Guild.Id, query)).ConfigureAwait(false);
            var jikan = new Jikan();
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
            await service.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                var data = result.Data.Skip(page).FirstOrDefault();
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder()
                    .WithTitle(Format.Bold($"{data?.Titles?.First()?.Title ?? "Unknown"}"))
                    .AddField(Strings.MangaFirstPublishDate(ctx.Guild.Id), data?.Published?.ToString() ?? "Unknown")
                    .AddField(Strings.MangaVolumes(ctx.Guild.Id), data?.Volumes?.ToString() ?? "Unknown")
                    .AddField(Strings.MangaIsStillActive(ctx.Guild.Id), data?.Publishing ?? false)
                    .AddField(Strings.AnimeScore(ctx.Guild.Id), data?.Score?.ToString() ?? "Unknown")
                    .AddField(Strings.MangaUrl(ctx.Guild.Id), data?.Url ?? "")
                    .WithDescription(data?.Background ?? Strings.NoDescriptionAvailable(ctx.Guild.Id))
                    .WithImageUrl(data?.Images?.WebP?.MaximumImageUrl ?? "").WithColor(Mewdeko.OkColor);
            }
        }
    }
}