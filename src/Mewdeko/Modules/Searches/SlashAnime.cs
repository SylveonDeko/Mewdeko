#nullable enable
using System.IO;
using System.Net.Http;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Html.Dom;
using Anilist4Net;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using JikanDotNet;
using MartineApiNet;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Searches.Services;
using NekosBestApiNet;

namespace Mewdeko.Modules.Searches;

/// <summary>
///     Slash commands related to anime, manga, characters and shipping.
/// </summary>
/// <param name="interactivity">The interactive service used for pagination.</param>
/// <param name="martineApi">The Martine API used for ship image generation.</param>
/// <param name="nekosBestApi">The nekos.best API used for random images.</param>
/// <param name="factory">The HTTP client factory.</param>
[Group("anime", "Anime, manga, characters and shipping")]
public class SlashAnime(
    InteractiveService interactivity,
    MartineApi martineApi,
    NekosBestApi nekosBestApi,
    IHttpClientFactory factory)
    : MewdekoSlashModuleBase<SearchesService>
{
    /// <summary>
    ///     Sends a random neko image.
    /// </summary>
    [SlashCommand("neko", "Get a random neko image")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task RandomNeko()
    {
        await DeferAsync().ConfigureAwait(false);
        var req = await nekosBestApi.CategoryApi.Neko().ConfigureAwait(false);
        var em = new EmbedBuilder
        {
            Description = Strings.NekoSource(ctx.Guild.Id, req.Results.FirstOrDefault()?.SourceUrl),
            ImageUrl = req.Results.FirstOrDefault()?.Url,
            Color = Mewdeko.OkColor
        };
        await ctx.Interaction.FollowupAsync(embed: em.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sends a random kitsune image.
    /// </summary>
    [SlashCommand("kitsune", "Get a random kitsune image")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task RandomKitsune()
    {
        await DeferAsync().ConfigureAwait(false);
        var req = await nekosBestApi.CategoryApi.Kitsune().ConfigureAwait(false);
        var em = new EmbedBuilder
        {
            Description = Strings.KitsuneSource(ctx.Guild.Id, req.Results.FirstOrDefault()?.SourceUrl),
            ImageUrl = req.Results.FirstOrDefault()?.Url,
            Color = Mewdeko.OkColor
        };
        await ctx.Interaction.FollowupAsync(embed: em.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sends a random waifu image.
    /// </summary>
    [SlashCommand("waifu", "Get a random waifu image")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task RandomWaifu()
    {
        await DeferAsync().ConfigureAwait(false);
        var req = await nekosBestApi.CategoryApi.Waifu().ConfigureAwait(false);
        var em = new EmbedBuilder
        {
            Description = Strings.WaifuSource(ctx.Guild.Id, req.Results.FirstOrDefault()?.SourceUrl),
            ImageUrl = req.Results.FirstOrDefault()?.Url,
            Color = Mewdeko.OkColor
        };
        await ctx.Interaction.FollowupAsync(embed: em.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Retrieves and displays information about a character.
    /// </summary>
    /// <param name="chara">The name of the character to search for.</param>
    [SlashCommand("char-info", "Look up an anime character")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task CharInfo([Summary("character", "The character name")] string chara)
    {
        await DeferAsync().ConfigureAwait(false);
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
        await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Searches for anime and displays information about the search results.
    /// </summary>
    /// <param name="query">The query to search for.</param>
    [SlashCommand("anime", "Search for an anime")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Anime([Summary("query", "The anime to search for")] string query)
    {
        await DeferAsync().ConfigureAwait(false);
        var client = new Jikan();
        var result = await client.SearchAnimeAsync(query);
        if (result is null)
        {
            await ErrorAsync(Strings.AnimeNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        IEnumerable<Anime> newResult = ((ITextChannel)ctx.Channel).IsNsfw
            ? result.Data
                .Where(x => x.Genres.Any(malUrl => malUrl.Name != "Hentai"))
            : result.Data;

        if (!newResult.Any())
        {
            await ErrorAsync(Strings.NoResultsFound(ctx.Guild.Id)).ConfigureAwait(false);
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
        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
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
                        ? TimestampTag.FromDateTimeOffset(data.Aired.From.Value)
                        : "Unknown",
                    true)
                .AddField(Strings.AnimeEndDate(ctx.Guild.Id),
                    data?.Aired?.To.HasValue == true
                        ? TimestampTag.FromDateTimeOffset(data.Aired.To.Value)
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
    }

    /// <summary>
    ///     Searches for manga and displays information about the search results.
    /// </summary>
    /// <param name="query">The query to search for.</param>
    [SlashCommand("manga", "Search for a manga")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Manga([Summary("query", "The manga to search for")] string query)
    {
        await DeferAsync().ConfigureAwait(false);
        var jikan = new Jikan();
        var result = await jikan.SearchMangaAsync(query).ConfigureAwait(false);
        if (result?.Data is null || result.Data.Count == 0)
        {
            await ErrorAsync(Strings.NoResultsFound(ctx.Guild.Id)).ConfigureAwait(false);
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
        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60))
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

    /// <summary>
    ///     Sends a ship image based on compatibility between two users. When only one user is given, ships them with
    ///     the caller.
    /// </summary>
    /// <param name="user">The first user to be compared.</param>
    /// <param name="user2">The second user to be compared. Defaults to the caller.</param>
    [SlashCommand("ship", "Check the compatibility between two users")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Ship([Summary("user", "The first user")] IUser user,
        [Summary("user2", "The second user, defaults to you")]
        IUser? user2 = null)
    {
        await DeferAsync().ConfigureAwait(false);

        if (user2 is null)
        {
            user2 = user;
            user = ctx.User;
        }

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

        await ctx.Interaction.FollowupWithFileAsync(ms, "ship.png",
                embed: new EmbedBuilder().WithColor(color)
                    .WithDescription(Strings.CompatibilityResult(ctx.Guild.Id, random, response))
                    .WithImageUrl("attachment://ship.png").Build())
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Retrieves and displays information about a MyAnimeList profile, either by username or by a guild user
    ///     whose Discord username is used as the MyAnimeList name. Exactly one of the two must be provided.
    /// </summary>
    /// <param name="name">The username of the MyAnimeList profile.</param>
    /// <param name="usr">The user whose username is used to look up the profile.</param>
    [SlashCommand("mal", "Show a MyAnimeList profile")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Mal([Summary("username", "The MyAnimeList username")] string? name = null,
        [Summary("user", "A user whose username is the MyAnimeList name")]
        IGuildUser? usr = null)
    {
        if (string.IsNullOrWhiteSpace(name) == usr is null)
        {
            await ReplyErrorAsync(Strings.MalSpecifyOne(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        name ??= usr!.Username;

        await DeferAsync().ConfigureAwait(false);

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

        if (stats.Count < 5 || info.Count < 2 || daysAndMean.Length < 2)
        {
            await ReplyErrorAsync(Strings.NoResultsFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.MalProfile(ctx.Guild.Id, name))
            .AddField(efb =>
                efb.WithName(Strings.Watching(ctx.Guild.Id)).WithValue(stats[0]).WithIsInline(true))
            .AddField(efb =>
                efb.WithName(Strings.Completed(ctx.Guild.Id)).WithValue(stats[1]).WithIsInline(true));
        if (info.Count < 3)
            embed.AddField(efb =>
                efb.WithName(Strings.OnHold(ctx.Guild.Id)).WithValue(stats[2]).WithIsInline(true));
        embed
            .AddField(efb =>
                efb.WithName(Strings.Dropped(ctx.Guild.Id)).WithValue(stats[3]).WithIsInline(true))
            .AddField(efb =>
                efb.WithName(Strings.PlanToWatch(ctx.Guild.Id)).WithValue(stats[4]).WithIsInline(true))
            .AddField(efb =>
                efb.WithName(daysAndMean[0][0]).WithValue(daysAndMean[0][1]).WithIsInline(true))
            .AddField(efb =>
                efb.WithName(daysAndMean[1][0]).WithValue(daysAndMean[1][1]).WithIsInline(true))
            .AddField(efb =>
                efb.WithName(info[0].Item1)
                    .WithValue(info[0].Item2.TrimTo(20)).WithIsInline(true))
            .AddField(efb =>
                efb.WithName(info[1].Item1)
                    .WithValue(info[1].Item2.TrimTo(20)).WithIsInline(true));
        if (info.Count > 2)
            embed.AddField(efb =>
                efb.WithName(info[2].Item1)
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

        await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Finds anime information based on an image using the trace.moe API.
    /// </summary>
    /// <param name="url">The image URL to search with.</param>
    /// <param name="attachment">An attached image to search with.</param>
    [SlashCommand("find", "Find which anime a screenshot is from")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task FindAnime([Summary("url", "A direct image url")] string? url = null,
        [Summary("image", "An image to search with")]
        IAttachment? attachment = null)
    {
        var t = attachment?.Url ?? url;
        if (string.IsNullOrWhiteSpace(t))
        {
            await ErrorAsync(Strings.YouNeedToAttachFileOrUrl(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);

        var c2 = new Client();
        using var httpClient = factory.CreateClient();
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
            await ErrorAsync(Strings.FindAnimeError(ctx.Guild.Id, stuff.Error)).ConfigureAwait(false);
            return;
        }

        var ert = stuff?.Result?.FirstOrDefault();
        if (ert?.Filename is null)
        {
            await ErrorAsync(Strings.NoResultsTryDifferent(ctx.Guild.Id)).ConfigureAwait(false);
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
        await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
    }
}