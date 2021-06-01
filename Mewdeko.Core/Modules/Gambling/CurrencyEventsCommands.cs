using Discord;
using Discord.Commands;
using Mewdeko.Extensions;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Gambling.Services;
using Mewdeko.Core.Common;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Core.Modules.Gambling.Common.Events;
using System;
using Mewdeko.Core.Modules.Gambling.Common;
using Mewdeko.Core.Modules.Gambling.Services;

namespace Mewdeko.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class CurrencyEventsCommands : GamblingSubmodule<CurrencyEventsService>
        {
            public enum OtherEvent
            {
                BotListUpvoters
            }

            public CurrencyEventsCommands(GamblingConfigService gamblingConf) : base(gamblingConf)
            {
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [MewdekoOptionsAttribute(typeof(EventOptions))]
            [OwnerOnly]
            public async Task EventStart(CurrencyEvent.Type ev, params string[] options)
            {
                var (opts, _) = OptionsParser.ParseFrom(new EventOptions(), options);
                if (!await _service.TryCreateEventAsync(ctx.Guild.Id,
                    ctx.Channel.Id,
                    ev,
                    opts,
                    GetEmbed
                    ).ConfigureAwait(false))
                {
                    await ReplyErrorLocalizedAsync("start_event_fail").ConfigureAwait(false);
                    return;
                }
            }

            private EmbedBuilder GetEmbed(CurrencyEvent.Type type, EventOptions opts, long currentPot)
            {
                switch (type)
                {
                    case CurrencyEvent.Type.Reaction:
                        return new EmbedBuilder()
                            .WithOkColor()
                            .WithTitle(GetText("event_title", type.ToString()))
                            .WithDescription(GetReactionDescription(opts.Amount, currentPot))
                            .WithFooter(GetText("event_duration_footer", opts.Hours));
                    case CurrencyEvent.Type.GameStatus:
                        return new EmbedBuilder()
                            .WithOkColor()
                            .WithTitle(GetText("event_title", type.ToString()))
                            .WithDescription(GetGameStatusDescription(opts.Amount, currentPot))
                            .WithFooter(GetText("event_duration_footer", opts.Hours));
                    default:
                        break;
                }
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            private string GetReactionDescription(long amount, long potSize)
            {
                string potSizeStr = Format.Bold(potSize == 0
                    ? "∞" + CurrencySign
                    : potSize.ToString() + CurrencySign);
                return GetText("new_reaction_event",
                                   CurrencySign,
                                   Format.Bold(amount + CurrencySign),
                                   potSizeStr);
            }

            private string GetGameStatusDescription(long amount, long potSize)
            {
                string potSizeStr = Format.Bold(potSize == 0
                    ? "∞" + CurrencySign
                    : potSize.ToString() + CurrencySign);
                return GetText("new_gamestatus_event",
                                   CurrencySign,
                                   Format.Bold(amount + CurrencySign),
                                   potSizeStr);
            }
        }
    }
}
