using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Interactions;

namespace Mewdeko.Common.TypeReaders;

public class TimeSpanConverter : TypeConverter<TimeSpan>
{
    private readonly Dictionary<string, Func<string, TimeSpan>> callback = new();

    private readonly Regex regex = new(@"(\d*)\s*([a-zA-Z]*)\s*(?:and|,)?\s*", RegexOptions.Compiled);

    public TimeSpanConverter()
    {
        callback["second"] = Seconds;
        callback["seconds"] = Seconds;
        callback["sec"] = Seconds;
        callback["s"] = Seconds;
        callback["minute"] = Minutes;
        callback["minutes"] = Minutes;
        callback["min"] = Minutes;
        callback["m"] = Minutes;
        callback["hour"] = Hours;
        callback["hours"] = Hours;
        callback["h"] = Hours;
        callback["day"] = Days;
        callback["days"] = Days;
        callback["d"] = Days;
        callback["week"] = Weeks;
        callback["weeks"] = Weeks;
        callback["w"] = Weeks;
        callback["month"] = Months;
        callback["months"] = Months;
    }

    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    public override Task<TypeConverterResult> ReadAsync(
        IInteractionContext context,
        IApplicationCommandInteractionDataOption option,
        IServiceProvider services)
    {
        var @string = option.Value as string;
        if (!TimeSpan.TryParse(@string, out var span))
        {
            @string = @string?.ToLower().Trim();
            var matches = regex.Matches(@string ?? string.Empty);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                    if (callback.TryGetValue(match.Groups[2].Value, out var result))
                        span += result(match.Groups[1].Value);
            }
        }

        return Task.FromResult(TypeConverterResult.FromSuccess(span));
    }

    private TimeSpan Seconds(string match) => new(0, 0, int.Parse(match));

    private TimeSpan Minutes(string match) => new(0, int.Parse(match), 0);

    private TimeSpan Hours(string match) => new(int.Parse(match), 0, 0);

    private TimeSpan Days(string match) => new(int.Parse(match), 0, 0, 0);

    private TimeSpan Weeks(string match) => new(int.Parse(match) * 7, 0, 0, 0);

    private TimeSpan Months(string match) => new(int.Parse(match) * 30, 0, 0, 0);
}