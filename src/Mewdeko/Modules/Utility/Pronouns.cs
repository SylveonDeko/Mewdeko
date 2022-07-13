using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    public class Pronouns : MewdekoSubmodule<PronounsService>
    {
        private readonly DbService _db;
        public Pronouns(DbService db) => _db = db;

        [Cmd, Aliases]
        public async Task GetPronouns(IUser? user = null)
        {
            user ??= ctx.User;
            var uow = _db.GetDbContext();
            await using var _ = uow.ConfigureAwait(false);
            var dbUser = await uow.GetOrCreateUser(user).ConfigureAwait(false);
            if (await PronounsDisabled(dbUser).ConfigureAwait(false)) return;
            var pronouns = await Service.GetPronounsOrUnspecifiedAsync(user.Id).ConfigureAwait(false);
            var cb = new ComponentBuilder();
            if (!pronouns.PronounDb)
                cb.WithButton(GetText("pronouns_report_button"), $"pronouns_report.{user.Id};", ButtonStyle.Danger);
            await ctx.Channel.SendConfirmAsync(
                GetText(
                    pronouns.PronounDb
                        ? pronouns.Pronouns.Contains(' ') ? "pronouns_pndb_special" : "pronouns_pndb_get"
                        : "pronouns_internal_get", user.ToString(), pronouns.Pronouns), cb).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task PronounOverride([Remainder] string? pronouns = null)
        {
            var uow = _db.GetDbContext();
            await using var _ = uow.ConfigureAwait(false);
            var user = await uow.GetOrCreateUser(ctx.User).ConfigureAwait(false);
            if (await PronounsDisabled(user).ConfigureAwait(false)) return;
            if (string.IsNullOrWhiteSpace(pronouns))
            {
                var cb = new ComponentBuilder().WithButton(GetText("pronouns_overwrite_button"), "pronouns_overwrite");
                if (string.IsNullOrWhiteSpace(user.Pronouns))
                {
                    await ctx.Channel.SendConfirmAsync(GetText("pronouns_internal_no_override"), cb)
                             .ConfigureAwait(false);
                    return;
                }

                cb.WithButton(GetText("pronouns_overwrite_clear_button"), "pronouns_overwrite_clear",
                    ButtonStyle.Danger);
                await ctx.Channel.SendConfirmAsync(GetText("pronouns_internal_self", user.Pronouns), cb)
                         .ConfigureAwait(false);
                return;
            }

            user.Pronouns = pronouns;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await ConfirmLocalizedAsync("pronouns_internal_update", user.Pronouns).ConfigureAwait(false);
        }

        private async Task<bool> PronounsDisabled(DiscordUser user)
        {
            if (!user.PronounsDisabled) return false;
            await ReplyErrorLocalizedAsync("pronouns_disabled_user", user.PronounsClearedReason).ConfigureAwait(false);
            return true;
        }


        [Cmd, Aliases, OwnerOnly]
        public async Task PronounOverrideForceClear(IUser? user, bool pronounsDisabledAbuse, [Remainder] string reason)
        {
            var uow = _db.GetDbContext();
            await using var _ = uow.ConfigureAwait(false);
            var dbUser = await uow.GetOrCreateUser(user).ConfigureAwait(false);
            dbUser.PronounsDisabled = pronounsDisabledAbuse;
            dbUser.PronounsClearedReason = reason;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await ConfirmLocalizedAsync(pronounsDisabledAbuse ? "pronouns_disabled_user" : "pronouns_cleared").ConfigureAwait(false);
        }
        [Cmd, Aliases, OwnerOnly]
        public async Task PronounOverrideForceClear(ulong user, bool pronounsDisabledAbuse, [Remainder] string reason)
        {
            var uow = _db.GetDbContext();
            await using var _ = uow.ConfigureAwait(false);
            var dbUser = await uow.DiscordUser.AsQueryable().FirstAsync(x => x.UserId == user).ConfigureAwait(false);
            dbUser.PronounsDisabled = pronounsDisabledAbuse;
            dbUser.PronounsClearedReason = reason;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await ConfirmLocalizedAsync(pronounsDisabledAbuse ? "pronouns_disabled_user" : "pronouns_cleared").ConfigureAwait(false);
        }
    }
}