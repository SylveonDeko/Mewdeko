using Discord;
using Discord.Interactions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Modals;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Utility.Services;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Utility;

[Group("pronouns", "pronouns")]
public class SlashPronoun : MewdekoSlashSubmodule<PronounsService>
{
    private readonly DbService _db;
    private readonly Mewdeko _bot;
    public SlashPronoun(DbService db, Mewdeko bot)
    {
        _db = db;
        _bot = bot;
    }

    [ComponentInteraction("pronouns_overwrite", true), BlacklistCheck]
    public async Task OverwritePronouns() => await RespondWithModalAsync<PronounsModal>("pronouns_overwrite_modal");

    [ComponentInteraction("pronouns_overwrite_clear", true), BlacklistCheck]
    public async Task ClearPronounsOverwrite()
    {
        await using var uow = _db.GetDbContext();
        var user = uow.GetOrCreateUser(ctx.User);
        if (await PronounsDisabled(user).ConfigureAwait(false)) return;
        user.Pronouns = "";
        await uow.SaveChangesAsync();
        await ConfirmLocalizedAsync("pronouns_cleared_self");
    }
    

    [ModalInteraction("pronouns_overwrite_modal", true), BlacklistCheck]
    public async Task PronounsOverwriteModal(PronounsModal modal)
    {
        await using var uow = _db.GetDbContext();
        var user = uow.GetOrCreateUser(ctx.User);
        if (await PronounsDisabled(user).ConfigureAwait(false)) return;
        user.Pronouns = modal.Pronouns;
        await uow.SaveChangesAsync();
        await ConfirmLocalizedAsync("pronouns_internal_update", user.Pronouns);
    }

    [ComponentInteraction("pronouns_report.*;", true), BlacklistCheck]
    public async Task ReportPronouns(string sId)
    {
        await using var uow = _db.GetDbContext();
        var reporter = uow.GetOrCreateUser(ctx.User);
        
        if (await PronounsDisabled(reporter).ConfigureAwait(false)) return;
        
        var id = ulong.Parse(sId);
        var user = await uow.DiscordUser.FirstOrDefaultAsync(x => x.UserId == id).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(user?.Pronouns)) return;

        var channel = await ctx.Client.GetChannelAsync(_bot.Credentials.PronounAbuseReportChannelId)
                               .ConfigureAwait(false);
        var eb = new EmbedBuilder().WithAuthor(ctx.User).WithTitle("Pronoun abuse report")
                                   .AddField("Reported User", $"{user.Username} ({user.UserId}, <@{user.UserId}>)")
                                   .AddField("Pronouns Cleared Reason",
                                       string.IsNullOrWhiteSpace(user.PronounsClearedReason)
                                           ? "Never Cleared"
                                           : user.PronounsClearedReason)
                                   .AddField("Pronouns", user.Pronouns)
                                   .WithErrorColor();
        await (channel as ITextChannel).SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        await EphemeralReplyConfirmLocalizedAsync("pronouns_reported");
    }

    private async Task<bool> PronounsDisabled(DiscordUser user)
    {
        if (!user.PronounsDisabled) return false;
        await ReplyErrorLocalizedAsync("pronouns_disabled_user", user.PronounsClearedReason).ConfigureAwait(false);
        return true;
    }
    
    [SlashCommand("get", "Get a user's pronouns!"), BlacklistCheck, CheckPermissions]
    [UserCommand("Pronouns")]
    public async Task GetPronouns(IUser? user)
    {
        await using var uow = _db.GetDbContext();
        var dbUser = uow.GetOrCreateUser(user);
        if (await PronounsDisabled(dbUser).ConfigureAwait(false)) return;
        var pronouns = await Service.GetPronounsOrUnspecifiedAsync(user.Id);
        var cb = new ComponentBuilder();
        if (!pronouns.PronounDb)
            cb.WithButton(GetText("pronouns_report_button"), $"pronouns_report.{user.Id};", ButtonStyle.Danger);
        await RespondAsync(GetText(
            pronouns.PronounDb
                ? pronouns.Pronouns.Contains(' ') ? "pronouns_pndb_special" : "pronouns_pndb_get"
                : "pronouns_internal_get", user.ToString(), pronouns.Pronouns), components: cb.Build(), ephemeral:true);
    }
    
    [SlashCommand("override", "Override your default pronouns"), BlacklistCheck, CheckPermissions]
    public async Task PronounOverride(string? pronouns = null)
    {
        await using var uow = _db.GetDbContext();
        var user = uow.GetOrCreateUser(ctx.User);
        if (await PronounsDisabled(user).ConfigureAwait(false)) return;
        if (string.IsNullOrWhiteSpace(pronouns))
        {
            var cb = new ComponentBuilder()
                .WithButton(GetText("pronouns_overwrite_button"), "pronouns_overwrite");
            if (string.IsNullOrWhiteSpace(user.Pronouns))
            {
                await RespondAsync(GetText("pronouns_internal_no_override"), components:cb.Build()).ConfigureAwait(false);
                return;
            }

            cb.WithButton(GetText("pronouns_overwrite_clear_button"), "pronouns_overwrite_clear", ButtonStyle.Danger);
            await RespondAsync(GetText("pronouns_internal_self", user.Pronouns), components:cb.Build()).ConfigureAwait(false);
            return;
        }
        user.Pronouns = pronouns;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await ConfirmLocalizedAsync("pronouns_internal_update", user.Pronouns).ConfigureAwait(false);
    }
}