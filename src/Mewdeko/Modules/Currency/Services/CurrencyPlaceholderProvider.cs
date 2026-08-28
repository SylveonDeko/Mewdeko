using Mewdeko.Common.TriggerPlaceholders;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Supplies currency placeholders to chat trigger responses.
/// </summary>
public sealed class CurrencyPlaceholderProvider : INService, ITriggerPlaceholderProvider
{
    private readonly ICurrencyService currency;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CurrencyPlaceholderProvider" /> class.
    /// </summary>
    /// <param name="currency">The currency service the placeholders read from.</param>
    public CurrencyPlaceholderProvider(ICurrencyService currency)
    {
        this.currency = currency;
    }

    /// <inheritdoc />
    public IEnumerable<(string Name, Func<TriggerPlaceholderContext, Task<string?>> Func)> GetPlaceholders()
    {
        yield return ("%currency.balance%", ctx => Balance(ctx, false, x => x.Wallet));
        yield return ("%currency.bank%", ctx => Balance(ctx, false, x => x.Bank));
        yield return ("%currency.total%", ctx => Balance(ctx, false, x => x.Wallet + x.Bank));
        yield return ("%targetuser.currency.balance%", ctx => Balance(ctx, true, x => x.Wallet));
        yield return ("%targetuser.currency.bank%", ctx => Balance(ctx, true, x => x.Bank));

        yield return ("%currency.emote%", async ctx =>
            await currency.GetCurrencyEmote(ctx.Guild?.Id).ConfigureAwait(false));
    }

    private async Task<string?> Balance(TriggerPlaceholderContext ctx, bool target,
        Func<(long Wallet, long Bank), long> select)
    {
        var user = target ? ctx.Target : ctx.User;
        if (user is null)
            return "";

        var balances = await currency.GetBalancesAsync(user.Id, ctx.Guild?.Id).ConfigureAwait(false);
        return select(balances).ToString("N0");
    }
}