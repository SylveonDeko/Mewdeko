using System.Text.RegularExpressions;

namespace Mewdeko.Common.Replacements;

/// <summary>
/// Class that replaces placeholders in text with their corresponding values.
/// </summary>
public class Replacer
{
    /// <summary>
    /// Collection of regular expressions and their corresponding replacement functions.
    /// </summary>
    private readonly IEnumerable<(Regex Regex, Func<Match, string> Replacement)> regex;

    /// <summary>
    /// Collection of placeholder keys and their corresponding replacement functions.
    /// </summary>
    private readonly IEnumerable<(string Key, Func<string> Text)> replacements;

    /// <summary>
    /// Initializes a new instance of the Replacer class.
    /// </summary>
    /// <param name="replacements">Collection of placeholder keys and their corresponding replacement functions.</param>
    /// <param name="regex">Collection of regular expressions and their corresponding replacement functions.</param>
    public Replacer(IEnumerable<(string, Func<string>)> replacements,
        IEnumerable<(Regex, Func<Match, string>)> regex)
    {
        this.replacements = replacements;
        this.regex = regex;
    }

    /// <summary>
    /// Replaces placeholders in the input string with their corresponding values.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The input string with placeholders replaced with their corresponding values.</returns>
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