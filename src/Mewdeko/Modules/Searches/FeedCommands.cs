using System.Threading.Tasks;
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
    [Group]
    public class FeedCommands : MewdekoSubmodule<FeedsService>
    {
        private readonly InteractiveService interactivity;

        public FeedCommands(InteractiveService serv) => interactivity = serv;

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
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

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedRemove(int index)
        {
            if (Service.RemoveFeed(ctx.Guild.Id, --index))
                await ReplyConfirmLocalizedAsync("feed_removed").ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("feed_out_of_range").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedMessage(int index, [Remainder] string message)
        {
            if (await Service.AddFeedMessage(ctx.Guild.Id, --index, message).ConfigureAwait(false))
                await ReplyConfirmLocalizedAsync("feed_msg_updated").ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("feed_out_of_range").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task RssTest(int index)
        {
            var feeds = Service.GetFeeds(ctx.Guild.Id);
            if (feeds.ElementAt(index - 1) is null)
            {
                await ReplyErrorLocalizedAsync("feed_out_of_range").ConfigureAwait(false);
                return;
            }

            await Service.TestRss(feeds.ElementAt(index - 1), ctx.Channel as ITextChannel).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedList()
        {
            var feeds = Service.GetFeeds(ctx.Guild.Id);

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

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var embed = new PageBuilder()
                    .WithOkColor();
                var i = 0;
                var fs = string.Join("\n", feeds.Skip(page * 10)
                    .Take(10)
                    .Select(x => $"`{(page * 10) + ++i}.` <#{x.ChannelId}> {x.Url}"));

                return embed.WithDescription(fs);
            }
        }
    }
}