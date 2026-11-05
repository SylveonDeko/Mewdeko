using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Holds the top-level member age group.
/// </summary>
public class SlashMemberAge : MewdekoSlashSubmodule
{
    /// <summary>
    ///     Commands that list members by account age or join date.
    /// </summary>
    [Group("members", "List members by account age or join date")]
    public class MemberAge(InteractiveService interactivity, DownloadTracker tracker) : MewdekoSlashSubmodule
    {
        /// <summary>
        ///     Lists the members with the newest accounts.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("youngest", "Lists the members with the newest accounts")]
        [RequireContext(ContextType.Guild)]
        public Task Youngest()
        {
            return SendList(Strings.MembersYoungestAccounts(ctx.Guild.Id), false, false);
        }

        /// <summary>
        ///     Lists the members with the oldest accounts.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("oldest", "Lists the members with the oldest accounts")]
        [RequireContext(ContextType.Guild)]
        public Task Oldest()
        {
            return SendList(Strings.MembersOldestAccounts(ctx.Guild.Id), true, false);
        }

        /// <summary>
        ///     Lists the members who joined most recently.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("latest", "Lists the members who joined most recently")]
        [RequireContext(ContextType.Guild)]
        public Task Latest()
        {
            return SendList(Strings.MembersLatestJoiners(ctx.Guild.Id), false, true);
        }

        /// <summary>
        ///     Lists the members who joined first.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("earliest", "Lists the members who joined first")]
        [RequireContext(ContextType.Guild)]
        public Task Earliest()
        {
            return SendList(Strings.MembersEarliestJoiners(ctx.Guild.Id), true, true);
        }

        /// <summary>
        ///     Exports the member list as a text or CSV file.
        /// </summary>
        /// <param name="hasRole">Only members with this role.</param>
        /// <param name="lacksRole">Only members without this role.</param>
        /// <param name="joinedBefore">Only members who joined before this date.</param>
        /// <param name="joinedAfter">Only members who joined after this date.</param>
        /// <param name="createdBefore">Only accounts created before this date.</param>
        /// <param name="createdAfter">Only accounts created after this date.</param>
        /// <param name="kind">Whether to include bots, humans or both.</param>
        /// <param name="csv">Whether to export a CSV file instead of plain text.</param>
        /// <param name="fields">Comma separated columns: id, name, display, joined, created, roles.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("dump", "Exports the member list as a text or CSV file")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Dump(
            [Summary("has-role", "Only members with this role")]
            IRole? hasRole = null,
            [Summary("lacks-role", "Only members without this role")]
            IRole? lacksRole = null,
            [Summary("joined-before", "Only members who joined before this date, for example 2025-01-31")]
            string? joinedBefore = null,
            [Summary("joined-after", "Only members who joined after this date")]
            string? joinedAfter = null,
            [Summary("created-before", "Only accounts created before this date")]
            string? createdBefore = null,
            [Summary("created-after", "Only accounts created after this date")]
            string? createdAfter = null,
            [Summary("kind", "Bots, humans or both")]
            MemberDumpKind kind = MemberDumpKind.All,
            [Summary("csv", "Export a CSV file instead of plain text")]
            bool csv = false,
            [Summary("fields", "Columns: id, name, display, joined, created, roles")]
            string? fields = null)
        {
            var text = new List<string>();
            if (joinedBefore != null) text.Add($"joinedbefore:{Quote(joinedBefore)}");
            if (joinedAfter != null) text.Add($"joinedafter:{Quote(joinedAfter)}");
            if (createdBefore != null) text.Add($"createdbefore:{Quote(createdBefore)}");
            if (createdAfter != null) text.Add($"createdafter:{Quote(createdAfter)}");
            if (fields != null) text.Add($"fields:{Quote(fields)}");

            if (!MemberDumpOptions.TryParse(string.Join(' ', text), _ => null, out var options, out var bad))
            {
                await EphemeralReplyErrorAsync(Strings.MemberDumpBadOption(ctx.Guild.Id, bad)).ConfigureAwait(false);
                return;
            }

            options.HasRole = hasRole?.Id;
            options.LacksRole = lacksRole?.Id;
            options.Kind = kind;
            options.Csv = csv;

            await DeferAsync().ConfigureAwait(false);
            await tracker.EnsureUsersDownloadedAsync(ctx.Guild).ConfigureAwait(false);
            var (stream, fileName, count) = MemberDumpSource.Build(ctx.Guild,
                await ctx.Guild.GetUsersAsync().ConfigureAwait(false), options);

            if (count == 0)
            {
                await ReplyErrorAsync(Strings.MembersNoneFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await using (stream)
            {
                await ctx.Interaction.FollowupWithFileAsync(stream, fileName, Strings.MemberDumpDone(ctx.Guild.Id, count))
                    .ConfigureAwait(false);
            }

            static string Quote(string value)
            {
                return $"\"{value.Replace("\"", "")}\"";
            }
        }

        private async Task SendList(string title, bool ascending, bool byJoin)
        {
            await DeferAsync().ConfigureAwait(false);
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

            await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(60),
                InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);
            return;

            Task<PageBuilder> PageFactory(int page)
            {
                return Task.FromResult(MemberAgeLists.Page(title, members, page, byJoin));
            }
        }
    }
}
