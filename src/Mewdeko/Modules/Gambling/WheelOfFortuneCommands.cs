using System.Collections.Immutable;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Modules.Gambling.Services;

namespace Mewdeko.Modules.Gambling;

public partial class Gambling
{
    public class WheelOfFortuneCommands : GamblingSubmodule<GamblingService>
    {
        private static readonly ImmutableArray<string> Emojis = new[]
        {
            "⬆", "↖", "⬅", "↙", "⬇", "↘", "➡", "↗"
        }.ToImmutableArray();

        private readonly ICurrencyService cs;

        public WheelOfFortuneCommands(ICurrencyService cs, GamblingConfigService gamblingConfService)
            : base(gamblingConfService) =>
            this.cs = cs;

        [Cmd, Aliases]
        public async Task WheelOfFortune(ShmartNumber amount)
        {
            if (!await CheckBetMandatory(amount).ConfigureAwait(false))
                return;

            if (!await cs.RemoveAsync(ctx.User.Id, "Wheel Of Fortune - bet", amount, true).ConfigureAwait(false))
            {
                await ReplyErrorLocalizedAsync("not_enough", CurrencySign).ConfigureAwait(false);
                return;
            }

            var result = await Service.WheelOfFortuneSpinAsync(ctx.User.Id, amount).ConfigureAwait(false);

            var wofMultipliers = Config.WheelOfFortune.Multipliers;
            await ctx.Channel.SendConfirmAsync(
                Format.Bold($@"{ctx.User} won: {result.Amount + CurrencySign}

   『{wofMultipliers[1]}』   『{wofMultipliers[0]}』   『{wofMultipliers[7]}』

『{wofMultipliers[2]}』      {Emojis[result.Index]}      『{wofMultipliers[6]}』

     『{wofMultipliers[3]}』   『{wofMultipliers[4]}』   『{wofMultipliers[5]}』")).ConfigureAwait(false);
        }
    }
}