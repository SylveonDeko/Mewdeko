using NadekoBot.Common.Attributes;
using NadekoBot.Core.Modules.Gambling.Services;
using System.Threading.Tasks;
using Discord;
using NadekoBot.Extensions;
using System.Linq;
using Discord.Commands;
using NadekoBot.Core.Modules.Gambling.Common;
using NadekoBot.Core.Common;

namespace NadekoBot.Modules.Gambling
{
    public partial class Gambling
    {
        public class CurrencyRaffleCommands : GamblingSubmodule<CurrencyRaffleService>
        {
            public enum Mixed { Mixed }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(0)]
            public Task RaffleCur(Mixed _, ShmartNumber amount) =>
                RaffleCur(amount, true);

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(1)]
            public async Task RaffleCur(ShmartNumber amount, bool mixed = false)
            {
                if (!await CheckBetMandatory(amount).ConfigureAwait(false))
                    return;
                async Task OnEnded(IUser arg, long won)
                {
                    await ctx.Channel.SendConfirmAsync(GetText("rafflecur_ended", Bc.BotConfig.CurrencyName, Format.Bold(arg.ToString()), won + Bc.BotConfig.CurrencySign)).ConfigureAwait(false);
                }
                var res = await _service.JoinOrCreateGame(ctx.Channel.Id,
                    ctx.User, amount, mixed, OnEnded)
                        .ConfigureAwait(false);

                if (res.Item1 != null)
                {
                    await ctx.Channel.SendConfirmAsync(GetText("rafflecur", res.Item1.GameType.ToString()),
                        string.Join("\n", res.Item1.Users.Select(x => $"{x.DiscordUser} ({x.Amount})")),
                        footer: GetText("rafflecur_joined", ctx.User.ToString())).ConfigureAwait(false);
                }
                else
                {
                    if (res.Item2 == CurrencyRaffleService.JoinErrorType.AlreadyJoinedOrInvalidAmount)
                        await ReplyErrorLocalizedAsync("rafflecur_already_joined").ConfigureAwait(false);
                    else if (res.Item2 == CurrencyRaffleService.JoinErrorType.NotEnoughCurrency)
                        await ReplyErrorLocalizedAsync("not_enough", Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                }
            }
        }
    }
}
