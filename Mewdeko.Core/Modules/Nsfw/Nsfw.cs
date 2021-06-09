using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Collections;
using Mewdeko.Extensions;
using Mewdeko.Modules.Searches.Common;
using Mewdeko.Modules.Searches.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using System.Collections.Generic;
using NHentai.NET.Client;
using NHentai.NET.Models.Books;
using NHentai.NET.Models.Searches;
using KSoftNet;
namespace Mewdeko.Modules.NSFW
{
    // thanks to halitalf for adding autoboob and autobutt features :D
    public class NSFW : MewdekoModule<SearchesService>
    {
        private static readonly ConcurrentHashSet<ulong> _hentaiBombBlacklist = new ConcurrentHashSet<ulong>();
        private readonly IHttpClientFactory _httpFactory;
        public KSoftAPI ksoftapi;

        public NSFW(IHttpClientFactory factory, KSoftAPI kSoftApi)
        {
            _httpFactory = factory;
            ksoftapi = kSoftApi;
        }

        private async Task InternalHentai(IMessageChannel channel, string tag)
        {
            // create a random number generator
            var rng = new MewdekoRandom();

            // get all of the DAPI search types, except first 3 
            // which are safebooru (not nsfw), and 2 furry ones 🤢
            var listOfProviders = Enum.GetValues(typeof(DapiSearchType))
                .Cast<DapiSearchType>()
                .Skip(3)
                .ToList();

            // now try to get an image, if it fails return an error,
            // keep trying for each provider until one of them is successful, or until 
            // we run out of providers. If we run out, then return an error
            ImageCacherObject img;
            do
            {
                // random index of the providers
                var num = rng.Next(0, listOfProviders.Count);
                // get the type
                var type = listOfProviders[num];
                // remove it 
                listOfProviders.RemoveAt(num);
                // get the image
                img = await _service.DapiSearch(tag, type, ctx.Guild?.Id, true).ConfigureAwait(false);
                // if i can't find the image, ran out of providers, or tag is blacklisted
                // return the error
                if (img == null && !listOfProviders.Any())
                {
                    await ReplyErrorLocalizedAsync("no_results").ConfigureAwait(false);
                    return;
                }

            } while (img == null);

            await channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithImageUrl(img.FileUrl)
                .WithDescription($"[{GetText("tag")}: {tag}]({img})"))
                .ConfigureAwait(false);
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        [RequireContext(ContextType.Guild)]
        [RequireNsfw]
        public async Task RedditNSFW(string subreddit)
        {
            var image = await ksoftapi.imagesAPI.RandomReddit(subreddit, false, "year");
            var eb = new EmbedBuilder
            {
                Description = $"[{image.Title}]({image.Source})",
                ImageUrl = image.ImageUrl,
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync("", embed: eb.Build());
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        [RequireContext(ContextType.Guild)]
        [RequireNsfw]
        public async Task NHentai(int num, int page = 0)
        {
            var client = new HentaiClient();
            Book book = await client.SearchBookAsync(num);
            string title = book.Titles.English;
            IEnumerable<string> pages = book.GetPages();
            var tags = new List<string>();
            foreach (var i in book.Tags)
            {
                tags.Add(i.Name);
            }
            if (tags.Contains("lolicon") || tags.Contains("loli"))
            {
                await ctx.Channel.SendErrorAsync("This manga contains loli content and is not allowed by discord TOS!");
                return;
            }

            await ctx.SendPaginatedConfirmAsync(page, cur =>
            {
                var enumerable = pages as string[] ?? pages.ToArray();
                return new EmbedBuilder().WithOkColor()
                    .WithTitle(Format.Bold($"{title}") + $" - {enumerable.ToArray().Length} pages")
                    .WithImageUrl(pages.Skip(cur).FirstOrDefault());
            }, pages.ToArray().Length, 1).ConfigureAwait(false);
        }

        public async Task InternalNHentaiSearch(string search, int page = 1, string type = "popular", string exclude = null)
        {
            var client = new HentaiClient();
            Sort e = Sort.Date;
            switch (type.ToLower())
            {
                case "date":
                    e = Sort.Date;
                    break;
                case "popular":
                    e = Sort.Popular;
                    break;
            }
            NHentai.NET.Models.Searches.SearchResult result = await client.SearchQueryAsync(page, e, search, exclude + " -lolicon -loli");
            if (!result.Books.Any())
            {
                await ctx.Channel.SendErrorAsync("The search returned no results. Try again with a different query!");
                return;
            }
            await ctx.SendPaginatedConfirmAsync(0, cur =>
            {
                var list = new List<string>();
                foreach (var i in result.Books.Skip(cur).FirstOrDefault().Tags)
                {
                    list.Add($"[{i.Name}](https://nhentai.net{i.Url})");
                }
                return new EmbedBuilder().WithOkColor()
                    .WithTitle(result.Books.Skip(cur).FirstOrDefault().Titles.English)
                    .WithDescription(string.Join("|", list.Take(20)))
                    .AddField("NHentai Magic Number", result.Books.Skip(cur).FirstOrDefault().Id)
                    .AddField("NHentai Magic URL", $"https://nhentai.net/g/{result.Books.Skip(cur).FirstOrDefault().Id}")
                    .AddField("Pages", result.Books.Skip(cur).FirstOrDefault().PagesCount)
                    .WithImageUrl(result.Books.Skip(cur).FirstOrDefault().GetCover());
            }, result.Books.ToArray().Length, 1).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        [RequireContext(ContextType.Guild)]
        [RequireNsfw]
        public async Task NHentaiSearch([Remainder] string search)
            => await InternalNHentaiSearch(search);
        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        [RequireContext(ContextType.Guild)]
        [RequireNsfw]
        public async Task NHentaiSearch(string search, [Remainder] string blacklist)
            => await InternalNHentaiSearch(search, 1, blacklist);
        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        [RequireContext(ContextType.Guild)]
        [RequireNsfw]
        public async Task NHentaiSearch(string search, int page)
            => await InternalNHentaiSearch(search, page);
        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        [RequireContext(ContextType.Guild)]
        [RequireNsfw]
        public async Task NHentaiSearch(string search, int page, string type)
            => await InternalNHentaiSearch(search, page, type);
        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        [RequireContext(ContextType.Guild)]
        [RequireNsfw]
        public async Task NHentaiSearch(string search, int page, string type, [Remainder] string blacklist)
            => await InternalNHentaiSearch(search, page, type, blacklist);
        [MewdekoCommand]
        [Usage]
        [Description]
        [Alias]
        [RequireContext(ContextType.Guild)]
        [RequireNsfw]
        private async Task InternalBoobs(IMessageChannel Channel)
        {
            try
            {
                JToken obj;
                using (var http = _httpFactory.CreateClient())
                {
                    obj = JArray.Parse(await http.GetStringAsync($"http://api.oboobs.ru/boobs/{new MewdekoRandom().Next(0, 10330)}").ConfigureAwait(false))[0];
                }
                await Channel.SendMessageAsync($"http://media.oboobs.ru/{obj["preview"]}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Channel.SendErrorAsync(ex.Message).ConfigureAwait(false);
            }
        }
        private async Task InternalButts(IMessageChannel Channel)
        {
            try
            {
                JToken obj;
                using (var http = _httpFactory.CreateClient())
                {
                    obj = JArray.Parse(await http.GetStringAsync($"http://api.obutts.ru/butts/{new MewdekoRandom().Next(0, 4335)}").ConfigureAwait(false))[0];
                }
                await Channel.SendMessageAsync($"http://media.obutts.ru/{obj["preview"]}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Channel.SendErrorAsync(ex.Message).ConfigureAwait(false);
            }
        }

#if !GLOBAL_Mewdeko
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw]
        [RequireContext(ContextType.Guild)]
        [UserPerm(ChannelPerm.ManageMessages)]
        public async Task AutoHentai(int interval = 0, string tags = null)
        {
            Timer t;

            if (interval == 0)
            {
                if (!_service.AutoHentaiTimers.TryRemove(ctx.Channel.Id, out t)) return;

                t.Change(Timeout.Infinite, Timeout.Infinite); //proper way to disable the timer
                await ReplyConfirmLocalizedAsync("stopped").ConfigureAwait(false);
                return;
            }

            if (interval < 20)
                return;

            var tagsArr = tags?.Split('|');

            t = new Timer(async (state) =>
            {
                try
                {
                    if (tagsArr == null || tagsArr.Length == 0)
                        await InternalHentai(ctx.Channel, null).ConfigureAwait(false);
                    else
                        await InternalHentai(ctx.Channel, tagsArr[new MewdekoRandom().Next(0, tagsArr.Length)]).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }, null, interval * 1000, interval * 1000);

            _service.AutoHentaiTimers.AddOrUpdate(ctx.Channel.Id, t, (key, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return t;
            });

            await ReplyConfirmLocalizedAsync("autohentai_started",
                interval,
                string.Join(", ", tagsArr)).ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw]
        [RequireContext(ContextType.Guild)]
        [UserPerm(ChannelPerm.ManageMessages)]
        public async Task AutoBoobs(int interval = 0)
        {
            Timer t;

            if (interval == 0)
            {
                if (!_service.AutoBoobTimers.TryRemove(ctx.Channel.Id, out t)) return;

                t.Change(Timeout.Infinite, Timeout.Infinite); //proper way to disable the timer
                await ReplyConfirmLocalizedAsync("stopped").ConfigureAwait(false);
                return;
            }

            if (interval < 20)
                return;

            t = new Timer(async (state) =>
            {
                try
                {
                    await InternalBoobs(ctx.Channel).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }, null, interval * 1000, interval * 1000);

            _service.AutoBoobTimers.AddOrUpdate(ctx.Channel.Id, t, (key, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return t;
            });

            await ReplyConfirmLocalizedAsync("started", interval).ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        [UserPerm(ChannelPerm.ManageMessages)]
        public async Task AutoButts(int interval = 0)
        {
            Timer t;

            if (interval == 0)
            {
                if (!_service.AutoButtTimers.TryRemove(ctx.Channel.Id, out t)) return;

                t.Change(Timeout.Infinite, Timeout.Infinite); //proper way to disable the timer
                await ReplyConfirmLocalizedAsync("stopped").ConfigureAwait(false);
                return;
            }

            if (interval < 20)
                return;

            t = new Timer(async (state) =>
            {
                try
                {
                    await InternalButts(ctx.Channel).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }, null, interval * 1000, interval * 1000);

            _service.AutoButtTimers.AddOrUpdate(ctx.Channel.Id, t, (key, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return t;
            });

            await ReplyConfirmLocalizedAsync("started", interval).ConfigureAwait(false);
        }
#endif

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        public Task Hentai([Leftover] string tag = null) =>
            InternalHentai(ctx.Channel, tag);

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        public async Task HentaiBomb([Leftover] string tag = null)
        {
            if (!_hentaiBombBlacklist.Add(ctx.Guild?.Id ?? ctx.User.Id))
                return;
            try
            {
                var images = await Task.WhenAll(_service.DapiSearch(tag, DapiSearchType.Gelbooru, ctx.Guild?.Id, true),
                                                _service.DapiSearch(tag, DapiSearchType.Danbooru, ctx.Guild?.Id, true),
                                                _service.DapiSearch(tag, DapiSearchType.Konachan, ctx.Guild?.Id, true),
                                                _service.DapiSearch(tag, DapiSearchType.Yandere, ctx.Guild?.Id, true)).ConfigureAwait(false);

                var linksEnum = images?.Where(l => l != null).ToArray();
                if (images == null || !linksEnum.Any())
                {
                    await ReplyErrorLocalizedAsync("no_results").ConfigureAwait(false);
                    return;
                }

                await ctx.Channel.SendMessageAsync(string.Join("\n\n", linksEnum.Select(x => x.FileUrl))).ConfigureAwait(false);
            }
            finally
            {
                _hentaiBombBlacklist.TryRemove(ctx.Guild?.Id ?? ctx.User.Id);
            }
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        public Task Yandere([Leftover] string tag = null)
            => InternalDapiCommand(tag, DapiSearchType.Yandere, false);

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        public Task Konachan([Leftover] string tag = null)
            => InternalDapiCommand(tag, DapiSearchType.Konachan, false);

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        public Task E621([Leftover] string tag = null)
            => InternalDapiCommand(tag, DapiSearchType.E621, false);

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        public Task Rule34([Leftover] string tag = null)
            => InternalDapiCommand(tag, DapiSearchType.Rule34, false);

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        public Task Danbooru([Leftover] string tag = null)
            => InternalDapiCommand(tag, DapiSearchType.Danbooru, false);

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        public Task Gelbooru([Leftover] string tag = null)
            => InternalDapiCommand(tag, DapiSearchType.Gelbooru, false);

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        public Task Derpibooru([Leftover] string tag = null)
            => InternalDapiCommand(tag, DapiSearchType.Derpibooru, false);

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        public async Task Boobs()
        {
            try
            {
                JToken obj;
                using (var http = _httpFactory.CreateClient())
                {
                    obj = JArray.Parse(await http.GetStringAsync($"http://api.oboobs.ru/boobs/{new MewdekoRandom().Next(0, 12000)}").ConfigureAwait(false))[0];
                }
                await ctx.Channel.SendMessageAsync($"http://media.oboobs.ru/{obj["preview"]}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ctx.Channel.SendErrorAsync(ex.Message).ConfigureAwait(false);
            }
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
        public async Task Butts()
        {
            try
            {
                JToken obj;
                using (var http = _httpFactory.CreateClient())
                {
                    obj = JArray.Parse(await http.GetStringAsync($"http://api.obutts.ru/butts/{new MewdekoRandom().Next(0, 6100)}").ConfigureAwait(false))[0];
                }
                await ctx.Channel.SendMessageAsync($"http://media.obutts.ru/{obj["preview"]}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ctx.Channel.SendErrorAsync(ex.Message).ConfigureAwait(false);
            }
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageMessages)]
        public async Task NsfwTagBlacklist([Leftover] string tag = null)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                var blTags = _service.GetBlacklistedTags(ctx.Guild.Id);
                await ctx.Channel.SendConfirmAsync(GetText("blacklisted_tag_list"),
                    blTags.Any()
                    ? string.Join(", ", blTags)
                    : "-").ConfigureAwait(false);
            }
            else
            {
                tag = tag.Trim().ToLowerInvariant();
                var added = _service.ToggleBlacklistedTag(ctx.Guild.Id, tag);

                if (added)
                    await ReplyConfirmLocalizedAsync("blacklisted_tag_add", tag).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("blacklisted_tag_remove", tag).ConfigureAwait(false);
            }
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public Task NsfwClearCache()
        {
            _service.ClearCache();
            return Context.OkAsync();
        }

        public async Task InternalDapiCommand(string tag, DapiSearchType type, bool forceExplicit)
        {
            ImageCacherObject imgObj;

            imgObj = await _service.DapiSearch(tag, type, ctx.Guild?.Id, forceExplicit).ConfigureAwait(false);

            if (imgObj == null)
                await ReplyErrorLocalizedAsync("no_results").ConfigureAwait(false);
            else
            {
                var embed = new EmbedBuilder().WithOkColor()
                    .WithDescription($"{ctx.User} [{tag ?? "url"}]({imgObj}) ")
                    .WithFooter(efb => efb.WithText(type.ToString()));

                if (Uri.IsWellFormedUriString(imgObj.FileUrl, UriKind.Absolute))
                    embed.WithImageUrl(imgObj.FileUrl);
                else
                    Log.Error($"Image link from {type} is not a proper Url: {imgObj.FileUrl}");

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }
    }
}