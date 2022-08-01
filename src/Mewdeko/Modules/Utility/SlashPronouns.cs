using Discord.Interactions;
using Mewdeko.Common.Attributes.SlashCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Modules.Utility.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Utility;

[Group("pronouns", "pronouns")]
public class SlashPronoun : MewdekoSlashSubmodule<PronounsService>
{
    private readonly DbService _db;
    private readonly Mewdeko _bot;
    private readonly BlacklistService _bss;
    public SlashPronoun(DbService db, Mewdeko bot, BlacklistService bss)
    {
        _db = db;
        _bot = bot;
        _bss = bss;
    }

    [ComponentInteraction("pronouns_overwrite", true)]
    public async Task OverwritePronouns() => await RespondWithModalAsync<PronounsModal>("pronouns_overwrite_modal").ConfigureAwait(false);

    [ComponentInteraction("pronouns_overwrite_clear", true)]
    public async Task ClearPronounsOverwrite()
    {
        await using var uow = _db.GetDbContext();
        var user = await uow.GetOrCreateUser(ctx.User).ConfigureAwait(false);
        if (await PronounsDisabled(user).ConfigureAwait(false)) return;
        user.Pronouns = "";
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await ConfirmLocalizedAsync("pronouns_cleared_self").ConfigureAwait(false);
    }

    [ModalInteraction("pronouns_overwrite_modal", true)]
    public async Task PronounsOverwriteModal(PronounsModal modal)
    {
        await using var uow = _db.GetDbContext();
        var user = await uow.GetOrCreateUser(ctx.User).ConfigureAwait(false);
        if (await PronounsDisabled(user).ConfigureAwait(false)) return;
        user.Pronouns = modal.Pronouns;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await ConfirmLocalizedAsync("pronouns_internal_update", user.Pronouns).ConfigureAwait(false);
    }

    [ComponentInteraction("pronouns_report.*;", true)]
    public async Task ReportPronouns(string sId)
    {
        await using var uow = _db.GetDbContext();
        var reporter = await uow.GetOrCreateUser(ctx.User).ConfigureAwait(false);

        if (await PronounsDisabled(reporter).ConfigureAwait(false)) return;

        var id = ulong.Parse(sId);
        var user = await uow.DiscordUser.FirstOrDefaultAsync(x => x.UserId == id).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(user?.Pronouns)) return;

        var channel = await ctx.Client.GetChannelAsync(_bot.Credentials.PronounAbuseReportChannelId)
                               .ConfigureAwait(false);
        var eb = new EmbedBuilder().WithAuthor(ctx.User).WithTitle("Pronoun abuse report")
                                   .AddField("Reported User", $"{user.Username} ({user.UserId}, <@{user.UserId}>)")
                                   .AddField("Reporter", $"{reporter.Username} ({reporter.UserId}, <@{reporter.UserId}>)")
                                   .AddField("Pronouns Cleared Reason",
                                       string.IsNullOrWhiteSpace(user.PronounsClearedReason)
                                           ? "Never Cleared"
                                           : user.PronounsClearedReason)
                                   .AddField("Pronouns", user.Pronouns)
                                   .WithFooter($"reported in the guild {ctx.Guild?.Id ?? 0} on shard {(ctx.Client as DiscordSocketClient)?.ShardId ?? 0}")
                                   .WithErrorColor();
        var cb = new ComponentBuilder()
                 .WithButton("Reported User", "reported_row", ButtonStyle.Secondary, disabled: true)
                 .WithButton("Clear Pronouns", $"pronouns_clear:{user.UserId},false", ButtonStyle.Danger)
                 .WithButton("Clear and Disable Pronouns", $"pronouns_clear:{user.UserId},true", ButtonStyle.Danger)
                 .WithButton("Blacklist User", $"pronouns_blacklist:{user.UserId}", ButtonStyle.Danger)
                 .WithButton("DM User", $"pronouns_reportdm:{user.UserId}", ButtonStyle.Danger)
                 .WithButton("Reporter", "reporter_row", ButtonStyle.Secondary, disabled: true, row: 1)
                 .WithButton("Clear Pronouns", $"pronouns_clear:{reporter.UserId},false", ButtonStyle.Danger, row: 1)
                 .WithButton("Clear and Disable Pronouns", $"pronouns_clear:{reporter.UserId},true", ButtonStyle.Danger,
                     row: 1)
                 .WithButton("Blacklist User", $"pronouns_blacklist:{reporter.UserId}", ButtonStyle.Danger, row: 1)
                 .WithButton("DM User", $"pronouns_reportdm:{reporter.UserId}", ButtonStyle.Danger, row:1)
                 .WithButton("Context", "context_row", ButtonStyle.Secondary, disabled: true, row: 2)
                 .WithButton("Blacklist Guild", $"pronouns_blacklist_guild:{ctx.Guild.Id}", ButtonStyle.Danger, row: 2)
                 .WithButton("DM Guild Owner", $"pronouns_reportdm:{ctx.Guild.OwnerId}", ButtonStyle.Danger, row: 2);

