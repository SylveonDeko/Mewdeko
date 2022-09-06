using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using MartineApiNet;
using MartineApiNet.Enums;
using MartineApiNet.Models.Images;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Collections;
using Mewdeko.Services.Settings;
using Newtonsoft.Json.Linq;
using NHentaiAPI;
using Refit;
using Serilog;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Nsfw;

public class Nsfw : MewdekoModuleBase<ISearchImagesService>
{
    private static readonly ConcurrentHashSet<ulong> _hentaiBombBlacklist = new();
    private readonly IHttpClientFactory _httpFactory;
    private readonly MewdekoRandom _rng;
    private readonly InteractiveService _interactivity;
    private readonly MartineApi _martineApi;
    private readonly GuildSettingsService _guildSettings;
    private readonly HttpClient _client;
    private readonly BotConfigService _config;
    public static List<RedditCache> Cache { get; set; } = new();

    public Nsfw(IHttpClientFactory factory, InteractiveService interactivity, MartineApi martineApi,
        GuildSettingsService guildSettings, HttpClient client,
        BotConfigService config)
    {
        _martineApi = martineApi;
        _guildSettings = guildSettings;
        _client = client;
        _config = config;
        _interactivity = interactivity;
        _httpFactory = factory;
        _rng = new MewdekoRandom();
    }

    public record RedditCache
    {
        public IGuild Guild { get; set; }
        public string Url { get; set; }
    }

    public static bool CheckIfAlreadyPosted(IGuild guild, string url)
    {
        var e = new RedditCache { Guild = guild, Url = url };
        if (Cache.Count == 0)
        {
            Cache.Add(e);
            return false;
        }

        if (!Cache.Contains(e))
        {
            Cache.Add(e);
            return false;
        }

        if (Cache.Contains(e)) return true;
        return true;
    }

