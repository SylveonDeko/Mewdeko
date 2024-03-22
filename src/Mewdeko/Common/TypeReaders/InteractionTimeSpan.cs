using System.Text.RegularExpressions;
using Discord.Interactions;

namespace Mewdeko.Common.TypeReaders
{
    /// <summary>
    /// Type converter for parsing strings into TimeSpan objects.
    /// </summary>
    public class TimeSpanConverter : TypeConverter<TimeSpan>
    {
        // Dictionary to map string representations of time units to conversion methods
        private readonly Dictionary<string, Func<string, TimeSpan>> callback = new();

        // Regular expression to match time unit representations in input strings
        private readonly Regex regex = new(@"(\d*)\s*([a-zA-Z]*)\s*(?:and|,)?\s*", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeSpanConverter"/> class.
        /// </summary>
        public TimeSpanConverter()
        {
            // Assigns conversion methods to corresponding string representations of time units
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

        /// <inheritdoc />
        public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

        /// <inheritdoc />
        public override Task<TypeConverterResult> ReadAsync(
            IInteractionContext context,
            IApplicationCommandInteractionDataOption option,
            IServiceProvider services)
        {
            var inputString = option.Value as string;
            if (!TimeSpan.TryParse(inputString, out var span))
            {
                inputString = inputString?.ToLower().Trim();
                var matches = regex.Matches(inputString ?? string.Empty);
                if (matches.Count > 0)
                {
                    // Parses the string representation of time units and constructs a TimeSpan
                    foreach (Match match in matches)
                        if (callback.TryGetValue(match.Groups[2].Value, out var result))
                            span += result(match.Groups[1].Value);
                }
            }

            return Task.FromResult(TypeConverterResult.FromSuccess(span));
        }

        // Methods for converting string representations of time units into TimeSpan components

        private TimeSpan Seconds(string match) => new(0, 0, int.Parse(match));

        private TimeSpan Minutes(string match) => new(0, int.Parse(match), 0);

        private TimeSpan Hours(string match) => new(int.Parse(match), 0, 0);

        private TimeSpan Days(string match) => new(int.Parse(match), 0, 0, 0);

        private TimeSpan Weeks(string match) => new(int.Parse(match) * 7, 0, 0, 0);

        private TimeSpan Months(string match) => new(int.Parse(match) * 30, 0, 0, 0);
    }
}