        await (channel as ITextChannel).SendMessageAsync(embed: eb.Build(), components: cb.Build()).ConfigureAwait(false);
        await EphemeralReplyConfirmLocalizedAsync("pronouns_reported").ConfigureAwait(false);
    }

    [ComponentInteraction("pronouns_clear:*,*", true), SlashOwnerOnly]
    public async Task ClearPronouns(string sId, string sDisable) =>
        await Context.Interaction.RespondWithModalAsync<PronounsFcbModal>(
            $"pronouns_fc_action:{sId},{sDisable},false", null, x => x.WithTitle("Clear Pronouns")).ConfigureAwait(false);

    [ComponentInteraction("pronouns_blacklist:*", true), SlashOwnerOnly]
    public async Task BlacklistPronouns(string sId) =>
        await ctx.Interaction.RespondWithModalAsync<PronounsFcbModal>($"pronouns_fc_action:{sId},true,true", null,
            x => x.WithTitle("Blacklist User and Clear Pronouns")).ConfigureAwait(false);

    [ComponentInteraction("pronouns_blacklist_guild:*", true), SlashOwnerOnly]
    public async Task BlacklistGuildPronouns(string sId) =>
        await ctx.Interaction.RespondWithModalAsync<PronounsFcbModal>($"pronouns_fcb_g:{sId}", null,
            x => x.WithTitle("Blacklist Guild")).ConfigureAwait(false);

    [ModalInteraction("pronouns_fcb_g:*", true), SlashOwnerOnly]
    public async Task PronounsGuildBlacklist(string sId, PronounsFcbModal modal)
    {
        var id = ulong.Parse(sId);
        _bss.Blacklist(BlacklistType.Server, id, modal.FcbReason);
        await RespondAsync("blacklisted the server").ConfigureAwait(false);
    }

    [ModalInteraction("pronouns_fc_action:*,*,*", true), SlashOwnerOnly]
    public async Task PronounsFcAction(string sId, string sPronounsDisable, string sBlacklist, PronounsFcbModal modal)
    {
        var userId = ulong.Parse(sId);
        await using var uow = _db.GetDbContext();
        var user = await uow.DiscordUser.AsQueryable().FirstAsync(x => x.UserId == userId).ConfigureAwait(false);
        user.Pronouns = "";
        user.PronounsDisabled = bool.TryParse(sPronounsDisable, out var disable) && disable;
        user.PronounsClearedReason = modal.FcbReason;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        if (bool.TryParse(sBlacklist, out var blacklist) && blacklist)
            _bss.Blacklist(BlacklistType.User, user.UserId, modal.FcbReason);
        await RespondAsync("completed moderation actions.").ConfigureAwait(false);
    }

    private async Task<bool> PronounsDisabled(DiscordUser user)
    {
        if (!user.PronounsDisabled) return false;
        await ReplyErrorLocalizedAsync("pronouns_disabled_user", user.PronounsClearedReason).ConfigureAwait(false);
        return true;
    }

    [SlashCommand("get", "Get a user's pronouns!"), CheckPermissions]
    [UserCommand("Pronouns")]
    public async Task GetPronouns(IUser? user)
    {
        await using var uow = _db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user).ConfigureAwait(false);
        if (await PronounsDisabled(dbUser).ConfigureAwait(false)) return;
        var pronouns = await Service.GetPronounsOrUnspecifiedAsync(user.Id).ConfigureAwait(false);
        var cb = new ComponentBuilder();
        if (!pronouns.PronounDb)
            cb.WithButton(GetText("pronouns_report_button"), $"pronouns_report.{user.Id};", ButtonStyle.Danger);
        await RespondAsync(GetText(
            pronouns.PronounDb
                ? pronouns.Pronouns.Contains(' ') ? "pronouns_pndb_special" : "pronouns_pndb_get"
                : "pronouns_internal_get", user.ToString(), pronouns.Pronouns), components: cb.Build(), ephemeral: true).ConfigureAwait(false);
    }

    [SlashCommand("override", "Override your default pronouns"), CheckPermissions]
    public async Task PronounOverride(string? pronouns = null)
    {
        await using var uow = _db.GetDbContext();
        var user = await uow.GetOrCreateUser(ctx.User).ConfigureAwait(false);
        if (await PronounsDisabled(user).ConfigureAwait(false)) return;
        if (string.IsNullOrWhiteSpace(pronouns))
        {
            var cb = new ComponentBuilder()
                .WithButton(GetText("pronouns_overwrite_button"), "pronouns_overwrite");
            if (string.IsNullOrWhiteSpace(user.Pronouns))
            {
                await RespondAsync(GetText("pronouns_internal_no_override"), components: cb.Build()).ConfigureAwait(false);
                return;
            }

            cb.WithButton(GetText("pronouns_overwrite_clear_button"), "pronouns_overwrite_clear", ButtonStyle.Danger);
            await RespondAsync(GetText("pronouns_internal_self", user.Pronouns), components: cb.Build()).ConfigureAwait(false);
            return;
        }
        user.Pronouns = pronouns;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await ConfirmLocalizedAsync("pronouns_internal_update", user.Pronouns).ConfigureAwait(false);
    }

    [ComponentInteraction("pronouns_reportdm:*", true), SlashOwnerOnly]
    public async Task DmUser(string uIdStr) =>
        await ctx.Interaction.RespondWithModalAsync<DmUserModal>($"pronouns_reportdm_modal:{uIdStr}", null, x => x.WithTitle("dm user")).ConfigureAwait(false);

    [ModalInteraction("pronouns_reportdm_modal:*", true), SlashOwnerOnly]
    public async Task DmUserModal(string uIdStr, DmUserModal modal)
    {
        try
        {
            var user = await ctx.Client.GetUserAsync(ulong.Parse(uIdStr)).ConfigureAwait(false);
            var channel = await user.CreateDMChannelAsync().ConfigureAwait(false);
            if (SmartEmbed.TryParse(modal.Message, ctx.Guild.Id, out var eb, out var txt, out var cb))
                await channel.SendMessageAsync(txt, embeds: eb, components: cb.Build()).ConfigureAwait(false);
            else
                await channel.SendMessageAsync(modal.Message).ConfigureAwait(false);
            await RespondAsync($"sent a dm to <@{ulong.Parse(uIdStr)}>").ConfigureAwait(false);
        }
        catch
        {
            await RespondAsync("Failed to dm user.").ConfigureAwait(false);
        }
    }
}