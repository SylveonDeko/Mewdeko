using CodeHollow.FeedReader;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Searches.Services;
using Serilog;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    /// <summary>
    ///     Module for managing RSS feeds in the guild.
    /// </summary>
    [Group]
    public class FeedCommands(InteractiveService serv) : MewdekoSubmodule<FeedsService>
    {
        /// <summary>
        ///     Adds a new RSS feed to the guild's feed list.
        /// </summary>
        /// <param name="url">The URL of the RSS feed.</param>
        /// <param name="channel">The channel where feed updates will be sent (optional, defaults to the current channel).</param>
        /// <remarks>
        ///     This command adds a new RSS feed to the guild's feed list.
        ///     It requires the Manage Messages permission in the guild.
        /// </remarks>
        /// <example>
        ///     <code>.feedadd https://example.com/feed</code>
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedAdd(string url, [Remainder] ITextChannel? channel = null)
        {
            var success = Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                          (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            if (success)
            {
                channel ??= (ITextChannel)ctx.Channel;
                try
                {
                    await FeedReader.ReadAsync(url).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Information(ex, "Unable to get feeds from that url");
                    success = false;
                }
            }

            if (success)
            {
                success = await Service.AddFeed(ctx.Guild.Id, channel.Id, url);
                if (success)
                {
                    await ReplyConfirmLocalizedAsync("feed_added").ConfigureAwait(false);
                    return;
                }
            }

            await ReplyErrorLocalizedAsync("feed_not_valid").ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes an RSS feed from the guild's feed list.
        /// </summary>
        /// <param name="index">The index of the feed in the list.</param>
        /// <remarks>
        ///     This command removes an RSS feed from the guild's feed list.
        ///     It requires the Manage Messages permission in the guild.
        /// </remarks>
        /// <example>
        ///     <code>.feedremove 1</code>
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedRemove(int index)
        {
            if (await Service.RemoveFeed(ctx.Guild.Id, --index))
                await ReplyConfirmLocalizedAsync("feed_removed").ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("feed_out_of_range").ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a custom message format for feed updates in a specific channel.
        /// </summary>
        /// <param name="index">The index of the feed in the list.</param>
        /// <param name="message">The custom message format.</param>
        /// <remarks>
        ///     This command sets a custom message format for feed updates in a specific channel.
        ///     It requires the Manage Messages permission in the guild.
        /// </remarks>
        /// <example>
        ///     <code>.feedmessage 1 New feed update: %title%</code>
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedMessage(int index, [Remainder] string message)
        {
            if (await Service.AddFeedMessage(ctx.Guild.Id, --index, message).ConfigureAwait(false))
                await ReplyConfirmLocalizedAsync("feed_msg_updated").ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("feed_out_of_range").ConfigureAwait(false);
        }

        /// <summary>
        ///     Tests the retrieval of an RSS feed to check for updates.
        /// </summary>
        /// <param name="index">The index of the feed in the list.</param>
        /// <remarks>
        ///     This command tests the retrieval of an RSS feed to check for updates.
        ///     It requires the Manage Messages permission in the guild.
        /// </remarks>
        /// <example>
        ///     <code>.rsstest 1</code>
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RssTest(int index)
        {
            var feeds = await Service.GetFeeds(ctx.Guild.Id);
            if (feeds.ElementAt(index - 1) is null)
            {
                await ReplyErrorLocalizedAsync("feed_out_of_range").ConfigureAwait(false);
                return;
            }

            await Service.TestRss(feeds.ElementAt(index - 1), ctx.Channel as ITextChannel).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all RSS feeds added to the guild.
        /// </summary>
        /// <remarks>
        ///     This command lists all RSS feeds added to the guild.
        ///     It requires the Manage Messages permission in the guild.
        /// </remarks>
        /// <example>
        ///     <code>.feedlist</code>
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedList()
        {
            var feeds = await Service.GetFeeds(ctx.Guild.Id);

            if (feeds.Count == 0)
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(GetText("feed_no_feed")))
                    .ConfigureAwait(false);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(feeds.Count / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var embed = new PageBuilder()
                    .WithOkColor();
                var i = 0;
                var fs = string.Join("\n", feeds.Skip(page * 10)
                    .Take(10)
                    .Select(x => $"`{page * 10 + ++i}.` <#{x.ChannelId}> {x.Url}"));

                return embed.WithDescription(fs);
            }
        }
    }
}