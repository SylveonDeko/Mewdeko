﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;
using Anilist4Net;
using Anilist4Net.Enums;
using Discord;
using Discord.Commands;
using JikanDotNet;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Interactive;
using Mewdeko.Interactive.Pagination;
using Mewdeko.Modules.Searches.Services;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class AnimeSearchCommands : MewdekoSubmodule<AnimeSearchService>
        {
            private readonly InteractiveService Interactivity;

            public AnimeSearchCommands(InteractiveService service)
            {
                Interactivity = service;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task FindAnime(string e = null)
            {
                var t = string.Empty;
                if (e != null) t = e;
                if (e is null)
                    try
                    {
                        t = ctx.Message.Attachments.FirstOrDefault().Url;
                    }
                    catch
                    {
                        await ctx.Channel.SendErrorAsync("You need to attach a file or use a url with this!");
                        return;
                    }

                var c2 = new Client();
                var client = new HttpClient();
                var response = await client.PostAsync(
                    $"https://api.trace.moe/search?url={t}", null);
                var responseContent = response.Content;
                using (var reader = new StreamReader(await responseContent.ReadAsStreamAsync()))
                {
                    var er = await reader.ReadToEndAsync();
                    var stuff = JsonConvert.DeserializeObject<Root>(er,
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    var ert = stuff.Result1.FirstOrDefault();
                    if (ert.Filename is null)
                        await ctx.Channel.SendErrorAsync(
                            "No results found. Please try a different image/, or avoid cropping the current one.");
                    var image = await c2.GetMediaById(ert.Anilist);
                    var eb = new EmbedBuilder
                    {
                        ImageUrl = image.CoverImageLarge,
                        Color = Mewdeko.OkColor
                    };
                    var te = string.Empty;
                    if (image.SeasonInt.ToString()[2..] is "") te = image.SeasonInt.ToString()[1..];
                    else te = image.SeasonInt.ToString()[2..];
                    var entitle = image.EnglishTitle;
                    if (image.EnglishTitle == null) entitle = "None";
                    eb.AddField("English Title", entitle);
                    eb.AddField("Japanese Title", image.NativeTitle);
                    eb.AddField("Romaji Title", image.RomajiTitle);
                    eb.AddField("Air Start Date", image.AiringStartDate);
                    eb.AddField("Air End Date", image.AiringEndDate);
                    eb.AddField("Season Number", te);
                    if (ert.Episode is not null) eb.AddField("Episode", ert.Episode);
                    eb.AddField("AniList Link", image.SiteUrl);
                    eb.AddField("MAL Link", $"https://myanimelist.net/anime/{image.IdMal}");
                    eb.AddField("Score", image.MeanScore);
                    eb.AddField("Description", image.DescriptionMd.TrimTo(1024));
                    _ = await ctx.Channel.SendMessageAsync("", embed: eb.Build());
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task CharInfo([Remainder] string chara)
            {
                var anilist = new Client();
                var te = await anilist.GetCharacterBySearch(chara);
                var desc = string.Empty;
                if (te.DescriptionMd is null) desc = "None";
                if (te.DescriptionMd != null) desc = te.DescriptionMd;
                if (te.DescriptionMd != null && te.DescriptionMd.Length > 1024) desc = te.DescriptionMd.TrimTo(1024);
                string altnames;
                if (te.AlternativeNames.FirstOrDefault() == "")
                    altnames = "None";
                else
                    altnames = string.Join(",", te.AlternativeNames);
                var eb = new EmbedBuilder();
                eb.AddField(" Full Name", te.FullName);
                eb.AddField("Alternative Names", altnames);
                eb.AddField("Native Name", te.NativeName);
                eb.AddField("Description/Backstory", desc);
                eb.ImageUrl = te.ImageLarge;
                eb.Color = Mewdeko.OkColor;
                await ctx.Channel.SendMessageAsync(embed: eb.Build());
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task Novel([Remainder] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;

                var novelData = await _service.GetNovelData(query).ConfigureAwait(false);

                if (novelData == null)
                {
                    await ReplyErrorLocalizedAsync("failed_finding_novel").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(novelData.Description.Replace("<br>", Environment.NewLine,
                        StringComparison.InvariantCulture))
                    .WithTitle(novelData.Title)
                    .WithUrl(novelData.Link)
                    .WithImageUrl(novelData.ImageUrl)
                    .AddField(efb =>
                        efb.WithName(GetText("authors")).WithValue(string.Join("\n", novelData.Authors))
                            .WithIsInline(true))
                    .AddField(efb => efb.WithName(GetText("status")).WithValue(novelData.Status).WithIsInline(true))
                    .AddField(efb =>
                        efb.WithName(GetText("genres"))
                            .WithValue(string.Join(" ", novelData.Genres.Any() ? novelData.Genres : new[] { "none" }))
                            .WithIsInline(true))
                    .WithFooter(efb => efb.WithText(GetText("score") + " " + novelData.Score));
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(0)]
            public async Task Mal([Remainder] string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return;

                var fullQueryLink = "https://myanimelist.net/profile/" + name;

                var config = Configuration.Default.WithDefaultLoader();
                using (var document = await BrowsingContext.New(config).OpenAsync(fullQueryLink).ConfigureAwait(false))
                {
                    var imageElem = document.QuerySelector(
                        "body > div#myanimelist > div.wrapper > div#contentWrapper > div#content > div.content-container > div.container-left > div.user-profile > div.user-image > img");
                    var imageUrl = ((IHtmlImageElement)imageElem)?.Source ??
                                   "http://icecream.me/uploads/870b03f36b59cc16ebfe314ef2dde781.png";

                    var stats = document.QuerySelectorAll(
                            "body > div#myanimelist > div.wrapper > div#contentWrapper > div#content > div.content-container > div.container-right > div#statistics > div.user-statistics-stats > div.stats > div.clearfix > ul.stats-status > li > span")
                        .Select(x => x.InnerHtml).ToList();

                    var favorites = document.QuerySelectorAll("div.user-favorites > div.di-tc");

                    var favAnime = GetText("anime_no_fav");
                    if (favorites[0].QuerySelector("p") == null)
                        favAnime = string.Join("\n", favorites[0].QuerySelectorAll("ul > li > div.di-tc.va-t > a")
                            .Shuffle()
                            .Take(3)
                            .Select(x =>
                            {
                                var elem = (IHtmlAnchorElement)x;
                                return $"[{elem.InnerHtml}]({elem.Href})";
                            }));

                    var info = document.QuerySelectorAll("ul.user-status:nth-child(3) > li.clearfix")
                        .Select(x => Tuple.Create(x.Children[0].InnerHtml, x.Children[1].InnerHtml))
                        .ToList();

                    var daysAndMean = document.QuerySelectorAll("div.anime:nth-child(1) > div:nth-child(2) > div")
                        .Select(x => x.TextContent.Split(':').Select(y => y.Trim()).ToArray())
                        .ToArray();

                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle(GetText("mal_profile", name))
                        .AddField(efb =>
                            efb.WithName("💚 " + GetText("watching")).WithValue(stats[0]).WithIsInline(true))
                        .AddField(efb =>
                            efb.WithName("💙 " + GetText("completed")).WithValue(stats[1]).WithIsInline(true));
                    if (info.Count < 3)
                        embed.AddField(efb =>
                            efb.WithName("💛 " + GetText("on_hold")).WithValue(stats[2]).WithIsInline(true));
                    embed
                        .AddField(
                            efb => efb.WithName("💔 " + GetText("dropped")).WithValue(stats[3]).WithIsInline(true))
                        .AddField(efb =>
                            efb.WithName("⚪ " + GetText("plan_to_watch")).WithValue(stats[4]).WithIsInline(true))
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
                        .WithDescription($@"
** https://myanimelist.net/animelist/{name} **

**{GetText("top_3_fav_anime")}**
{favAnime}"
                        )
                        .WithUrl(fullQueryLink)
                        .WithImageUrl(imageUrl);

                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
            }

            private static string MalInfoToEmoji(string info)
            {
                info = info.Trim().ToLowerInvariant();
                switch (info)
                {
                    case "gender":
                        return "🚁";
                    case "location":
                        return "🗺";
                    case "last online":
                        return "👥";
                    case "birthday":
                        return "📆";
                    default:
                        return "❔";
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(1)]
            public Task Mal(IGuildUser usr)
            {
                return Mal(usr.Username);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task Anime([Remainder] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;
                var c2 = new Client();
                Media result = null;
                try
                {
                    result = await c2.GetMediaBySearch(query, MediaTypes.ANIME);
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync(
                        "THe anime you searched for wasn't found! Please try a different query!");
                    return;
                }

                var eb = new EmbedBuilder();
                eb.ImageUrl = result.CoverImageLarge;
                var list = new List<string>();
                if (result.Recommendations.Nodes.Any())
                    foreach (var i in result.Recommendations.Nodes)
                    {
                        var t = await c2.GetMediaById(i.Id);
                        if (t is not null) list.Add(t.EnglishTitle);
                    }

                var te = string.Empty;
                if (result.SeasonInt.ToString()[2..] is "") te = result.SeasonInt.ToString()[1..];
                else te = result.SeasonInt.ToString()[2..];
                if (result.DescriptionMd != null) eb.AddField("Description", result.DescriptionMd.TrimTo(1024), true);
                if (result.Genres.Any()) eb.AddField("Genres", string.Join("\n", result.Genres), true);
                if (result.CountryOfOrigin is not null) eb.AddField("Country of Origin", result.CountryOfOrigin, true);
                if (!list.Contains(null) && list.Any())
                    eb.AddField("Recommendations based on this search",
                        string.Join("\n", list.Where(x => !string.IsNullOrWhiteSpace(x)).Take(10)), true);
                eb.AddField("Episodes", result.Episodes, true);
                if (result.SeasonInt is not null) eb.AddField("Seasons", te, true);
                eb.AddField("Air Start Date", result.AiringStartDate, true);
                eb.AddField("Air End Date", result.AiringEndDate, true);
                eb.AddField("Average Score", result.AverageScore, true);
                eb.AddField("Mean Score", result.MeanScore, true);
                eb.AddField("Is this NSFW?", result.IsAdult, true);
                eb.Title = $"{result.EnglishTitle}";
                eb.Color = Mewdeko.OkColor;
                eb.WithUrl(result.SiteUrl);
                await ctx.Channel.SendMessageAsync(embed: eb.Build());
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Manga([Remainder] string query)
            {
                var msg = await ctx.Channel.SendConfirmAsync(
                    $"<a:loading:847706744741691402> Getting results for {query}...");
                IJikan jikan = new Jikan(true);
                var Result = await jikan.SearchManga(query);
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(Result.Results.Count() - 1)
                    .WithDefaultCanceledPage()
                    .WithDefaultEmotes()
                    .Build();
                await msg.DeleteAsync();
                await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

                Task<PageBuilder> PageFactory(int page)
                {
                    return Task.FromResult(new PageBuilder()
                        .WithTitle(Format.Bold($"{Result.Results.Skip(page).FirstOrDefault().Title}"))
                        .AddField("First Publish Date", Result.Results.Skip(page).FirstOrDefault().StartDate)
                        .AddField("Volumes", Result.Results.Skip(page).FirstOrDefault().Volumes)
                        .AddField("Is Still Active", Result.Results.Skip(page).FirstOrDefault().Publishing)
                        .AddField("Score", Result.Results.Skip(page).FirstOrDefault().Score)
                        .AddField("Url", Result.Results.Skip(page).FirstOrDefault().URL)
                        .WithDescription(Result.Results.Skip(page).FirstOrDefault().Description)
                        .WithImageUrl(Result.Results.Skip(page).FirstOrDefault().ImageURL)
                        .WithColor(Mewdeko.OkColor));
                }
            }
        }
    }
}