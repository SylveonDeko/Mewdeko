using Mewdeko.Modules.Gambling.Services;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Gambling.Common;

public abstract class GamblingModuleBase<TService> : MewdekoModuleBase<TService>
{
    private readonly Lazy<GamblingConfig> _lazyConfig;

    protected GamblingModuleBase(GamblingConfigService gambService) => _lazyConfig = new Lazy<GamblingConfig>(() => gambService.Data);

    protected GamblingConfig Config => _lazyConfig.Value;
    protected string? CurrencySign => Config.Currency.Sign;
    protected string? CurrencyName => Config.Currency.Name;

    private async Task<bool> InternalCheckBet(long amount)
    {
        if (amount < 1) return false;
        if (amount < Config.MinBet)
        {
            await ReplyErrorLocalizedAsync("min_bet_limit",
                Format.Bold(Config.MinBet.ToString()) + CurrencySign).ConfigureAwait(false);
            return false;
        }

        if (Config.MaxBet > 0 && amount > Config.MaxBet)
        {
            await ReplyErrorLocalizedAsync("max_bet_limit",
                Format.Bold(Config.MaxBet.ToString()) + CurrencySign).ConfigureAwait(false);
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

public abstract class GamblingSubmodule<TService> : GamblingModuleBase<TService>
{
    protected GamblingSubmodule(GamblingConfigService gamblingConfService) : base(gamblingConfService)
    {
    }
}