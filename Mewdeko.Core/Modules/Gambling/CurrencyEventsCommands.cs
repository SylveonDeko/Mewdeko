using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common;
using Mewdeko.Core.Modules.Gambling.Common.Events;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Gambling.Services;

namespace Mewdeko.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class CurrencyEventsCommands : MewdekoSubmodule<CurrencyEventsService>
        {
            public enum OtherEvent
            {
                BotListUpvoters
            }

            private readonly DiscordSocketClient _client;
            private readonly IBotCredentials _creds;
            private readonly ICurrencyService _cs;

            public CurrencyEventsCommands(DiscordSocketClient client, ICurrencyService cs, IBotCredentials creds)
            {
                _client = client;
                _creds = creds;
                _cs = cs;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
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
                    await ReplyErrorLocalizedAsync("start_event_fail").ConfigureAwait(false);
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
                }

                throw new ArgumentOutOfRangeException(nameof(type));
            }

            private string GetReactionDescription(long amount, long potSize)
            {
                var potSizeStr = Format.Bold(potSize == 0
                    ? "∞" + Bc.BotConfig.CurrencySign
                    : potSize + Bc.BotConfig.CurrencySign);
                return GetText("new_reaction_event",
                    Bc.BotConfig.CurrencySign,
                    Format.Bold(amount + Bc.BotConfig.CurrencySign),
                    potSizeStr);
            }

            private string GetGameStatusDescription(long amount, long potSize)
            {
                var potSizeStr = Format.Bold(potSize == 0
                    ? "∞" + Bc.BotConfig.CurrencySign
                    : potSize + Bc.BotConfig.CurrencySign);
                return GetText("new_gamestatus_event",
                    Bc.BotConfig.CurrencySign,
                    Format.Bold(amount + Bc.BotConfig.CurrencySign),
                    potSizeStr);
            }
        }
    }
}