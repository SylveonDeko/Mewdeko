using System.IO;
using System.Text;
using Discord.Interactions;
using Fergun.Interactive;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Modules.Moderation.Services;

namespace Mewdeko.Modules.Moderation;

/// <summary>
///     Slash commands for moderation cases.
/// </summary>
/// <param name="interactive">The service used to send paginated lists.</param>
[Group("modcase", "Numbered moderation cases")]
public class SlashModCases(InteractiveService interactive) : MewdekoSlashModuleBase<ModCaseService>
{
    /// <summary>
    ///     Shows one case.
    /// </summary>
    /// <param name="caseNumber">The case number.</param>
    [SlashCommand("show", "Shows one case")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task Show(int caseNumber)
    {
        var entry = await Service.GetCaseAsync(ctx.Guild.Id, caseNumber).ConfigureAwait(false);
        if (entry is null)
        {
            await EphemeralReplyErrorAsync(Strings.ModCaseNotFound(ctx.Guild.Id, caseNumber)).ConfigureAwait(false);
            return;
        }

        await RespondAsync(embed: Service.BuildCaseEmbed(entry).Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists a member's cases.
    /// </summary>
    /// <param name="user">The member.</param>
    [SlashCommand("list", "Lists a member's cases")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task List(IUser user)
    {
        var cases = await Service.GetUserCasesAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
        if (cases.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.ModCasesNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = Service.CasesPaginator(ctx.User, ctx.Guild.Id, user.ToString(), cases);
        await interactive.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(10))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Changes the reason of a case.
    /// </summary>
    /// <param name="caseNumber">The case number.</param>
    /// <param name="reason">The new reason.</param>
    [SlashCommand("reason", "Changes the reason of a case")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task Reason(int caseNumber, string reason)
    {
        if (await Service.SetReasonAsync(ctx.Guild.Id, caseNumber, reason).ConfigureAwait(false))
            await ReplyConfirmAsync(Strings.ModCaseReasonSet(ctx.Guild.Id, caseNumber)).ConfigureAwait(false);
        else
            await EphemeralReplyErrorAsync(Strings.ModCaseNotFound(ctx.Guild.Id, caseNumber)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a case.
    /// </summary>
    /// <param name="caseNumber">The case number.</param>
    [SlashCommand("delete", "Deletes a case")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Delete(int caseNumber)
    {
        if (await Service.DeleteCaseAsync(ctx.Guild.Id, caseNumber).ConfigureAwait(false))
            await ReplyConfirmAsync(Strings.ModCaseDeleted(ctx.Guild.Id, caseNumber)).ConfigureAwait(false);
        else
            await EphemeralReplyErrorAsync(Strings.ModCaseNotFound(ctx.Guild.Id, caseNumber)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Exports every case in the server as a CSV file.
    /// </summary>
    [SlashCommand("export", "Exports every case as a CSV file")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Export()
    {
        var cases = await Service.GetAllCasesAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (cases.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.ModCaseExportEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ModCaseService.BuildExport(cases)));
        await ctx.Interaction.RespondWithFileAsync(stream, $"cases-{ctx.Guild.Id}.csv",
            Strings.ModCaseExportDone(ctx.Guild.Id, cases.Count), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows how many cases each moderator has.
    /// </summary>
    [SlashCommand("leaderboard", "Shows how many cases each moderator has")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task Leaderboard()
    {
        var rows = await Service.GetLeaderboardAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.ModStatsNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var lines = rows.Select((x, i) => Strings.ModStatsLine(ctx.Guild.Id, i + 1, x.ModeratorId, x.Count));
        await RespondAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.ModStatsTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", lines))
            .Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets or clears the channel new cases are posted to.
    /// </summary>
    /// <param name="channel">The channel, or nothing to turn it off.</param>
    [SlashCommand("logchannel", "Sets or clears the channel new cases are posted to")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task LogChannel(ITextChannel? channel = null)
    {
        await Service.SetLogChannelAsync(ctx.Guild.Id, channel?.Id).ConfigureAwait(false);
        if (channel is null)
            await ReplyConfirmAsync(Strings.ModCaseLogCleared(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.ModCaseLogSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }
}

/// <summary>
///     Slash commands for reason aliases.
/// </summary>
/// <param name="interactive">The service used to send paginated lists.</param>
[Group("reasonalias", "Short names for punishment reasons")]
public class SlashReasonAliases(InteractiveService interactive) : MewdekoSlashModuleBase<ModCaseService>
{
    /// <summary>
    ///     Saves a short name that stands for a full punishment reason.
    /// </summary>
    /// <param name="name">The short name.</param>
    /// <param name="reason">The full reason.</param>
    [SlashCommand("add", "Saves a short name for a punishment reason")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Add([Summary("name", "The short name")] string name,
        [Summary("reason", "The full reason")] string reason)
    {
        if (name.Length > 32 || name == "-" || name.Any(char.IsWhiteSpace))
        {
            await EphemeralReplyErrorAsync(Strings.ModReasonAliasBadName(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetReasonAliasAsync(ctx.Guild.Id, name, reason).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.ModReasonAliasSet(ctx.Guild.Id, name.ToLowerInvariant()))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the reason aliases.
    /// </summary>
    [SlashCommand("list", "Lists the reason aliases")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task List()
    {
        var aliases = await Service.GetReasonAliasesAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (aliases.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.ModReasonAliasesNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = Service.ReasonAliasesPaginator(ctx.User, ctx.Guild.Id, aliases);
        await interactive.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(10))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a reason alias.
    /// </summary>
    /// <param name="name">The short name.</param>
    [SlashCommand("remove", "Removes a reason alias")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Remove([Summary("name", "The short name")] string name)
    {
        if (await Service.RemoveReasonAliasAsync(ctx.Guild.Id, name).ConfigureAwait(false))
            await ReplyConfirmAsync(Strings.ModReasonAliasRemoved(ctx.Guild.Id, name.ToLowerInvariant()))
                .ConfigureAwait(false);
        else
            await EphemeralReplyErrorAsync(Strings.ModReasonAliasNotFound(ctx.Guild.Id, name.ToLowerInvariant()))
                .ConfigureAwait(false);
    }
}

/// <summary>
///     Slash commands for the DMs members get when punished.
/// </summary>
/// <param name="punish">The service holding the punishment DMs.</param>
[Group("punishdm", "DMs sent to members when they are punished")]
public class SlashPunishDms(UserPunishService punish) : MewdekoSlashModuleBase<ModCaseService>
{
    /// <summary>
    ///     Sets the DM a member gets for a punishment.
    /// </summary>
    /// <param name="action">The punishment.</param>
    /// <param name="template">The message, plain text or embed code, or - to send no DM.</param>
    [SlashCommand("set", "Sets the DM a member gets for a punishment")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Set([Summary("action", "The punishment")] PunishDmAction action,
        [Summary("message", "The message, or - to send no DM")] string template)
    {
        await punish.SetPunishDm(ctx.Guild.Id, action, template).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.ModPunishDmSet(ctx.Guild.Id, action)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the DMs set for each punishment.
    /// </summary>
    [SlashCommand("list", "Shows the DM set for each punishment")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task List()
    {
        var set = await punish.GetPunishDms(ctx.Guild.Id).ConfigureAwait(false);
        await RespondAsync(embed: Service.BuildPunishDmEmbed(ctx.Guild.Id, set)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Puts a punishment DM back to the default.
    /// </summary>
    /// <param name="action">The punishment.</param>
    [SlashCommand("reset", "Puts a punishment DM back to the default")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Reset([Summary("action", "The punishment")] PunishDmAction action)
    {
        await punish.SetPunishDm(ctx.Guild.Id, action, null).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.ModPunishDmReset(ctx.Guild.Id, action)).ConfigureAwait(false);
    }
}

/// <summary>
///     Slash commands for private staff notes on members.
/// </summary>
/// <param name="interactive">The service used to send paginated lists.</param>
[Group("modnote", "Private staff notes on members")]
public class SlashModNotes(InteractiveService interactive) : MewdekoSlashModuleBase<ModCaseService>
{
    /// <summary>
    ///     Adds a private staff note to a member.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="note">The note.</param>
    [SlashCommand("add", "Adds a private staff note to a member")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task Add(IUser user, string note)
    {
        var added = await Service.AddNoteAsync(ctx.Guild.Id, user.Id, ctx.User.Id, note).ConfigureAwait(false);
        await EphemeralReplyConfirmAsync(Strings.ModNoteAdded(ctx.Guild.Id, added.Id, user.ToString()))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the staff notes on a member.
    /// </summary>
    /// <param name="user">The member.</param>
    [SlashCommand("list", "Lists the staff notes on a member")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task List(IUser user)
    {
        var notes = await Service.GetNotesAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
        if (notes.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.ModNotesNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = Service.NotesPaginator(ctx.User, ctx.Guild.Id, user.ToString(), notes);
        await interactive.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(10),
            ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes one staff note.
    /// </summary>
    /// <param name="noteId">The note ID.</param>
    [SlashCommand("remove", "Removes one staff note")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task Remove(int noteId)
    {
        if (await Service.RemoveNoteAsync(ctx.Guild.Id, noteId).ConfigureAwait(false))
            await EphemeralReplyConfirmAsync(Strings.ModNoteRemoved(ctx.Guild.Id, noteId)).ConfigureAwait(false);
        else
            await EphemeralReplyErrorAsync(Strings.ModNoteNotFound(ctx.Guild.Id, noteId)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes every staff note on a member.
    /// </summary>
    /// <param name="user">The member.</param>
    [SlashCommand("clear", "Removes every staff note on a member")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task Clear(IUser user)
    {
        var removed = await Service.ClearNotesAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
        await EphemeralReplyConfirmAsync(Strings.ModNotesCleared(ctx.Guild.Id, removed, user.ToString()))
            .ConfigureAwait(false);
    }
}

/// <summary>
///     Slash commands for member reports.
/// </summary>
[Group("report", "Report members to the staff")]
public class SlashReports : MewdekoSlashModuleBase<ModCaseService>
{
    /// <summary>
    ///     Reports a member to the staff.
    /// </summary>
    /// <param name="user">The member to report.</param>
    /// <param name="reason">Why they are reported.</param>
    /// <param name="messageLink">A link to the offending message.</param>
    [SlashCommand("user", "Reports a member to the staff")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task User(IUser user, string reason, string? messageLink = null)
    {
        if (user.Id == ctx.User.Id)
        {
            await EphemeralReplyErrorAsync(Strings.ModReportSelf(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (result, remaining) = await Service.ReportAsync(ctx.Guild, ctx.User, user, reason, messageLink,
            $"https://discord.com/channels/{ctx.Guild.Id}/{ctx.Channel.Id}").ConfigureAwait(false);

        switch (result)
        {
            case ReportResult.Sent:
                await EphemeralReplyConfirmAsync(Strings.ModReportSent(ctx.Guild.Id)).ConfigureAwait(false);
                break;
            case ReportResult.NoChannel:
                await EphemeralReplyErrorAsync(Strings.ModReportNoChannel(ctx.Guild.Id)).ConfigureAwait(false);
                break;
            case ReportResult.Cooldown:
                await EphemeralReplyErrorAsync(Strings.ModReportCooldown(ctx.Guild.Id, remaining.Humanize(2)))
                    .ConfigureAwait(false);
                break;
            default:
                await EphemeralReplyErrorAsync(Strings.ModReportUnavailable(ctx.Guild.Id)).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Sets or clears the channel member reports are posted to.
    /// </summary>
    /// <param name="channel">The channel, or nothing to turn reports off.</param>
    [SlashCommand("channel", "Sets or clears the channel reports are posted to")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Channel(ITextChannel? channel = null)
    {
        await Service.SetReportChannelAsync(ctx.Guild.Id, channel?.Id).ConfigureAwait(false);
        if (channel is null)
            await ReplyConfirmAsync(Strings.ModReportChannelCleared(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.ModReportChannelSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }
}
