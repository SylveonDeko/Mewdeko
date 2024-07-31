using System.Net.Http;
using System.Threading;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using MartineApiNet;
using MartineApiNet.Enums;
using MartineApiNet.Models.Images;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Collections;
using Newtonsoft.Json.Linq;
using NHentaiAPI;
using Refit;
using Serilog;

namespace Mewdeko.Modules.Nsfw;

/// <summary>
/// The most used module in Mewdeko, nsfw.
/// </summary>
/// <param name="interactivity">Used for sending paginated messages</param>
/// <param name="martineApi">The Martine API</param>
/// <param name="guildSettings">The guild settings service</param>
/// <param name="client">The http client</param>
/// <param name="credentials">The bot credentials</param>
public class Nsfw(
    InteractiveService interactivity,
    MartineApi martineApi,
    GuildSettingsService guildSettings,
    HttpClient client,
    IBotCredentials credentials)
    : MewdekoModuleBase<ISearchImagesService>
{
    private static readonly ConcurrentHashSet<ulong> HentaiBombBlacklist = [];
    private static readonly ConcurrentHashSet<ulong> PornBombBlacklist = [];
    private readonly MewdekoRandom rng = new();


    /// <summary>
    /// Command to retrieve NSFW content from a specified subreddit.
    /// </summary>
    /// <param name="subreddit">The name of the subreddit from which to fetch the content.</param>
    /// <remarks>
    /// This command requires the context to be in a guild.
    /// NSFW (Not Safe For Work) content is required for this command.
    /// The command is rate-limited to 10 uses within a specified time frame.
    /// </remarks>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw, Ratelimit(10)]
    public async Task RedditNsfw(string subreddit)
    {
        var msg = await ctx.Channel.SendConfirmAsync(
            $"{Config.LoadingEmote} Trying to get a post from `{subreddit}`...");
        try
        {
            RedditPost image;
            try
            {
                image = await martineApi.RedditApi.GetRandomFromSubreddit(subreddit, Toptype.year)
                    .ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                await msg.DeleteAsync();
                Log.Error(
                    "Seems that NSFW Subreddit fetching has failed. Here\'s the error:\\nCode:{ExStatusCode}\\nContent: {ExContent}",
                    ex.StatusCode, (ex.HasContent ? ex.Content : "No Content."));
                await ctx.Channel.SendErrorAsync(
                    "Unable to fetch nsfw subreddit. Please check console or report the issue at https://discord.gg/mewdeko.",
                    Config);
                return;
            }

            var eb = new EmbedBuilder
            {
                Description = $"[{image.Data.Title}]({image.Data.PostUrl})",
                ImageUrl = image.Data.ImageUrl.CheckIfNotEmbeddable() ? null : image.Data.ImageUrl,
                Color = Mewdeko.OkColor
            };
            if (image.Data.ImageUrl.CheckIfNotEmbeddable())
            {
                if (image.Data.ImageUrl.Contains("redgifs"))
                    image.Data.ImageUrl = await GetRedGifMp4(image.Data.ImageUrl);
                image.Data.ImageUrl = image.Data.ImageUrl.Replace("gifv", "mp4");
                using var sr = await client.GetAsync(image.Data.ImageUrl, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var imgStream = imgData.ToStream();
                await using var _ = imgStream.ConfigureAwait(false);
                await ctx.Channel.SendFileAsync(imgStream, "boobs.mp4", embed: eb.Build(),
                    components: Config.ShowInviteButton
                        ? new ComponentBuilder()
                            .WithButton(style: ButtonStyle.Link,
                                url:
                                "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                                label: "Invite Me!",
                                emote: "<a:HaneMeow:968564817784877066>".ToIEmote())
                            .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko")
                            .Build()
                        : null).ConfigureAwait(false);
                await msg.DeleteAsync();
            }
            else
            {
                await ctx.Channel.SendMessageAsync(embed: eb.Build(),
                    components: Config.ShowInviteButton
                        ? new ComponentBuilder()
                            .WithButton(style: ButtonStyle.Link,
                                url:
                                "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                                label: "Invite Me!",
                                emote: "<a:HaneMeow:968564817784877066>".ToIEmote())
                            .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko")
                            .Build()
                        : null).ConfigureAwait(false);
                await msg.DeleteAsync();
            }
        }
        catch (ApiException)
        {
            await msg.DeleteAsync();
            await ctx.Channel.SendErrorAsync(
                    $"Hey guys stop spamming the command! The api can only take so much man. Wait at least a few mins before trying again. If theres an issue join the support server in {await guildSettings.GetPrefix(ctx.Guild)}vote.",
                    Config)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Command to fetch and display information about a manga from the NHentai website.
    /// </summary>
    /// <param name="num">The identification number of the manga to fetch.</param>
    /// <remarks>
    /// This command requires the context to be in a guild.
    /// NSFW (Not Safe For Work) content is required for this command.
    /// </remarks>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public async Task NHentai(int num)
    {
        var cookies = new Dictionary<string, string>
        {
            {
                "cf_clearance", credentials.CfClearance
            },
            {
                "csrftoken", credentials.CsrfToken
            }
        };
        var nHentaiClient = new NHentaiClient(credentials.UserAgent, cookies);
        var book = await nHentaiClient.GetBookAsync(num).ConfigureAwait(false);
        var title = book.Title.English;
        var pages = book.Images.Pages;
        var tags = book.Tags.Select(i => i.Name).ToList();
        if (tags.Contains("lolicon") || tags.Contains("loli") || tags.Contains("shotacon") || tags.Contains("shota"))
        {
            await ctx.Channel
                .SendErrorAsync("This manga contains loli/shota content and is not allowed by Discord TOS!", Config)
                .ConfigureAwait(false);
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

        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder()
                .WithTitle($"{Format.Bold($"{title}")} - {book.Images.Pages.Count} pages")
                .WithImageUrl(nHentaiClient.GetPictureUrl(book, page + 1))
                .WithOkColor();
        }
    }

    private async Task InternalNHentaiSearch(string search, int page = 1, string type = "popular",
        string? exclude = null)
    {
        var cookies = new Dictionary<string, string>
        {
            {
                "cf_clearance", credentials.CfClearance
            },
            {
                "csrftoken", credentials.CsrfToken
            }
        };
        var nHentaiClient = new NHentaiClient(credentials.UserAgent, cookies);
        var result = await nHentaiClient
            .GetSearchPageListAsync($"{search} {exclude} -lolicon -loli -shota -shotacon", page).ConfigureAwait(false);
        if (result.Result.Count == 0)
        {
            await ctx.Channel
                .SendErrorAsync("The search returned no results. Try again with a different query!", Config)
                .ConfigureAwait(false);
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

        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page1)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var list = result.Result.Skip(page1).FirstOrDefault().Tags
                .Select(i => $"[{i.Name}](https://nhentai.net{i.Url})").ToList();
            return new PageBuilder().WithOkColor()
                .WithTitle(result.Result.Skip(page1).FirstOrDefault().Title.English)
                .WithDescription(string.Join("|", list.Take(20)))
                .AddField("NHentai Magic Number", result.Result.Skip(page1).FirstOrDefault().Id)
                .AddField("NHentai Magic URL",
                    $"https://nhentai.net/g/{result.Result.Skip(page1).FirstOrDefault().Id}")
                .AddField("Pages", result.Result.Skip(page1).FirstOrDefault().Images.Pages.Count)
                .WithImageUrl(nHentaiClient.GetBigCoverUrl(result.Result.Skip(page1).FirstOrDefault()));
        }
    }

    /// <summary>
    /// Command to fetch and display NSFW content from the "HENTAI_GIF" subreddit.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public Task HentaiGif() => RedditNsfw("HENTAI_GIF");

    /// <summary>
    /// Command to fetch and display NSFW content from the "pussy" subreddit.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public Task Pussy() => RedditNsfw("pussy");

    /// <summary>
    /// Command to fetch and display NSFW content from the "anal" subreddit.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public Task Anal() => RedditNsfw("anal");

    /// <summary>
    /// Command to fetch and display NSFW content from the "porn" subreddit.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public Task Porn() => RedditNsfw("porn");

    /// <summary>
    /// Command to fetch and display NSFW content from the "bondage" subreddit.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public Task Bondage() => RedditNsfw("bondage");

    /// <summary>
    /// Command to search for hentai manga on NHentai based on the provided search query.
    /// </summary>
    /// <param name="search">The search query for NHentai.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public Task NHentaiSearch([Remainder] string search) =>
        InternalNHentaiSearch(search);

    /// <summary>
    /// Command to search for hentai manga on NHentai based on the provided search query and blacklist.
    /// </summary>
    /// <param name="search">The search query for NHentai.</param>
    /// <param name="blacklist">The blacklist for NHentai search.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public Task NHentaiSearch(string search, [Remainder] string blacklist) =>
        InternalNHentaiSearch(search, 1, blacklist);

    /// <summary>
    /// Command to search for hentai manga on NHentai based on the provided search query and page number.
    /// </summary>
    /// <param name="search">The search query for NHentai.</param>
    /// <param name="page">The page number for NHentai search.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public Task NHentaiSearch(string search, int page) =>
        InternalNHentaiSearch(search, page);

    /// <summary>
    /// Command to search for hentai manga on NHentai based on the provided search query, page number, and type.
    /// </summary>
    /// <param name="search">The search query for NHentai.</param>
    /// <param name="page">The page number for NHentai search.</param>
    /// <param name="type">The type of NHentai search.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public Task NHentaiSearch(string search, int page, string type) =>
        InternalNHentaiSearch(search, page, type);

    /// <summary>
    /// Command to search for hentai manga on NHentai based on the provided search query, page number, type, and blacklist.
    /// </summary>
    /// <param name="search">The search query for NHentai.</param>
    /// <param name="page">The page number for NHentai search.</param>
    /// <param name="type">The type of NHentai search.</param>
    /// <param name="blacklist">The blacklist for NHentai search.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), RequireNsfw]
    public Task NHentaiSearch(string search, int page, string type, [Remainder] string blacklist) =>
        InternalNHentaiSearch(search, page, type, blacklist);

    /// <summary>
    /// Command to start or stop automatic posting of NSFW content at specified intervals in the current guild channel.
    /// </summary>
    /// <param name="interval">The interval in seconds between each automatic posting. Set to 0 to stop automatic posting.</param>
    /// <param name="tags">Optional tags to filter the NSFW content. Separate multiple tags with '|'. Leave blank for random NSFW content.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
                    var group = groups[rng.Next(0, groups.Length)];
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

    /// <summary>
    /// Command to start or stop automatic posting of NSFW content from the "boobs" subreddit at specified intervals in the current guild channel.
    /// </summary>
    /// <param name="interval">The interval in seconds between each automatic posting. Set to 0 to stop automatic posting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
                await Boobs().ConfigureAwait(false);
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

    /// <summary>
    /// Command to start or stop automatic posting of NSFW content from the "butts" subreddit at specified intervals in the current guild channel or direct messages.
    /// </summary>
    /// <param name="interval">The interval in seconds between each automatic posting. Set to 0 to stop automatic posting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
                await Butts().ConfigureAwait(false);
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

    /// <summary>
    /// Command to fetch and display NSFW content from the "hentai" subreddit with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the "hentai" subreddit.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Hentai(params string[] tags)
        => InternalDapiCommand(tags, true, Service.Hentai);

    /// <summary>
    /// Command to initiate a "hentai bomb" by fetching and displaying NSFW content from multiple sources with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from each source.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public async Task HentaiBomb(params string[] tags)
    {
        if (!HentaiBombBlacklist.Add(ctx.Guild?.Id ?? ctx.User.Id))
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

            await ctx.Channel.SendMessageAsync(string.Join("\n", linksEnum.Select(x => x.Url)),
                components: Config.ShowInviteButton
                    ? new ComponentBuilder()
                        .WithButton(style: ButtonStyle.Link,
                            url:
                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                            label: "Invite Me!",
                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote())
                        .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko").Build()
                    : null).ConfigureAwait(false);
        }
        finally
        {
            HentaiBombBlacklist.TryRemove(ctx.Guild?.Id ?? ctx.User.Id);
        }
    }

    /// <summary>
    /// Command to initiate a "porn bomb" by fetching and displaying NSFW content from a RealBooru source with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the RealBooru source.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public async Task PornBomb(params string[] tags)
    {
        if (!PornBombBlacklist.Add(ctx.Guild?.Id ?? ctx.User.Id))
            return;
        try
        {
            var images = await Task.WhenAll(Service.RealBooru(ctx.Guild?.Id, true, tags),
                Service.RealBooru(ctx.Guild?.Id, true, tags),
                Service.RealBooru(ctx.Guild?.Id, true, tags),
                Service.RealBooru(ctx.Guild?.Id, true, tags)).ConfigureAwait(false);

            var linksEnum = images.Where(l => l != null).ToArray();
            if (!images.Any())
            {
                await ReplyErrorLocalizedAsync("no_results").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.SendMessageAsync(string.Join("\n", linksEnum.Select(x => x.Url)),
                components: Config.ShowInviteButton
                    ? new ComponentBuilder()
                        .WithButton(style: ButtonStyle.Link,
                            url:
                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                            label: "Invite Me!",
                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote())
                        .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko").Build()
                    : null).ConfigureAwait(false);
        }
        finally
        {
            PornBombBlacklist.TryRemove(ctx.Guild?.Id ?? ctx.User.Id);
        }
    }

    /// <summary>
    /// Command to fetch and display NSFW content from the Yandere image board with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the Yandere image board.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Yandere(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Yandere);

    /// <summary>
    /// Command to fetch and display NSFW content from the Konachan image board with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the Konachan image board.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Konachan(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Konachan);

    /// <summary>
    /// Command to fetch and display NSFW content from the Sankaku image board with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the Sankaku image board.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Sankaku(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Sankaku);

    /// <summary>
    /// Command to fetch and display NSFW content from the E621 image board with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the E621 image board.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task E621(params string[] tags)
        => InternalDapiCommand(tags, false, Service.E621);

    /// <summary>
    /// Command to fetch and display NSFW content from the Rule34 image board with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the Rule34 image board.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Rule34(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Rule34);

    /// <summary>
    /// Command to fetch and display NSFW content from the Danbooru image board with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the Danbooru image board.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Danbooru(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Danbooru);

    /// <summary>
    /// Command to fetch and display NSFW content from the Gelbooru image board with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the Gelbooru image board.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Gelbooru(params string[] tags)
        => InternalDapiCommand(tags, false, Service.Gelbooru);

    /// <summary>
    /// Command to fetch and display NSFW content from the Derpibooru image board with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the Derpibooru image board.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Derpibooru(params string[] tags)
        => InternalDapiCommand(tags, false, Service.DerpiBooru);

    /// <summary>
    /// Command to fetch and display NSFW content from the Safebooru image board with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the Safebooru image board.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Safebooru(params string[] tags)
        => InternalDapiCommand(tags, false, Service.SafeBooru);

    /// <summary>
    /// Command to fetch and display NSFW content from the Realbooru image board with optional tags in the current guild channel or direct messages.
    /// </summary>
    /// <param name="tags">Optional tags to filter the NSFW content from the Realbooru image board.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Realbooru(params string[] tags)
        => InternalDapiCommand(tags, false, Service.RealBooru);

    /// <summary>
    /// Command to fetch and display NSFW content from the "boobs" subreddit in the current guild channel or direct messages.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Boobs() => RedditNsfw("boobs");

    /// <summary>
    /// Command to fetch and display NSFW content from the "ass" subreddit in the current guild channel or direct messages.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Cmd, Aliases]
    [RequireNsfw(Group = "nsfw_or_dm"), RequireContext(ContextType.DM, Group = "nsfw_or_dm")]
    public Task Butts() => RedditNsfw("ass");

    /// <summary>
    /// Command to manage the blacklist of NSFW tags in the current guild.
    /// </summary>
    /// <param name="tag">Optional tag to add or remove from the blacklist. If not provided, displays the current list of blacklisted tags.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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

    private async Task<string> GetRedGifMp4(string url)
    {
        const string apiUrl = "https://api.redgifs.com/v1/gifs/";

        // Extract the gif id from the URL
        var gifId = url[(url.LastIndexOf('/') + 1)..];

        using var httpClient = new HttpClient();
        try
        {
            var response = await httpClient.GetAsync(apiUrl + gifId);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();

            var responseJson = JObject.Parse(responseBody);

            // Parse the MP4 URL from the JSON response
            var mp4Url = responseJson["gfyItem"]["content_urls"]["mp4"]["url"].Value<string>();

            return mp4Url;
        }
        catch (HttpRequestException e)
        {
            Log.Error("Error while fetching RedGif MP4 URL: {0}", e.Message);
            return null;
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

        if (data.Url.IsImage())
        {
            await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder().WithOkColor().WithImageUrl(data.Url)
                    .WithDescription($"[link]({data.Url})")
                    .WithFooter(
                        $"{data.Rating} ({data.Provider}) | {string.Join(" | ", data.Tags.Where(x => !string.IsNullOrWhiteSpace(x)).Take(5))}")
                    .Build(),
                components: Config.ShowInviteButton
                    ? new ComponentBuilder()
                        .WithButton(style: ButtonStyle.Link,
                            url:
                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                            label: "Invite Me!",
                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote())
                        .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko").Build()
                    : null).ConfigureAwait(false);
        }
        else
        {
            using var sr = await client.GetAsync(data.Url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            await ctx.Channel.SendFileAsync(imgStream, "video.mp4",
                embed: new EmbedBuilder().WithOkColor()
                    .WithDescription($"[link]({data.Url})")
                    .WithFooter(
                        $"{data.Rating} ({data.Provider}) | {string.Join(" | ", data.Tags.Where(x => !string.IsNullOrWhiteSpace(x)).Take(5))}")
                    .Build(),
                components: Config.ShowInviteButton
                    ? new ComponentBuilder()
                        .WithButton(style: ButtonStyle.Link,
                            url:
                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                            label: "Invite Me!",
                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote())
                        .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko").Build()
                    : null).ConfigureAwait(false);
        }
    }
}