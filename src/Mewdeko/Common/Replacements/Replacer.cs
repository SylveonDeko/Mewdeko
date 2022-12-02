using System.Text.RegularExpressions;

namespace Mewdeko.Common.Replacements;

public class Replacer
{
    private readonly IEnumerable<(Regex Regex, Func<Match, string> Replacement)> regex;
    private readonly IEnumerable<(string Key, Func<string> Text)> replacements;

    public Replacer(IEnumerable<(string, Func<string>)> replacements,
        IEnumerable<(Regex, Func<Match, string>)> regex)
    {
        this.replacements = replacements;
        this.regex = regex;
    }

    public string? Replace(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        foreach (var (key, text) in replacements)
        {
            if (input.Contains(key))
                input = input.Replace(key, text(), StringComparison.InvariantCulture);
        }

        return regex.Aggregate(input, (current, item) => item.Regex.Replace(current, m => item.Replacement(m)));
    }
}