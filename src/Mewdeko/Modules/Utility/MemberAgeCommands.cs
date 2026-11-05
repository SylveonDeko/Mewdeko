using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Commands that list members by account age or join date.
    /// </summary>
    [Group]
    public class MemberAgeCommands(InteractiveService interactivity, DownloadTracker tracker) : MewdekoSubmodule
    {
        /// <summary>
        ///     Lists the members with the newest accounts.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public Task YoungestAccounts()
        {
            return SendList(Strings.MembersYoungestAccounts(ctx.Guild.Id), false, false);
        }

        /// <summary>
        ///     Lists the members with the oldest accounts.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public Task OldestAccounts()
        {
            return SendList(Strings.MembersOldestAccounts(ctx.Guild.Id), true, false);
        }

        /// <summary>
        ///     Lists the members who joined most recently.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public Task LatestJoiners()
        {
            return SendList(Strings.MembersLatestJoiners(ctx.Guild.Id), false, true);
        }

        /// <summary>
        ///     Lists the members who joined first.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public Task EarliestJoiners()
        {
            return SendList(Strings.MembersEarliestJoiners(ctx.Guild.Id), true, true);
        }

        /// <summary>
        ///     Exports the member list as a text or CSV file.
        /// </summary>
        /// <param name="options">
        ///     Filters and format as key:value pairs: role, norole, joinedbefore, joinedafter, createdbefore,
        ///     createdafter, type (bots or humans), format (txt or csv) and fields.
        /// </param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task MemberDump([Remainder] string? options = null)
        {
            if (!MemberDumpOptions.TryParse(options, text => MemberDumpSource.ResolveRole(ctx.Guild, text),
                    out var parsed, out var bad))
            {
                await ReplyErrorAsync(Strings.MemberDumpBadOption(ctx.Guild.Id, bad)).ConfigureAwait(false);
                return;
            }

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            await tracker.EnsureUsersDownloadedAsync(ctx.Guild).ConfigureAwait(false);
            var (stream, fileName, count) = MemberDumpSource.Build(ctx.Guild,
                await ctx.Guild.GetUsersAsync().ConfigureAwait(false), parsed);

            if (count == 0)
            {
                await ReplyErrorAsync(Strings.MembersNoneFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await using (stream)
            {
                await ctx.Channel.SendFileAsync(stream, fileName, Strings.MemberDumpDone(ctx.Guild.Id, count))
                    .ConfigureAwait(false);
            }
        }

        private async Task SendList(string title, bool ascending, bool byJoin)
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            await tracker.EnsureUsersDownloadedAsync(ctx.Guild).ConfigureAwait(false);
            var members = MemberAgeLists.Sort(await ctx.Guild.GetUsersAsync().ConfigureAwait(false), ascending, byJoin);

            if (members.Count == 0)
            {
                await ReplyErrorAsync(Strings.MembersNoneFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex((members.Count - 1) / MemberAgeLists.PageSize)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);
            return;

            Task<PageBuilder> PageFactory(int page)
            {
                return Task.FromResult(MemberAgeLists.Page(title, members, page, byJoin));
            }
        }
    }
}
