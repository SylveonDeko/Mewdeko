using System.Text;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    /// <summary>
    ///     Commands for the daily dice game.
    /// </summary>
    [Group]
    public class DailyDiceCommands : MewdekoSubmodule<DailyDiceService>
    {
        /// <summary>
        ///     Rolls your one die for the day.
        /// </summary>
        /// <example>.dailydice</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task DailyDice()
        {
            var (rolled, roll) = await Service.RollAsync(ctx.Guild.Id, ctx.User.Id);
            await (rolled
                ? ReplyConfirmAsync(Strings.DiceDailyRolled(ctx.Guild.Id, ctx.User.Mention, roll,
                    DailyDiceService.Sides))
                : ReplyErrorAsync(Strings.DiceDailyAlready(ctx.Guild.Id, roll)));
        }

        /// <summary>
        ///     Shows the best rolls of the month.
        /// </summary>
        /// <param name="lastMonth">Shows last month instead.</param>
        /// <example>.dailydiceboard</example>
        /// <example>.dailydiceboard true</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task DailyDiceBoard(bool lastMonth = false)
        {
            var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            if (lastMonth)
                month = month.AddMonths(-1);

            var standings = await Service.GetStandingsAsync(ctx.Guild.Id, month);
            if (standings.Count == 0)
            {
                await ReplyErrorAsync(Strings.DiceBoardEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var text = new StringBuilder();
            for (var i = 0; i < standings.Count; i++)
            {
                var row = standings[i];
                text.AppendLine(Strings.DiceBoardRow(ctx.Guild.Id, i + 1, row.UserId, row.BestRoll, row.Rolls));
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.DiceBoardTitle(ctx.Guild.Id, month.ToString("MMMM yyyy")))
                .WithDescription(text.ToString())).ConfigureAwait(false);
        }
    }
}