    private async Task InternalBoobs()
    {
        try
        {
            JToken obj;
            using (var http = _httpFactory.CreateClient())
            {
                obj = JArray.Parse(await http
                                         .GetStringAsync(
                                             $"http://api.oboobs.ru/boobs/{new MewdekoRandom().Next(0, 10330)}")
                                         .ConfigureAwait(false))[0];
            }

            await ctx.Channel.SendMessageAsync($"http://media.oboobs.ru/{obj["preview"]}", 
                components: _config.Data.ShowInviteButton ? new ComponentBuilder()
                                                            .WithButton(style: ButtonStyle.Link, 
                                                                url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands", 
                                                                label: "Invite Me!", 
                                                                emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build() : null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ctx.Channel.SendErrorAsync(ex.Message).ConfigureAwait(false);
        }
    }

    private async Task InternalButts(IMessageChannel channel)
    {
        try
        {
            JToken obj;
            using (var http = _httpFactory.CreateClient())
            {
                obj = JArray.Parse(await http
                                         .GetStringAsync(
                                             $"http://api.obutts.ru/butts/{new MewdekoRandom().Next(0, 4335)}")
                                         .ConfigureAwait(false))[0];
            }

            await channel.SendMessageAsync($"https://media.obutts.ru/{obj["preview"]}", 
                components: _config.Data.ShowInviteButton ? new ComponentBuilder()
                                                            .WithButton(style: ButtonStyle.Link, 
                                                                url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands", 
                                                                label: "Invite Me!", 
                                                                emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build() : null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ctx.Channel.SendErrorAsync(ex.Message).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw, Ratelimit(10)]
    public async Task RedditNsfw(string subreddit)
    {
        var msg = await ctx.Channel.SendConfirmAsync($"<a:loading:900381735244689469> Trying to get a post from `{subreddit}`...");
        try
        {
            RedditPost image;
            try
            {
                image = await _martineApi.RedditApi.GetRandomFromSubreddit(subreddit, Toptype.year).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await msg.DeleteAsync();
                Log.Error("Unable to fetch NSFW Subreddit\n{0}", e);
                await ctx.Channel.SendErrorAsync("Unable to fetch nsfw subreddit. Please check console or report the issue at https://discord.gg/mewdeko.");
                return;
            }
            while (CheckIfAlreadyPosted(ctx.Guild, image.Data.ImageUrl))
                image = await _martineApi.RedditApi.GetRandomFromSubreddit(subreddit, Toptype.year).ConfigureAwait(false);
            var eb = new EmbedBuilder
            {
                Description = $"[{image.Data.Title}]({image.Data.PostUrl})",
                ImageUrl = image.Data.ImageUrl.CheckIfNotEmbeddable() ? null : image.Data.ImageUrl,
                Color = Mewdeko.OkColor
            };
            if (image.Data.ImageUrl.CheckIfNotEmbeddable())
            {
                image.Data.ImageUrl = image.Data.ImageUrl.Replace("gifv", "mp4");
                await msg.DeleteAsync();
                using var sr = await _client.GetAsync(image.Data.ImageUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var imgStream = imgData.ToStream();
                await using var _ = imgStream.ConfigureAwait(false);
                await ctx.Channel.SendFileAsync(imgStream, "boobs.mp4", embed: eb.Build(), 
                    components: _config.Data.ShowInviteButton ? new ComponentBuilder()
                                                                .WithButton(style: ButtonStyle.Link, 
                                                                    url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands", 
                                                                    label: "Invite Me!", 
                                                                    emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build() : null);
            }
            else
            {
                await msg.DeleteAsync();
                await ctx.Channel.SendMessageAsync(embed: eb.Build(), 
                    components: _config.Data.ShowInviteButton ? new ComponentBuilder()
                                                                .WithButton(style: ButtonStyle.Link, 
                                                                    url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands", 
                                                                    label: "Invite Me!", 
                                                                    emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build() : null).ConfigureAwait(false);
            }
        }
        catch (ApiException)
        {
            await msg.DeleteAsync();
            await ctx.Channel.SendErrorAsync(
                $"Hey guys stop spamming the command! The api can only take so much man. Wait at least a few mins before trying again. If theres an issue join the support sevrer in {await _guildSettings.GetPrefix(ctx.Guild)}vote.").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public async Task NHentai(int num)
    {
        var client = new NHentaiClient();
        var book = await client.GetBookAsync(num).ConfigureAwait(false);
        var title = book.Title.English;
        var pages = book.Images.Pages;
        var tags = book.Tags.Select(i => i.Name).ToList();
        if (tags.Contains("lolicon") || tags.Contains("loli") || tags.Contains("shotacon") || tags.Contains("shota"))

        {
            await ctx.Channel.SendErrorAsync("This manga contains loli/shota content and is not allowed by discord TOS!").ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(pages.Count - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder()
                   .WithTitle($"{Format.Bold($"{title}")} - {book.Images.Pages.Count} pages")
                   .WithImageUrl(NHentaiClient.GetPictureUrl(book, page + 1).AbsoluteUri)
                   .WithOkColor();
        }
    }

    public async Task InternalNHentaiSearch(string search, int page = 1, string type = "popular",
        string? exclude = null)
    {
        var client = new NHentaiClient();

        var result = await client.GetSearchPageListAsync($"{search} {exclude} -lolicon -loli -shota -shotacon", page).ConfigureAwait(false);
        if (result.Result.Count == 0)
        {
            await ctx.Channel.SendErrorAsync("The search returned no results. Try again with a different query!").ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(result.Result.Count - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page1)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var list = result.Result.Skip(page1).FirstOrDefault().Tags.Select(i => $"[{i.Name}](https://nhentai.net{i.Url})").ToList();
            return new PageBuilder().WithOkColor()
                                    .WithTitle(result.Result.Skip(page1).FirstOrDefault().Title.English)
                                    .WithDescription(string.Join("|", list.Take(20)))
                                    .AddField("NHentai Magic Number", result.Result.Skip(page1).FirstOrDefault().Id)
                                    .AddField("NHentai Magic URL",
                                        $"https://nhentai.net/g/{result.Result.Skip(page1).FirstOrDefault().Id}")
                                    .AddField("Pages", result.Result.Skip(page1).FirstOrDefault().Images.Pages.Count)
                                    .WithImageUrl(client.GetBigCoverUrl(result.Result.Skip(page1).FirstOrDefault()).AbsoluteUri);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public async Task HentaiGif() => await RedditNsfw("HENTAI_GIF").ConfigureAwait(false);

    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public async Task NHentaiSearch([Remainder] string search) => await InternalNHentaiSearch(search).ConfigureAwait(false);

    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public async Task NHentaiSearch(string search, [Remainder] string blacklist) => await InternalNHentaiSearch(search, 1, blacklist).ConfigureAwait(false);

    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public async Task NHentaiSearch(string search, int page) => await InternalNHentaiSearch(search, page).ConfigureAwait(false);

    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public async Task NHentaiSearch(string search, int page, string type) => await InternalNHentaiSearch(search, page, type).ConfigureAwait(false);

    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public async Task NHentaiSearch(string search, int page, string type, [Remainder] string blacklist) => await InternalNHentaiSearch(search, page, type, blacklist).ConfigureAwait(false);
    [Cmd, Aliases]
    [RequireNsfw]
    [RequireContext(ContextType.Guild)]
    [UserPerm(ChannelPermission.ManageMessages)]
    public async Task AutoHentai(int interval = 0, [Remainder] string? tags = null)
    {
        // ReSharper disable once RedundantAssignment
        Timer t = default;

        switch (interval)
        {
            case 0 when !Service.AutoHentaiTimers.TryRemove(ctx.Channel.Id, out t):
                return;
            case 0:
                t.Change(Timeout.Infinite, Timeout.Infinite); //proper way to disable the timer
                await ReplyConfirmLocalizedAsync("stopped").ConfigureAwait(false);
                return;
            case < 20:
                return;
        }

        t = new Timer(async _ =>
        {
            try
            {
                if (tags is null || tags.Length == 0)
                {
                    await InternalDapiCommand(null, true, Service.Hentai).ConfigureAwait(false);
                }
                else
                {
                    var groups = tags.Split('|');
                    var group = groups[_rng.Next(0, groups.Length)];
                    await InternalDapiCommand(group.Split(' '), true, Service.Hentai).ConfigureAwait(false);
                }
            }
            catch
            {
                // ignored
            }
        }, null, interval * 1000, interval * 1000);

        Service.AutoHentaiTimers.AddOrUpdate(ctx.Channel.Id, t, (_, old) =>
        {
            old.Change(Timeout.Infinite, Timeout.Infinite);
            return t;
        });

        await ReplyConfirmLocalizedAsync("autohentai_started",
            interval,
            string.Join(", ", tags)).ConfigureAwait(false);
    }

    [Cmd, Aliases]
    [RequireNsfw]
    [RequireContext(ContextType.Guild)]
    [UserPerm(ChannelPermission.ManageMessages)]
    public async Task AutoBoobs(int interval = 0)
    {
        Timer t;

        if (interval == 0)
        {
            if (!Service.AutoHentaiTimers.TryRemove(ctx.Channel.Id, out t)) return;

            t.Change(Timeout.Infinite, Timeout.Infinite); //proper way to disable the timer
            await ReplyConfirmLocalizedAsync("stopped").ConfigureAwait(false);
            return;
        }

        t = new Timer(async _ =>
        {
            try
            {
                await InternalBoobs().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }, null, interval * 1000, interval * 1000);

        Service.AutoBoobTimers.AddOrUpdate(ctx.Channel.Id, t, (_, old) =>
        {
            old.Change(Timeout.Infinite, Timeout.Infinite);
            return t;
        });

        await ReplyConfirmLocalizedAsync("started", interval).ConfigureAwait(false);
    }

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    [UserPerm(ChannelPermission.ManageMessages)]
    public async Task AutoButts(int interval = 0)
    {
        // ReSharper disable once RedundantAssignment
        Timer t = default;

        switch (interval)
        {
            case 0 when !Service.AutoButtTimers.TryRemove(ctx.Channel.Id, out t):
                return;
            case 0:
                t.Change(Timeout.Infinite, Timeout.Infinite); //proper way to disable the timer
                await ReplyConfirmLocalizedAsync("stopped").ConfigureAwait(false);
                return;
            case < 20:
                return;
        }

        t = new Timer(async _ =>
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

        Service.AutoButtTimers.AddOrUpdate(ctx.Channel.Id, t, (_, old) =>
        {
            old.Change(Timeout.Infinite, Timeout.Infinite);
            return t;
        });

        await ReplyConfirmLocalizedAsync("started", interval).ConfigureAwait(false);
    }

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Hentai(params string[] tags)
        => InternalDapiCommand(tags, true, Service.Hentai);

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public async Task HentaiBomb(params string[] tags)
    {
        if (!_hentaiBombBlacklist.Add(ctx.Guild?.Id ?? ctx.User.Id))
            return;
        try
        {
            var images = await Task.WhenAll(Service.Yandere(ctx.Guild?.Id, true, tags),
                Service.Danbooru(ctx.Guild?.Id, true, tags),
                Service.Konachan(ctx.Guild?.Id, true, tags),
                Service.Gelbooru(ctx.Guild?.Id, true, tags)).ConfigureAwait(false);

            var linksEnum = images.Where(l => l != null).ToArray();
            if (!images.Any())
            {
                await ReplyErrorLocalizedAsync("no_results").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.SendMessageAsync(string.Join("\n\n", linksEnum.Select(x => x.Url)), 
                         components: _config.Data.ShowInviteButton ? new ComponentBuilder()
                                                                     .WithButton(style: ButtonStyle.Link, 
                                                                         url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands", 
                                                                         label: "Invite Me!", 
                                                                         emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build() : null)
                     .ConfigureAwait(false);
        }
        finally
        {
            _hentaiBombBlacklist.TryRemove(ctx.Guild?.Id ?? ctx.User.Id);
        }
    }

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Yandere(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Yandere);

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Konachan(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Konachan);

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Sankaku(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Sankaku);

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task E621(params string[] tags)
        => InternalDapiCommand(tags, false, Service.E621);

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Rule34(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Rule34);

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Danbooru(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Danbooru);

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Gelbooru(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Gelbooru);

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Derpibooru(params string[] tags)
        => InternalDapiCommand(tags, false, Service.DerpiBooru);

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Safebooru(params string[] tags)
        => InternalDapiCommand(tags, false, Service.SafeBooru);

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public async Task Boobs() => await RedditNsfw("boobs").ConfigureAwait(false);

    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public async Task Butts() => await RedditNsfw("ass").ConfigureAwait(false);

    [Cmd, Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task NsfwTagBlacklist([Remainder] string? tag = null)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            var blTags = await Service.GetBlacklistedTags(ctx.Guild.Id).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(GetText("blacklisted_tag_list"),
                blTags.Length > 0
                    ? string.Join(", ", blTags)
                    : "-").ConfigureAwait(false);
        }
        else
        {
            tag = tag.Trim().ToLowerInvariant();
            var added = await Service.ToggleBlacklistTag(ctx.Guild.Id, tag).ConfigureAwait(false);

            if (added)
                await ReplyConfirmLocalizedAsync("blacklisted_tag_add", tag).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("blacklisted_tag_remove", tag).ConfigureAwait(false);
        }
    }

    private async Task InternalDapiCommand(string[] tags,
        bool forceExplicit,
        Func<ulong?, bool, string[], Task<UrlReply?>> func)
    {
        var data = await func(ctx.Guild?.Id, forceExplicit, tags).ConfigureAwait(false);

        if (data is null || !string.IsNullOrWhiteSpace(data.Error))
        {
            await ReplyErrorLocalizedAsync("no_results").ConfigureAwait(false);
            return;
        }

        await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder().WithOkColor().WithImageUrl(data.Url)
                                                                    .WithDescription($"[link]({data.Url})")
                                                                    .WithFooter(
                                                                        $"{data.Rating} ({data.Provider}) | {string.Join(" | ", data.Tags.Where(x => !string.IsNullOrWhiteSpace(x)).Take(5))}")
                                                                    .Build(), 
            components: _config.Data.ShowInviteButton ? new ComponentBuilder()
                                                        .WithButton(style: ButtonStyle.Link, 
                                                            url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands", 
                                                            label: "Invite Me!", 
                                                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build() : null).ConfigureAwait(false);
    }
}