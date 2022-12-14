using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    [Group]
    public class UnitConverterCommands : MewdekoSubmodule<ConverterService>
    {
        [Cmd, Aliases]
        public async Task ConvertList()
        {
            var units = Service.Units;
            var res = units.GroupBy(x => x.UnitType)
                .Aggregate(new EmbedBuilder().WithTitle(GetText("convertlist"))
                        .WithOkColor(),
                    (embed, g) => embed.AddField(efb =>
                        efb.WithName(g.Key.ToTitleCase())
                            .WithValue(string.Join(", ", g.Select(x => x.Triggers.FirstOrDefault())
                                .OrderBy(x => x)))));
            await ctx.Channel.EmbedAsync(res).ConfigureAwait(false);
        }

        [Cmd, Aliases, Priority(0)]
        public async Task Convert(string origin, string target, decimal value)
        {
            var originUnit = Array.Find(Service.Units, x =>
                x.Triggers.Select(y => y.ToUpperInvariant()).Contains(origin.ToUpperInvariant()));
            var targetUnit = Array.Find(Service.Units, x =>
                x.Triggers.Select(y => y.ToUpperInvariant()).Contains(target.ToUpperInvariant()));
            if (originUnit == null || targetUnit == null)
            {
                await ReplyErrorLocalizedAsync("convert_not_found", Format.Bold(origin), Format.Bold(target))
                    .ConfigureAwait(false);
                return;
            }

            if (originUnit.UnitType != targetUnit.UnitType)
            {
                await ReplyErrorLocalizedAsync("convert_type_error", Format.Bold(originUnit.Triggers.First()),
                    Format.Bold(targetUnit.Triggers.First())).ConfigureAwait(false);
                return;
            }

            decimal res = 0;
            if (originUnit.Triggers == targetUnit.Triggers)
            {
                res = value;
            }
            else if (originUnit.UnitType == "temperature")
            {
                //from Kelvin to target
                res = targetUnit.Triggers.First().ToUpperInvariant() switch
                {
                    "C" => res - 273.15m //celcius!
                    ,
                    "F" => (res * (9m / 5m)) - 459.67m,
                    //don't really care too much about efficiency, so just convert to Kelvin, then to target
                    _ => originUnit.Triggers.First().ToUpperInvariant() switch
                    {
                        "C" => value + 273.15m //celcius!
                        ,
                        "F" => (value + 459.67m) * (5m / 9m),
                        _ => value
                    }
                };
            }
            else
            {
                if (originUnit.UnitType == "currency")
                    res = value * targetUnit.Modifier / originUnit.Modifier;
                else
                    res = value * originUnit.Modifier / targetUnit.Modifier;
            }

            res = Math.Round(res, 4);

            await ctx.Channel
                .SendConfirmAsync(GetText("convert", value, originUnit.Triggers.Last(), res,
                    targetUnit.Triggers.Last())).ConfigureAwait(false);
        }
    }
}