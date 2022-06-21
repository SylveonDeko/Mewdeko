using Discord.Interactions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mewdeko.Common.TypeReaders;

public class TimeSpanConverter : TypeConverter<TimeSpan>
{
    private readonly Dictionary<string, Func<string, TimeSpan>> _callback = new();

    private readonly Regex _regex = new(@"(\d*)\s*([a-zA-Z]*)\s*(?:and|,)?\s*", RegexOptions.Compiled);

    public TimeSpanConverter()
    {
        _callback["second"] = Seconds;
        _callback["seconds"] = Seconds;
        _callback["sec"] = Seconds;
        _callback["s"] = Seconds;
        _callback["minute"] = Minutes;
        _callback["minutes"] = Minutes;
        _callback["min"] = Minutes;
        _callback["m"] = Minutes;
        _callback["hour"] = Hours;
        _callback["hours"] = Hours;
        _callback["h"] = Hours;
        _callback["day"] = Days;
        _callback["days"] = Days;
        _callback["d"] = Days;
        _callback["week"] = Weeks;
        _callback["weeks"] = Weeks;
        _callback["w"] = Weeks;
        _callback["month"] = Months;
        _callback["months"] = Months;
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
            var matches = _regex.Matches(@string ?? string.Empty);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                    if (_callback.TryGetValue(match.Groups[2].Value, out var result))
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