using Discord;
using NadekoBot.Core.Services;
using NadekoBot.Modules;
using System.Threading.Tasks;

namespace NadekoBot.Core.Modules.Gambling.Common
{
    public abstract class GamblingTopLevelModule<TService> : NadekoTopLevelModule<TService> where TService : INService
    {
        protected GamblingTopLevelModule(bool isTopLevel = true) : base(isTopLevel)
        {
        }

        private async Task<bool> InternalCheckBet(long amount)
        {
            if (amount < 1)
            {
                return false;
            }
            if (amount < Bc.BotConfig.MinBet)
            {
                await ReplyErrorLocalizedAsync("min_bet_limit", Format.Bold(Bc.BotConfig.MinBet.ToString()) + Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                return false;
            }
            if (Bc.BotConfig.MaxBet > 0 && amount > Bc.BotConfig.MaxBet)
            {
                await ReplyErrorLocalizedAsync("max_bet_limit", Format.Bold(Bc.BotConfig.MaxBet.ToString()) + Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                return false;
            }
            return true;
        }

        protected Task<bool> CheckBetMandatory(long amount)
        {
            if (amount < 1)
            {
                return Task.FromResult(false);
            }
            return InternalCheckBet(amount);
        }

        protected Task<bool> CheckBetOptional(long amount)
        {
            if (amount == 0)
            {
                return Task.FromResult(true);
            }
            return InternalCheckBet(amount);
        }
    }

    public abstract class GamblingSubmodule<TService> : GamblingTopLevelModule<TService> where TService : INService
    {
        protected GamblingSubmodule() : base(false)
        {
        }
    }
}
