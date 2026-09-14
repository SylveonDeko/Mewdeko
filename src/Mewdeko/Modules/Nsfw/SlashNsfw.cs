using System.IO;
using System.Net.Http;
using System.Threading;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using MartineApiNet;
using MartineApiNet.Enums;
using MartineApiNet.Models.Images;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Collections;
using Newtonsoft.Json.Linq;
using NHentaiAPI;
using Refit;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Nsfw;

/// <summary>
///     The sort order used when searching NHentai.
/// </summary>
public enum NHentaiSearchType
{
    /// <summary>
    ///     Sort results by popularity.
    /// </summary>
    Popular,

    /// <summary>
    ///     Sort results by most recent.
    /// </summary>
    Recent
}

/// <summary>
///     Shared helpers for the nsfw slash subgroups: image board fetching, reddit fetching and result delivery.
/// </summary>
/// <param name="client">The http client used to download media.</param>
/// <param name="martineApi">The Martine API used for reddit fetching.</param>
/// <param name="logger">The logger instance.</param>
public abstract class SlashNsfwBase(HttpClient client, MartineApi martineApi, ILogger logger)
    : MewdekoSlashSubmodule<ISearchImagesService>
{
    /// <summary>
    ///     Builds the invite and support buttons when enabled in the bot config.
    /// </summary>
    /// <returns>The built components or null.</returns>
    protected MessageComponent? BuildInviteComponents()
    {
        return Config.ShowInviteButton
            ? new ComponentBuilder()
                .WithButton(style: ButtonStyle.Link,
                    url:
                    "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                    label: "Invite Me!")
                .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko")
                .Build()
            : null;
    }

    /// <summary>
    ///     Splits a space separated tag string into a tag array.
    /// </summary>
    /// <param name="tags">The raw tag string.</param>
    /// <returns>The tag array, empty when no tags were given.</returns>
    protected static string[] SplitTags(string? tags)
    {
        return string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    ///     Sends a message either as an interaction followup or directly to a channel.
    /// </summary>
    /// <param name="channel">The channel to send to, or null to use the interaction followup.</param>
    /// <param name="text">The message text.</param>
    /// <param name="embed">The embed to send.</param>
    /// <param name="components">The components to attach.</param>
    protected Task SendAsync(IMessageChannel? channel, string? text = null, Embed? embed = null,
        MessageComponent? components = null)
    {
        return channel is null
            ? ctx.Interaction.FollowupAsync(text, embed: embed, components: components)
            : channel.SendMessageAsync(text, embed: embed, components: components);
    }

    /// <summary>
    ///     Sends a file either as an interaction followup or directly to a channel.
    /// </summary>
    /// <param name="channel">The channel to send to, or null to use the interaction followup.</param>
    /// <param name="stream">The file stream.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="embed">The embed to send.</param>
    /// <param name="components">The components to attach.</param>
    protected Task SendFileAsync(IMessageChannel? channel, Stream stream, string fileName,
        Embed? embed = null,
        MessageComponent? components = null)
    {
        return channel is null
            ? ctx.Interaction.FollowupWithFileAsync(stream, fileName, embed: embed, components: components)
            : channel.SendFileAsync(stream, fileName, embed: embed, components: components);
    }

    /// <summary>
    ///     Sends an error either as an interaction reply or directly to a channel.
    /// </summary>
    /// <param name="channel">The channel to send to, or null to use the interaction reply.</param>
    /// <param name="text">The error text.</param>
    protected Task SendErrorAsync(IMessageChannel? channel, string text)
    {
        return channel is null
            ? ReplyErrorAsync(text)
            : channel.SendErrorAsync(text, Config);
    }

    /// <summary>
    ///     Fetches a random post from the given subreddit and posts it.
    /// </summary>
    /// <param name="subreddit">The subreddit to fetch from.</param>
    /// <param name="channel">The channel to post to, or null to use the interaction followup.</param>
    protected async Task RedditNsfw(string subreddit, IMessageChannel? channel = null)
    {
        try
        {
            RedditPost? image = null;
            try
            {
                image = await martineApi.RedditApi.GetRandomFromSubreddit(subreddit, Toptype.year)
                    .ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                logger.LogError(
                    "Seems that NSFW Subreddit fetching has failed. Here's the error:\nCode:{ExStatusCode}\nContent: {ExContent}",
                    ex.StatusCode, ex.HasContent ? ex.Content : "No Content.");
                await SendErrorAsync(channel, Strings.NsfwApiFetchError(ctx.Guild.Id));
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
                await SendFileAsync(channel, imgStream, "boobs.mp4", eb.Build(), BuildInviteComponents())
                    .ConfigureAwait(false);
            }
            else
            {
                await SendAsync(channel, embed: eb.Build(), components: BuildInviteComponents())
                    .ConfigureAwait(false);
            }
        }
        catch (ApiException)
        {
            await SendErrorAsync(channel,
                    "Hey guys stop spamming the command! The api can only take so much man. Wait at least a few mins before trying again. If theres an issue join the support server in /vote.")
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Resolves the mp4 url for a redgifs link.
    /// </summary>
    /// <param name="url">The redgifs url.</param>
    /// <returns>The mp4 url, or null when it could not be resolved.</returns>
    protected async Task<string?> GetRedGifMp4(string url)
    {
        const string apiUrl = "https://api.redgifs.com/v1/gifs/";

        var gifId = url[(url.LastIndexOf('/') + 1)..];

        using var httpClient = new HttpClient();
        try
        {
            var response = await httpClient.GetAsync(apiUrl + gifId);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();

            var responseJson = JObject.Parse(responseBody);

            var mp4Url = responseJson["gfyItem"]["content_urls"]["mp4"]["url"].Value<string>();

            return mp4Url;
        }
        catch (HttpRequestException e)
        {
            logger.LogError("Error while fetching RedGif MP4 URL: {0}", e.Message);
            return null;
        }
    }

    /// <summary>
    ///     Fetches an image from an image board and posts it.
    /// </summary>
    /// <param name="tags">The tags to search for.</param>
    /// <param name="forceExplicit">Whether to force explicit results.</param>
    /// <param name="func">The image board search function.</param>
    /// <param name="channel">The channel to post to, or null to use the interaction followup.</param>
    protected async Task InternalDapiCommand(string[] tags,
        bool forceExplicit,
        Func<ulong?, bool, string[], Task<UrlReply?>> func,
        IMessageChannel? channel = null)
    {
        var data = await func(ctx.Guild?.Id, forceExplicit, tags).ConfigureAwait(false);

        if (data is null || !string.IsNullOrWhiteSpace(data.Error))
        {
            await SendErrorAsync(channel, Strings.NoResults(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var footer =
            $"{data.Rating} ({data.Provider}) | {string.Join(" | ", data.Tags.Where(x => !string.IsNullOrWhiteSpace(x)).Take(5))}";

        if (data.Url.IsImage())
        {
            await SendAsync(channel, embed: new EmbedBuilder().WithOkColor().WithImageUrl(data.Url)
                    .WithDescription($"[link]({data.Url})")
                    .WithFooter(footer)
                    .Build(),
                components: BuildInviteComponents()).ConfigureAwait(false);
            return;
        }

        using var sr = await client.GetAsync(data.Url, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);

        var fileSize = sr.Content.Headers.ContentLength ?? -1;

        var maxUploadSize = ctx.Guild?.MaxUploadLimit ?? 26214400;

        if (fileSize > (long)maxUploadSize)
        {
            await SendAsync(channel, embed: new EmbedBuilder().WithErrorColor()
                    .WithTitle(Strings.NsfwFileTooLarge(ctx.Guild.Id))
                    .WithDescription(
                        $"The file is too large to be uploaded ({fileSize / 1048576.0:F2}MB). [View it here instead]({data.Url})")
                    .WithFooter(footer)
                    .Build(),
                components: BuildInviteComponents()).ConfigureAwait(false);
            return;
        }

        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

        if (imgData.Length > (int)maxUploadSize)
        {
            await SendAsync(channel, embed: new EmbedBuilder().WithErrorColor()
                    .WithTitle(Strings.NsfwFileTooLarge(ctx.Guild.Id))
                    .WithDescription(
                        $"The file is too large to be uploaded ({imgData.Length / 1048576.0:F2}MB). [View it here instead]({data.Url})")
                    .WithFooter(footer)
                    .Build(),
                components: BuildInviteComponents()).ConfigureAwait(false);
            return;
        }

        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await SendFileAsync(channel, imgStream, "video.mp4",
            new EmbedBuilder().WithOkColor()
                .WithDescription($"[link]({data.Url})")
                .WithFooter(footer)
                .Build(),
            BuildInviteComponents()).ConfigureAwait(false);
    }
}

/// <summary>
///     Slash commands for nsfw content: image boards, reddit, nhentai and auto posting.
/// </summary>
[Group("nsfw", "NSFW image boards, reddit and nhentai")]
[NsfwCommand(true)]
public class SlashNsfw : MewdekoSlashModuleBase<ISearchImagesService>
{
    /// <summary>
    ///     Manages the blacklist of NSFW tags in the current guild.
    /// </summary>
    /// <param name="tag">
    ///     Optional tag to add or remove from the blacklist. If not provided, displays the current list of
    ///     blacklisted tags.
    /// </param>
    [SlashCommand("tag-blacklist", "Toggle a blacklisted tag, or view the blacklist")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task NsfwTagBlacklist([Summary("tag", "The tag to toggle")] string? tag = null)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            var blTags = await Service.GetBlacklistedTags(ctx.Guild.Id).ConfigureAwait(false);
            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.NsfwBlacklistTitle(ctx.Guild.Id))
                .WithDescription(blTags.Length > 0 ? string.Join(", ", blTags) : "-")
                .Build());
        }
        else
        {
            tag = tag.Trim().ToLowerInvariant();
            var added = await Service.ToggleBlacklistTag(ctx.Guild.Id, tag).ConfigureAwait(false);

            if (added)
                await ReplyConfirmAsync(Strings.BlacklistedTagAdd(ctx.Guild.Id, tag)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.BlacklistedTagRemove(ctx.Guild.Id, tag)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Image board search commands.
    /// </summary>
    /// <param name="client">The http client used to download media.</param>
    /// <param name="martineApi">The Martine API.</param>
    /// <param name="logger">The logger instance.</param>
    [Group("booru", "Fetch images from image boards")]
    public class NsfwBooru(HttpClient client, MartineApi martineApi, ILogger<NsfwBooru> logger)
        : SlashNsfwBase(client, martineApi, logger)
    {
        private static readonly ConcurrentHashSet<ulong> HentaiBombBlacklist = [];
        private static readonly ConcurrentHashSet<ulong> PornBombBlacklist = [];

        /// <summary>
        ///     Fetches NSFW content from the Yandere image board with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("yandere", "Fetch an image from yande.re")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Yandere([Summary("tags", "Space separated tags")] string? tags = null)
        {
            await DeferAsync();
            await InternalDapiCommand(SplitTags(tags), false, Service.Yandere);
        }

        /// <summary>
        ///     Fetches NSFW content from the Konachan image board with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("konachan", "Fetch an image from konachan")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Konachan([Summary("tags", "Space separated tags")] string? tags = null)
        {
            await DeferAsync();
            await InternalDapiCommand(SplitTags(tags), false, Service.Konachan);
        }

        /// <summary>
        ///     Fetches NSFW content from the Sankaku image board with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("sankaku", "Fetch an image from sankaku")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Sankaku([Summary("tags", "Space separated tags")] string? tags = null)
        {
            await DeferAsync();
            await InternalDapiCommand(SplitTags(tags), false, Service.Sankaku);
        }

        /// <summary>
        ///     Fetches NSFW content from the E621 image board with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("e621", "Fetch an image from e621")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task E621([Summary("tags", "Space separated tags")] string? tags = null)
        {
            await DeferAsync();
            await InternalDapiCommand(SplitTags(tags), false, Service.E621);
        }

        /// <summary>
        ///     Fetches NSFW content from the Rule34 image board with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("rule34", "Fetch an image from rule34")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Rule34([Summary("tags", "Space separated tags")] string? tags = null)
        {
            await DeferAsync();
            await InternalDapiCommand(SplitTags(tags), false, Service.Rule34);
        }

        /// <summary>
        ///     Fetches NSFW content from the Danbooru image board with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("danbooru", "Fetch an image from danbooru")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Danbooru([Summary("tags", "Space separated tags")] string? tags = null)
        {
            await DeferAsync();
            await InternalDapiCommand(SplitTags(tags), false, Service.Danbooru);
        }

        /// <summary>
        ///     Fetches NSFW content from the Gelbooru image board with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("gelbooru", "Fetch an image from gelbooru")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Gelbooru([Summary("tags", "Space separated tags")] string? tags = null)
        {
            await DeferAsync();
            await InternalDapiCommand(SplitTags(tags), false, Service.Gelbooru);
        }

        /// <summary>
        ///     Fetches NSFW content from the Derpibooru image board with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("derpibooru", "Fetch an image from derpibooru")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Derpibooru([Summary("tags", "Space separated tags")] string? tags = null)
        {
            await DeferAsync();
            await InternalDapiCommand(SplitTags(tags), false, Service.DerpiBooru);
        }

        /// <summary>
        ///     Fetches content from the Safebooru image board with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("safebooru", "Fetch an image from safebooru")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Safebooru([Summary("tags", "Space separated tags")] string? tags = null)
        {
            await DeferAsync();
            await InternalDapiCommand(SplitTags(tags), false, Service.SafeBooru);
        }

        /// <summary>
        ///     Fetches NSFW content from the Realbooru image board with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("realbooru", "Fetch an image from realbooru")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Realbooru([Summary("tags", "Space separated tags")] string? tags = null)
        {
            await DeferAsync();
            await InternalDapiCommand(SplitTags(tags), false, Service.RealBooru);
        }

        /// <summary>
        ///     Fetches explicit hentai from a random image board with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("hentai", "Fetch hentai from a random image board")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [InteractionRatelimit(5)]
        public async Task Hentai([Summary("tags", "Space separated tags")] string? tags = null)
        {
            await DeferAsync();
            await InternalDapiCommand(SplitTags(tags), true, Service.Hentai);
        }

        /// <summary>
        ///     Initiates a "hentai bomb" by fetching NSFW content from multiple image boards with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content from each source.</param>
        [SlashCommand("hentai-bomb", "Fetch hentai from several image boards at once")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task HentaiBomb([Summary("tags", "Space separated tags")] string? tags = null)
        {
            if (!HentaiBombBlacklist.Add(ctx.Guild?.Id ?? ctx.User.Id))
            {
                await EphemeralReplyErrorAsync(Strings.NsfwBombAlreadyRunning(ctx.Guild.Id));
                return;
            }

            try
            {
                await DeferAsync();
                var tagArray = SplitTags(tags);
                var images = await Task.WhenAll(Service.Yandere(ctx.Guild?.Id, true, tagArray),
                    Service.Danbooru(ctx.Guild?.Id, true, tagArray),
                    Service.Konachan(ctx.Guild?.Id, true, tagArray),
                    Service.Gelbooru(ctx.Guild?.Id, true, tagArray)).ConfigureAwait(false);

                var linksEnum = images.Where(l => l != null).ToArray();
                if (linksEnum.Length == 0)
                {
                    await ReplyErrorAsync(Strings.NoResults(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await ctx.Interaction.FollowupAsync(string.Join("\n", linksEnum.Select(x => x.Url)),
                    components: BuildInviteComponents()).ConfigureAwait(false);
            }
            finally
            {
                HentaiBombBlacklist.TryRemove(ctx.Guild?.Id ?? ctx.User.Id);
            }
        }

        /// <summary>
        ///     Initiates a "porn bomb" by fetching several NSFW images from Realbooru with optional tags.
        /// </summary>
        /// <param name="tags">Optional space separated tags to filter the content.</param>
        [SlashCommand("porn-bomb", "Fetch several images from realbooru at once")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task PornBomb([Summary("tags", "Space separated tags")] string? tags = null)
        {
            if (!PornBombBlacklist.Add(ctx.Guild?.Id ?? ctx.User.Id))
            {
                await EphemeralReplyErrorAsync(Strings.NsfwBombAlreadyRunning(ctx.Guild.Id));
                return;
            }

            try
            {
                await DeferAsync();
                var tagArray = SplitTags(tags);
                var images = await Task.WhenAll(Service.RealBooru(ctx.Guild?.Id, true, tagArray),
                    Service.RealBooru(ctx.Guild?.Id, true, tagArray),
                    Service.RealBooru(ctx.Guild?.Id, true, tagArray),
                    Service.RealBooru(ctx.Guild?.Id, true, tagArray)).ConfigureAwait(false);

                var linksEnum = images.Where(l => l != null).ToArray();
                if (linksEnum.Length == 0)
                {
                    await ReplyErrorAsync(Strings.NoResults(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await ctx.Interaction.FollowupAsync(string.Join("\n", linksEnum.Select(x => x.Url)),
                    components: BuildInviteComponents()).ConfigureAwait(false);
            }
            finally
            {
                PornBombBlacklist.TryRemove(ctx.Guild?.Id ?? ctx.User.Id);
            }
        }
    }

    /// <summary>
    ///     Automatic posting commands that repeat on an interval in the current channel.
    /// </summary>
    /// <param name="client">The http client used to download media.</param>
    /// <param name="martineApi">The Martine API.</param>
    /// <param name="logger">The logger instance.</param>
    [Group("auto", "Automatically post content on an interval")]
    public class NsfwAuto(HttpClient client, MartineApi martineApi, ILogger<NsfwAuto> logger)
        : SlashNsfwBase(client, martineApi, logger)
    {
        private readonly MewdekoRandom rng = new();

        /// <summary>
        ///     Starts or stops automatic posting of hentai at the given interval in the current channel.
        /// </summary>
        /// <param name="interval">The interval in seconds between each post. 0 stops automatic posting.</param>
        /// <param name="tags">Optional tags to filter the content. Separate tag groups with '|'.</param>
        [SlashCommand("hentai", "Start or stop auto hentai posting in this channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(ChannelPermission.ManageMessages)]
        public async Task AutoHentai(
            [Summary("interval", "Seconds between posts, 0 to stop")]
            int interval = 0,
            [Summary("tags", "Tag groups separated by |")]
            string? tags = null)
        {
            Timer? t = null;

            switch (interval)
            {
                case 0 when !Service.AutoHentaiTimers.TryRemove(ctx.Channel.Id, out t):
                    await EphemeralReplyErrorAsync(Strings.NsfwAutoNotRunning(ctx.Guild.Id));
                    return;
                case 0:
                    t!.Change(Timeout.Infinite, Timeout.Infinite);
                    await ReplyConfirmAsync(Strings.NsfwAutohentaiStopped(ctx.Guild.Id));
                    return;
                case < 20:
                    await EphemeralReplyErrorAsync(Strings.NsfwAutoIntervalTooShort(ctx.Guild.Id));
                    return;
            }

            var channel = ctx.Channel;
            t = new Timer(async _ =>
            {
                try
                {
                    if (tags is null || tags.Length == 0)
                    {
                        await InternalDapiCommand([], true, Service.Hentai, channel).ConfigureAwait(false);
                    }
                    else
                    {
                        var groups = tags.Split('|');
                        var group = groups[rng.Next(0, groups.Length)];
                        await InternalDapiCommand(group.Split(' '), true, Service.Hentai, channel)
                            .ConfigureAwait(false);
                    }
                }
                catch
                {
                }
            }, null, interval * 1000, interval * 1000);

            Service.AutoHentaiTimers.AddOrUpdate(ctx.Channel.Id, t, (_, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return t;
            });

            await ReplyConfirmAsync(Strings.NsfwAutohentaiStarted(ctx.Guild.Id, interval, tags ?? string.Empty));
        }

        /// <summary>
        ///     Starts or stops automatic posting from the "boobs" subreddit at the given interval in the current channel.
        /// </summary>
        /// <param name="interval">The interval in seconds between each post. 0 stops automatic posting.</param>
        [SlashCommand("boobs", "Start or stop auto boobs posting in this channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(ChannelPermission.ManageMessages)]
        public async Task AutoBoobs([Summary("interval", "Seconds between posts, 0 to stop")] int interval = 0)
        {
            Timer? t;

            if (interval == 0)
            {
                if (!Service.AutoBoobTimers.TryRemove(ctx.Channel.Id, out t))
                {
                    await EphemeralReplyErrorAsync(Strings.NsfwAutoNotRunning(ctx.Guild.Id));
                    return;
                }

                t!.Change(Timeout.Infinite, Timeout.Infinite);
                await ReplyConfirmAsync(Strings.Stopped(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (interval < 20)
            {
                await EphemeralReplyErrorAsync(Strings.NsfwAutoIntervalTooShort(ctx.Guild.Id));
                return;
            }

            var channel = ctx.Channel;
            t = new Timer(async _ =>
            {
                try
                {
                    await RedditNsfw("boobs", channel).ConfigureAwait(false);
                }
                catch
                {
                }
            }, null, interval * 1000, interval * 1000);

            Service.AutoBoobTimers.AddOrUpdate(ctx.Channel.Id, t, (_, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return t;
            });

            await ReplyConfirmAsync(Strings.Started(ctx.Guild.Id, interval)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Starts or stops automatic posting from the "ass" subreddit at the given interval in the current channel.
        /// </summary>
        /// <param name="interval">The interval in seconds between each post. 0 stops automatic posting.</param>
        [SlashCommand("butts", "Start or stop auto butts posting in this channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(ChannelPermission.ManageMessages)]
        public async Task AutoButts([Summary("interval", "Seconds between posts, 0 to stop")] int interval = 0)
        {
            Timer? t = null;

            switch (interval)
            {
                case 0 when !Service.AutoButtTimers.TryRemove(ctx.Channel.Id, out t):
                    await EphemeralReplyErrorAsync(Strings.NsfwAutoNotRunning(ctx.Guild.Id));
                    return;
                case 0:
                    t!.Change(Timeout.Infinite, Timeout.Infinite);
                    await ReplyConfirmAsync(Strings.Stopped(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case < 20:
                    await EphemeralReplyErrorAsync(Strings.NsfwAutoIntervalTooShort(ctx.Guild.Id));
                    return;
            }

            var channel = ctx.Channel;
            t = new Timer(async _ =>
            {
                try
                {
                    await RedditNsfw("ass", channel).ConfigureAwait(false);
                }
                catch
                {
                }
            }, null, interval * 1000, interval * 1000);

            Service.AutoButtTimers.AddOrUpdate(ctx.Channel.Id, t, (_, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return t;
            });

            await ReplyConfirmAsync(Strings.Started(ctx.Guild.Id, interval)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     NHentai lookup and search commands.
    /// </summary>
    /// <param name="interactivity">Used for sending paginated messages.</param>
    /// <param name="credentials">The bot credentials.</param>
    [Group("nhentai", "Look up or search nhentai")]
    public class NsfwNHentai(InteractiveService interactivity, IBotCredentials credentials)
        : MewdekoSlashSubmodule<ISearchImagesService>
    {
        private NHentaiClient CreateClient()
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
            return new NHentaiClient(credentials.UserAgent, cookies);
        }

        /// <summary>
        ///     Fetches and displays a manga from NHentai by its number.
        /// </summary>
        /// <param name="num">The identification number of the manga to fetch.</param>
        [SlashCommand("get", "Read an nhentai manga by its number")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task NHentai([Summary("number", "The nhentai magic number")] int num)
        {
            await DeferAsync();
            var nHentaiClient = CreateClient();
            var book = await nHentaiClient.GetBookAsync(num).ConfigureAwait(false);
            var title = book.Title.English;
            var pages = book.Images.Pages;
            var tags = book.Tags.Select(i => i.Name).ToList();
            if (tags.Contains("lolicon") || tags.Contains("loli") || tags.Contains("shotacon") ||
                tags.Contains("shota"))
            {
                await ErrorAsync(Strings.NsfwLoliShotaContent(ctx.Guild.Id));
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

            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                    TimeSpan.FromMinutes(60), InteractionResponseType.DeferredChannelMessageWithSource)
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder()
                    .WithTitle(Strings.NsfwTitleFormat(ctx.Guild.Id, Format.Bold($"{title}"),
                        book.Images.Pages.Count))
                    .WithImageUrl(nHentaiClient.GetPictureUrl(book, page + 1))
                    .WithOkColor();
            }
        }

        /// <summary>
        ///     Searches NHentai for manga matching the query, with optional page, sort type and excluded tags.
        /// </summary>
        /// <param name="search">The search query.</param>
        /// <param name="page">The results page number.</param>
        /// <param name="type">The sort type for the search.</param>
        /// <param name="exclude">Tags to exclude from the search, prefixed with - and separated by spaces.</param>
        [SlashCommand("search", "Search nhentai")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task NHentaiSearch(
            [Summary("query", "The search query")] string search,
            [Summary("page", "The results page")] int page = 1,
            [Summary("type", "Sort order")] NHentaiSearchType type = NHentaiSearchType.Popular,
            [Summary("exclude", "Tags to exclude, e.g. -tag1 -tag2")]
            string? exclude = null)
        {
            await DeferAsync();
            var nHentaiClient = CreateClient();
            var result = await nHentaiClient
                .GetSearchPageListAsync($"{search} {exclude} -lolicon -loli -shota -shotacon", page)
                .ConfigureAwait(false);
            if (result.Result.Count == 0)
            {
                await ErrorAsync(Strings.NsfwSearchNoResults(ctx.Guild.Id));
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

            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                    TimeSpan.FromMinutes(60), InteractionResponseType.DeferredChannelMessageWithSource)
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page1)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var book = result.Result.Skip(page1).FirstOrDefault();
                var list = book.Tags
                    .Select(i => $"[{i.Name}](https://nhentai.net{i.Url})").ToList();
                return new PageBuilder().WithOkColor()
                    .WithTitle(book.Title.English)
                    .WithDescription(string.Join("|", list.Take(20)))
                    .AddField("NHentai Magic Number", book.Id)
                    .AddField("NHentai Magic URL", $"https://nhentai.net/g/{book.Id}")
                    .AddField("Pages", book.Images.Pages.Count)
                    .WithImageUrl(nHentaiClient.GetBigCoverUrl(book));
            }
        }
    }

    /// <summary>
    ///     Quick reddit based content commands.
    /// </summary>
    /// <param name="client">The http client used to download media.</param>
    /// <param name="martineApi">The Martine API.</param>
    /// <param name="logger">The logger instance.</param>
    [Group("quick", "Quick reddit content")]
    public class NsfwQuick(HttpClient client, MartineApi martineApi, ILogger<NsfwQuick> logger)
        : SlashNsfwBase(client, martineApi, logger)
    {
        /// <summary>
        ///     Fetches NSFW content from the "HENTAI_GIF" subreddit.
        /// </summary>
        [SlashCommand("gif", "Fetch a hentai gif")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task HentaiGif()
        {
            await DeferAsync();
            await base.RedditNsfw("HENTAI_GIF");
        }

        /// <summary>
        ///     Fetches NSFW content from the "pussy" subreddit.
        /// </summary>
        [SlashCommand("pussy", "Fetch from r/pussy")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Pussy()
        {
            await DeferAsync();
            await base.RedditNsfw("pussy");
        }

        /// <summary>
        ///     Fetches NSFW content from the "anal" subreddit.
        /// </summary>
        [SlashCommand("anal", "Fetch from r/anal")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Anal()
        {
            await DeferAsync();
            await base.RedditNsfw("anal");
        }

        /// <summary>
        ///     Fetches NSFW content from the "porn" subreddit.
        /// </summary>
        [SlashCommand("porn", "Fetch from r/porn")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Porn()
        {
            await DeferAsync();
            await base.RedditNsfw("porn");
        }

        /// <summary>
        ///     Fetches NSFW content from the "bondage" subreddit.
        /// </summary>
        [SlashCommand("bondage", "Fetch from r/bondage")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Bondage()
        {
            await DeferAsync();
            await base.RedditNsfw("bondage");
        }

        /// <summary>
        ///     Fetches NSFW content from the "boobs" subreddit.
        /// </summary>
        [SlashCommand("boobs", "Fetch from r/boobs")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Boobs()
        {
            await DeferAsync();
            await base.RedditNsfw("boobs");
        }

        /// <summary>
        ///     Fetches NSFW content from the "ass" subreddit.
        /// </summary>
        [SlashCommand("butts", "Fetch from r/ass")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Butts()
        {
            await DeferAsync();
            await base.RedditNsfw("ass");
        }

        /// <summary>
        ///     Fetches NSFW content from the given subreddit.
        /// </summary>
        /// <param name="subreddit">The name of the subreddit to fetch from.</param>
        [SlashCommand("reddit", "Fetch a random post from an nsfw subreddit")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [InteractionRatelimit(10)]
        public async Task RedditNsfw([Summary("subreddit", "The subreddit name")] string subreddit)
        {
            await DeferAsync();
            await base.RedditNsfw(subreddit);
        }
    }
}