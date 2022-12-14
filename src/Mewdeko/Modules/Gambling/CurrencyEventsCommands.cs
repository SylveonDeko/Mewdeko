using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Modules.Gambling.Common.Events;
using Mewdeko.Modules.Gambling.Services;

namespace Mewdeko.Modules.Gambling;

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

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         MewdekoOptions(typeof(EventOptions)), OwnerOnly]
        public async Task EventStart(CurrencyEvent.Type ev, params string[] options)
        {
            var (opts, _) = OptionsParser.ParseFrom(new EventOptions(), options);
            if (!await Service.TryCreateEventAsync(ctx.Guild.Id,
                    ctx.Channel.Id,
                    ev,
                    opts,
                    GetEmbed
                ).ConfigureAwait(false))
            {
                await ReplyErrorLocalizedAsync("start_event_fail").ConfigureAwait(false);
            }
        }

        private EmbedBuilder GetEmbed(CurrencyEvent.Type type, EventOptions opts, long currentPot) =>
            type switch
            {
                CurrencyEvent.Type.Reaction => new EmbedBuilder().WithOkColor()
                    .WithTitle(GetText("event_title", type.ToString()))
                    .WithDescription(GetReactionDescription(opts.Amount,
                        currentPot))
                    .WithFooter(GetText("event_duration_footer",
                        opts.Hours)),
                CurrencyEvent.Type.GameStatus => new EmbedBuilder().WithOkColor()
                    .WithTitle(GetText("event_title", type.ToString()))
                    .WithDescription(
                        GetGameStatusDescription(opts.Amount,
                            currentPot))
                    .WithFooter(GetText("event_duration_footer",
                        opts.Hours)),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };

        private string? GetReactionDescription(long amount, long potSize)
        {
            var potSizeStr = Format.Bold(potSize == 0
                ? $"∞{CurrencySign}"
                : potSize + CurrencySign);
            return GetText("new_reaction_event",
                CurrencySign,
                Format.Bold(amount + CurrencySign),
                potSizeStr);
        }

        private string? GetGameStatusDescription(long amount, long potSize)
        {
            var potSizeStr = Format.Bold(potSize == 0
                ? $"∞{CurrencySign}"
                : potSize + CurrencySign);
            return GetText("new_gamestatus_event",
                CurrencySign,
                Format.Bold(amount + CurrencySign),
                potSizeStr);
        }
    }
}