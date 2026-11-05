using System.IO;
using System.Text;
using DataModel;
using Discord.Commands;
using Fergun.Interactive;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Modules.Moderation.Services;

namespace Mewdeko.Modules.Moderation;

public partial class Moderation
{
    /// <summary>
    ///     Commands for moderation cases, staff notes and member reports.
    /// </summary>
    /// <param name="interactive">The service used to send paginated lists.</param>
    [Group]
    public class ModCaseCommands(InteractiveService interactive, UserPunishService punish)
        : MewdekoSubmodule<ModCaseService>
    {
        /// <summary>
        ///     Shows one case.
        /// </summary>
        /// <param name="caseNumber">The case number.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task Case(int caseNumber)
        {
            var entry = await Service.GetCaseAsync(ctx.Guild.Id, caseNumber).ConfigureAwait(false);
            if (entry is null)
            {
                await ReplyErrorAsync(Strings.ModCaseNotFound(ctx.Guild.Id, caseNumber)).ConfigureAwait(false);
                return;
            }

            await ctx.Channel.SendMessageAsync(embed: Service.BuildCaseEmbed(entry).Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists a member's cases.
        /// </summary>
        /// <param name="user">The member.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task Cases(IUser user)
        {
            var cases = await Service.GetUserCasesAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
            if (cases.Count == 0)
            {
                await ReplyErrorAsync(Strings.ModCasesNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var paginator = Service.CasesPaginator(ctx.User, ctx.Guild.Id, user.ToString(), cases);
            await interactive.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(10))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Changes the reason of a case.
        /// </summary>
        /// <param name="caseNumber">The case number.</param>
        /// <param name="reason">The new reason.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task CaseReason(int caseNumber, [Remainder] string reason)
        {
            if (await Service.SetReasonAsync(ctx.Guild.Id, caseNumber, reason).ConfigureAwait(false))
                await ReplyConfirmAsync(Strings.ModCaseReasonSet(ctx.Guild.Id, caseNumber)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.ModCaseNotFound(ctx.Guild.Id, caseNumber)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes a case.
        /// </summary>
        /// <param name="caseNumber">The case number.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task CaseDelete(int caseNumber)
        {
            if (await Service.DeleteCaseAsync(ctx.Guild.Id, caseNumber).ConfigureAwait(false))
                await ReplyConfirmAsync(Strings.ModCaseDeleted(ctx.Guild.Id, caseNumber)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.ModCaseNotFound(ctx.Guild.Id, caseNumber)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Exports every case in the server as a CSV file.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task CaseExport()
        {
            var cases = await Service.GetAllCasesAsync(ctx.Guild.Id).ConfigureAwait(false);
            if (cases.Count == 0)
            {
                await ReplyErrorAsync(Strings.ModCaseExportEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ModCaseService.BuildExport(cases)));
            await ctx.Channel.SendFileAsync(stream, $"cases-{ctx.Guild.Id}.csv",
                Strings.ModCaseExportDone(ctx.Guild.Id, cases.Count)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows how many cases each moderator has.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task ModStats()
        {
            var rows = await Service.GetLeaderboardAsync(ctx.Guild.Id).ConfigureAwait(false);
            if (rows.Count == 0)
            {
                await ReplyErrorAsync(Strings.ModStatsNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var lines = rows.Select((x, i) => Strings.ModStatsLine(ctx.Guild.Id, i + 1, x.ModeratorId, x.Count));
            await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.ModStatsTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n", lines))
                .Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets or clears the channel new cases are posted to.
        /// </summary>
        /// <param name="channel">The channel, or nothing to turn it off.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ModCaseLog(ITextChannel? channel = null)
        {
            await Service.SetLogChannelAsync(ctx.Guild.Id, channel?.Id).ConfigureAwait(false);
            if (channel is null)
                await ReplyConfirmAsync(Strings.ModCaseLogCleared(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.ModCaseLogSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a private staff note to a member.
        /// </summary>
        /// <param name="user">The member.</param>
        /// <param name="note">The note.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task Note(IUser user, [Remainder] string note)
        {
            var added = await Service.AddNoteAsync(ctx.Guild.Id, user.Id, ctx.User.Id, note).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.ModNoteAdded(ctx.Guild.Id, added.Id, user.ToString()))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the staff notes on a member.
        /// </summary>
        /// <param name="user">The member.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task Notes(IUser user)
        {
            var notes = await Service.GetNotesAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
            if (notes.Count == 0)
            {
                await ReplyErrorAsync(Strings.ModNotesNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var paginator = Service.NotesPaginator(ctx.User, ctx.Guild.Id, user.ToString(), notes);
            await interactive.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(10))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes one staff note.
        /// </summary>
        /// <param name="noteId">The note ID.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task NoteRemove(int noteId)
        {
            if (await Service.RemoveNoteAsync(ctx.Guild.Id, noteId).ConfigureAwait(false))
                await ReplyConfirmAsync(Strings.ModNoteRemoved(ctx.Guild.Id, noteId)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.ModNoteNotFound(ctx.Guild.Id, noteId)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes every staff note on a member.
        /// </summary>
        /// <param name="user">The member.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task NoteClear(IUser user)
        {
            var removed = await Service.ClearNotesAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.ModNotesCleared(ctx.Guild.Id, removed, user.ToString()))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Reports a member to the staff.
        /// </summary>
        /// <param name="user">The member to report.</param>
        /// <param name="reason">Why they are reported, optionally followed by a message link.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Report(IUser user, [Remainder] string reason)
        {
            if (user.Id == ctx.User.Id)
            {
                await ReplyErrorAsync(Strings.ModReportSelf(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var link = reason.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(x => x.Contains("/channels/"));
            if (link is not null)
                reason = reason.Replace(link, "").Trim();

            var (result, remaining) = await Service.ReportAsync(ctx.Guild, ctx.User, user, reason, link,
                ctx.Message.GetJumpUrl()).ConfigureAwait(false);

            switch (result)
            {
                case ReportResult.Sent:
                    try
                    {
                        await ctx.Message.DeleteAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }

                    await ReplyConfirmAsync(Strings.ModReportSent(ctx.Guild.Id)).ConfigureAwait(false);
                    break;
                case ReportResult.NoChannel:
                    await ReplyErrorAsync(Strings.ModReportNoChannel(ctx.Guild.Id)).ConfigureAwait(false);
                    break;
                case ReportResult.Cooldown:
                    await ReplyErrorAsync(Strings.ModReportCooldown(ctx.Guild.Id, remaining.Humanize(2)))
                        .ConfigureAwait(false);
                    break;
                default:
                    await ReplyErrorAsync(Strings.ModReportUnavailable(ctx.Guild.Id)).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        ///     Sets or clears the channel member reports are posted to.
        /// </summary>
        /// <param name="channel">The channel, or nothing to turn reports off.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ReportLog(ITextChannel? channel = null)
        {
            await Service.SetReportChannelAsync(ctx.Guild.Id, channel?.Id).ConfigureAwait(false);
            if (channel is null)
                await ReplyConfirmAsync(Strings.ModReportChannelCleared(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.ModReportChannelSet(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Saves a short name that stands for a full punishment reason.
        /// </summary>
        /// <param name="name">The short name.</param>
        /// <param name="reason">The full reason.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ReasonAlias(string name, [Remainder] string reason)
        {
            if (name.Length > 32 || name == "-")
            {
                await ReplyErrorAsync(Strings.ModReasonAliasBadName(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetReasonAliasAsync(ctx.Guild.Id, name, reason).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.ModReasonAliasSet(ctx.Guild.Id, name.ToLowerInvariant()))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the reason aliases.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task ReasonAliasList()
        {
            var aliases = await Service.GetReasonAliasesAsync(ctx.Guild.Id).ConfigureAwait(false);
            if (aliases.Count == 0)
            {
                await ReplyErrorAsync(Strings.ModReasonAliasesNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var paginator = Service.ReasonAliasesPaginator(ctx.User, ctx.Guild.Id, aliases);
            await interactive.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(10))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a reason alias.
        /// </summary>
        /// <param name="name">The short name.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ReasonAliasRemove(string name)
        {
            if (await Service.RemoveReasonAliasAsync(ctx.Guild.Id, name).ConfigureAwait(false))
                await ReplyConfirmAsync(Strings.ModReasonAliasRemoved(ctx.Guild.Id, name.ToLowerInvariant()))
                    .ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.ModReasonAliasNotFound(ctx.Guild.Id, name.ToLowerInvariant()))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the DM a member gets for a punishment.
        /// </summary>
        /// <param name="action">The punishment.</param>
        /// <param name="template">The message, plain text or embed code, or - to send no DM.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task PunishDm(PunishDmAction action, [Remainder] string template)
        {
            await punish.SetPunishDm(ctx.Guild.Id, action, template).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.ModPunishDmSet(ctx.Guild.Id, action)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows the DMs set for each punishment.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task PunishDmList()
        {
            var set = await punish.GetPunishDms(ctx.Guild.Id).ConfigureAwait(false);
            await ctx.Channel.SendMessageAsync(embed: Service.BuildPunishDmEmbed(ctx.Guild.Id, set))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Puts a punishment DM back to the default.
        /// </summary>
        /// <param name="action">The punishment.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task PunishDmReset(PunishDmAction action)
        {
            await punish.SetPunishDm(ctx.Guild.Id, action, null).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.ModPunishDmReset(ctx.Guild.Id, action)).ConfigureAwait(false);
        }
    }
}
