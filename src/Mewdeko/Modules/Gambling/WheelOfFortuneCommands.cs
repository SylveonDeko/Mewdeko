using System.Collections.Immutable;
using System.Threading.Tasks;
using Discord;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Modules.Gambling.Services;
using Mewdeko.Services;

namespace Mewdeko.Modules.Gambling;

public partial class Gambling
{
    public class WheelOfFortuneCommands : GamblingSubmodule<GamblingService>
    {
        private static readonly ImmutableArray<string> _emojis = new[]
        {
            "⬆",
            "↖",
            "⬅",
            "↙",
            "⬇",
            "↘",
            "➡",
            "↗"
        }.ToImmutableArray();

        private readonly ICurrencyService _cs;
        private readonly DbService _db;

        public WheelOfFortuneCommands(ICurrencyService cs, DbService db, GamblingConfigService gamblingConfService)
            : base(gamblingConfService)
        {
            _cs = cs;
            _db = db;
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task WheelOfFortune(ShmartNumber amount)
        {
            if (!await CheckBetMandatory(amount).ConfigureAwait(false))
                return;

            if (!await _cs.RemoveAsync(ctx.User.Id, "Wheel Of Fortune - bet", amount, true).ConfigureAwait(false))
            {
                await ReplyErrorLocalizedAsync("not_enough", CurrencySign).ConfigureAwait(false);
                return;
            }

            var result = await Service.WheelOfFortuneSpinAsync(ctx.User.Id, amount).ConfigureAwait(false);

            var wofMultipliers = _config.WheelOfFortune.Multipliers;
            await ctx.Channel.SendConfirmAsync(
                Format.Bold($@"{ctx.User} won: {result.Amount + CurrencySign}

   『{wofMultipliers[1]}』   『{wofMultipliers[0]}』   『{wofMultipliers[7]}』

『{wofMultipliers[2]}』      {_emojis[result.Index]}      『{wofMultipliers[6]}』

     『{wofMultipliers[3]}』   『{wofMultipliers[4]}』   『{wofMultipliers[5]}』")).ConfigureAwait(false);
        }
    }
}