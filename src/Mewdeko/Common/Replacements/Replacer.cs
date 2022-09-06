using System.Text.RegularExpressions;

namespace Mewdeko.Common.Replacements;

public class Replacer
{
    private readonly IEnumerable<(Regex Regex, Func<Match, string> Replacement)> _regex;
    private readonly IEnumerable<(string Key, Func<string> Text)> _replacements;

    public Replacer(IEnumerable<(string, Func<string>)> replacements,
        IEnumerable<(Regex, Func<Match, string>)> regex)
    {
        _replacements = replacements;
        _regex = regex;
    }

    public string? Replace(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        foreach (var (key, text) in _replacements)
        {
            if (input.Contains(key))
                input = input.Replace(key, text(), StringComparison.InvariantCulture);
        }

        return _regex.Aggregate(input, (current, item) => item.Regex.Replace(current, m => item.Replacement(m)));
    }
}