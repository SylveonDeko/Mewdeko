using System.Text.RegularExpressions;

namespace Mewdeko.Common.TypeReaders.Models;

public class StoopidTime
{
    private static readonly Regex Regex = new(
        @"^(?:(?<years>\d)y)?(?:(?<months>\d)mo)?(?:(?<weeks>\d{1,2})w)?(?:(?<days>\d{1,2})d)?(?:(?<hours>\d{1,4})h)?(?:(?<minutes>\d{1,5})m)?(?:(?<seconds>\d{1,6})s)?$"
        ,
        RegexOptions.Compiled | RegexOptions.Multiline);

    public string Input { get; set; }
    public TimeSpan Time { get; set; }

    public static StoopidTime FromInput(string input)
    {
        var m = Regex.Match(input);

        if (m.Length == 0) throw new ArgumentException("Invalid Time! Valid Example: 1h2d3m");

        var namesAndValues = new Dictionary<string, int>();

        foreach (var groupName in Regex.GetGroupNames())
        {
            if (groupName == "0") continue;
            if (!int.TryParse(m.Groups[groupName].Value, out var value))
            {
                namesAndValues[groupName] = 0;
                continue;
            }

            namesAndValues[groupName] = value;
        }

        var ts = new TimeSpan((365 * namesAndValues["years"]) +
                              (30 * namesAndValues["months"]) +
                              (7 * namesAndValues["weeks"]) +
                              namesAndValues["days"],
            namesAndValues["hours"],
            namesAndValues["minutes"],
            namesAndValues["seconds"]);

        return new StoopidTime
        {
            Input = input, Time = ts
        };
    }
}