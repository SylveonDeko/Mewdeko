using System;
using System.Threading.Tasks;
using Discord;
using Mewdeko.Common;
using Mewdeko.Modules.Gambling.Services;

namespace Mewdeko.Modules.Gambling.Common
{
    public abstract class GamblingModule<TService> : MewdekoModule<TService>
    {
        private readonly Lazy<GamblingConfig> _lazyConfig;

        protected GamblingModule(GamblingConfigService gambService)
        {
            _lazyConfig = new Lazy<GamblingConfig>(() => gambService.Data);
        }

        protected GamblingConfig _config => _lazyConfig.Value;
        protected string CurrencySign => _config.Currency.Sign;
        protected string CurrencyName => _config.Currency.Name;

        private async Task<bool> InternalCheckBet(long amount)
        {
            if (amount < 1) return false;
            if (amount < _config.MinBet)
            {
                await ReplyErrorLocalizedAsync("min_bet_limit",
                    Format.Bold(_config.MinBet.ToString()) + CurrencySign).ConfigureAwait(false);
                return false;
            }

            if (_config.MaxBet > 0 && amount > _config.MaxBet)
            {
                await ReplyErrorLocalizedAsync("max_bet_limit",
                    Format.Bold(_config.MaxBet.ToString()) + CurrencySign).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        protected Task<bool> CheckBetMandatory(long amount)
        {
            if (amount < 1) return Task.FromResult(false);
            return InternalCheckBet(amount);
        }

        protected Task<bool> CheckBetOptional(long amount)
        {
            if (amount == 0) return Task.FromResult(true);
            return InternalCheckBet(amount);
        }
    }

    public abstract class GamblingSubmodule<TService> : GamblingModule<TService>
    {
        protected GamblingSubmodule(GamblingConfigService gamblingConfService) : base(gamblingConfService)
        {
        }
    }
}